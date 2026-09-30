using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TeleoperacionRV
{
    public enum ModoTransporte { Automatico, BluetoothAndroid, SerialPC, SoloTeclado }

    /// <summary>Patrones de vibración (deben coincidir con el firmware del ESP32).</summary>
    public enum PatronVibracion { Ninguno = 0, Objeto = 1, Pared = 2, Agarre = 3, Exito = 4 }

    /// <summary>Último estado conocido del control háptico.</summary>
    [Serializable]
    public struct EstadoControl
    {
        public float pitch;          // grados (+ = inclinado hacia adelante)
        public float roll;           // grados (+ = inclinado a la derecha)
        public bool botonAgarre;     // presionar la palanca del joystick (SW)
        public bool botonAdelante;   // joystick empujado hacia el frente (pasó el umbral)
        public bool botonAtras;      // joystick hacia atrás
        public float joystickY;      // -1..1 (proporcional): avatar adelante/atrás
        public float joystickX;      // -1..1 (proporcional): girar el avatar
        public int bateria;          // % (-1 = desconocido)
    }

    /// <summary>
    /// Centro de la comunicación con el control háptico:
    ///  - elige el transporte según la plataforma (Bluetooth en Android, COM en PC),
    ///  - interpreta las tramas "D,..." del ESP32,
    ///  - envía órdenes de vibración "H,patron,intensidad",
    ///  - mide la latencia con pings "P,id" y las tramas perdidas,
    ///  - si no hay control conectado, lo simula con el teclado (W/S/A/D, G, flechas).
    /// Control: IMU (pitch/roll) + joystick HW-504 (eje Y = avanzar, eje X = girar, presionar = agarre).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class EnlaceHaptico : MonoBehaviour
    {
        public static EnlaceHaptico Instancia { get; private set; }

        [Header("Conexión")]
        public ModoTransporte modo = ModoTransporte.Automatico;
        [Tooltip("Nombre Bluetooth del ESP32 (debe coincidir con el firmware)")]
        public string nombreBluetooth = "ControlHaptico_RV";
        [Tooltip("Puerto COM en Windows, por ejemplo COM5. AUTO = buscarlo solo.")]
        public string puertoSerial = "AUTO";
        public int baudios = 115200;

        [Header("Simulación con teclado (cuando no hay control)")]
        public bool simularConTecladoSiNoHayControl = true;
        public float anguloSimulado = 25f;
        public float velocidadRampaSimulada = 80f;

        [Header("Medición de latencia")]
        public float periodoPingSegundos = 0.5f;

        // --------- Estado público ---------
        public EstadoControl Estado => estado;
        public bool Conectado => transporte != null && transporte.Conectado;
        public bool UsandoTeclado { get; private set; }
        public string DescripcionConexion => UsandoTeclado ? "TECLADO (simulado) | " + (transporte?.Descripcion ?? "") : (transporte?.Descripcion ?? "sin transporte");
        public float LatenciaRTTms { get; private set; } = -1f;
        public float PeriodoTramaMs { get; private set; }
        public long TramasRecibidas { get; private set; }
        public long TramasPerdidas { get; private set; }
        public PatronVibracion UltimaVibracion { get; private set; }
        public float TiempoUltimaVibracion { get; private set; } = -999f;

        public event Action AlPresionarAgarre;
        public event Action AlSoltarAgarre;
        public event Action<float> AlMedirLatencia;                 // RTT en ms
        public event Action<PatronVibracion, int> AlVibrar;
        public event Action<double> AlRecibirTrama;                  // periodo entre tramas en ms

        ITransporte transporte;
        EstadoControl estado;
        EstadoControl estadoTeclado;
        bool agarreAnterior;
        double tTramaAnterior = -1;
        long secAnterior = -1;
        float tPing;
        int idPing;
        readonly Dictionary<int, double> pingsPendientes = new Dictionary<int, double>();

        void Awake()
        {
            if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
            Instancia = this;
            estado.bateria = -1;
        }

        void Start()
        {
            PedirPermisosAndroid();
            transporte = CrearTransporte();
            transporte?.Iniciar();
        }

        ITransporte CrearTransporte()
        {
            var m = modo;
            if (m == ModoTransporte.Automatico)
                m = Application.platform == RuntimePlatform.Android ? ModoTransporte.BluetoothAndroid : ModoTransporte.SerialPC;
            switch (m)
            {
                case ModoTransporte.BluetoothAndroid: return new TransporteBluetoothAndroid(nombreBluetooth);
                case ModoTransporte.SerialPC: return new TransporteSerialPC(puertoSerial, baudios);
                default: return null;
            }
        }

        static void PedirPermisosAndroid()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android 12+ exige pedir BLUETOOTH_CONNECT en tiempo de ejecución
            const string permiso = "android.permission.BLUETOOTH_CONNECT";
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(permiso))
                UnityEngine.Android.Permission.RequestUserPermission(permiso);
#endif
        }

        void Update()
        {
            // 1) Leer todo lo que llegó del control
            if (transporte != null)
                while (transporte.IntentarLeer(out var linea)) Procesar(linea);

            // 2) Si no hay control, usar el teclado
            UsandoTeclado = !Conectado && simularConTecladoSiNoHayControl;
            if (UsandoTeclado)
            {
                SimularTeclado();
                estado = estadoTeclado;
            }

            // 3) Detectar flancos del botón de agarre
            if (estado.botonAgarre && !agarreAnterior) AlPresionarAgarre?.Invoke();
            if (!estado.botonAgarre && agarreAnterior) AlSoltarAgarre?.Invoke();
            agarreAnterior = estado.botonAgarre;

            // 4) Pings periódicos para medir latencia
            if (Conectado && Time.unscaledTime - tPing >= periodoPingSegundos)
            {
                tPing = Time.unscaledTime;
                idPing = (idPing + 1) % 100000;
                pingsPendientes[idPing] = Reloj.Ms;
                transporte.Enviar("P," + idPing);
                if (pingsPendientes.Count > 50) pingsPendientes.Clear();
            }
        }

        void Procesar(LineaRecibida l)
        {
            string[] p = l.texto.Split(',');
            if (p.Length == 0) return;
            switch (p[0])
            {
                case "D":
                    // D,seq,pitch,roll,agarre,adelante,atras,bateria[,joyY[,joyX]]
                    if (p.Length < 7) return;
                    long seq;
                    if (!long.TryParse(p[1], out seq)) return;
                    float pitch, roll;
                    if (!float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out pitch)) return;
                    if (!float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out roll)) return;
                    estado.pitch = pitch;
                    estado.roll = roll;
                    estado.botonAgarre = p[4] == "1";
                    estado.botonAdelante = p[5] == "1";
                    estado.botonAtras = p[6] == "1";
                    int bat;
                    estado.bateria = (p.Length >= 8 && int.TryParse(p[7], out bat)) ? bat : -1;
                    float joy;
                    if (p.Length >= 9 && float.TryParse(p[8], NumberStyles.Float, CultureInfo.InvariantCulture, out joy))
                        estado.joystickY = Mathf.Clamp(joy / 100f, -1f, 1f);
                    else
                        estado.joystickY = estado.botonAdelante ? 1f : (estado.botonAtras ? -1f : 0f);
                    float joyX;
                    estado.joystickX = (p.Length >= 10 && float.TryParse(p[9], NumberStyles.Float, CultureInfo.InvariantCulture, out joyX))
                        ? Mathf.Clamp(joyX / 100f, -1f, 1f) : 0f;

                    if (secAnterior >= 0 && seq > secAnterior + 1) TramasPerdidas += seq - secAnterior - 1;
                    secAnterior = seq;
                    TramasRecibidas++;
                    if (tTramaAnterior > 0)
                    {
                        double periodo = l.tiempoMs - tTramaAnterior;
                        PeriodoTramaMs = PeriodoTramaMs <= 0 ? (float)periodo : Mathf.Lerp(PeriodoTramaMs, (float)periodo, 0.05f);
                        AlRecibirTrama?.Invoke(periodo);
                    }
                    tTramaAnterior = l.tiempoMs;
                    break;

                case "P":
                    int id;
                    double t0;
                    if (p.Length >= 2 && int.TryParse(p[1], out id) && pingsPendientes.TryGetValue(id, out t0))
                    {
                        pingsPendientes.Remove(id);
                        LatenciaRTTms = (float)(l.tiempoMs - t0);
                        AlMedirLatencia?.Invoke(LatenciaRTTms);
                    }
                    break;

                case "I":
                    Debug.Log("[EnlaceHaptico] Control conectado: " + l.texto);
                    secAnterior = -1;
                    break;

                case "K":
                    Debug.Log("[EnlaceHaptico] Control calibrado (nuevo cero).");
                    break;
            }
        }

        void SimularTeclado()
        {
            float dt = Time.unscaledDeltaTime;
            float objPitch = 0, objRoll = 0;
            if (Input.GetKey(KeyCode.W)) objPitch += anguloSimulado;
            if (Input.GetKey(KeyCode.S)) objPitch -= anguloSimulado;
            if (Input.GetKey(KeyCode.D)) objRoll += anguloSimulado;
            if (Input.GetKey(KeyCode.A)) objRoll -= anguloSimulado;
            estadoTeclado.pitch = Mathf.MoveTowards(estadoTeclado.pitch, objPitch, velocidadRampaSimulada * dt);
            estadoTeclado.roll = Mathf.MoveTowards(estadoTeclado.roll, objRoll, velocidadRampaSimulada * dt);
            estadoTeclado.botonAgarre = Input.GetKey(KeyCode.G) || Input.GetKey(KeyCode.Space);
            estadoTeclado.botonAdelante = Input.GetKey(KeyCode.UpArrow);
            estadoTeclado.botonAtras = Input.GetKey(KeyCode.DownArrow);
            estadoTeclado.joystickY = estadoTeclado.botonAdelante ? 1f : (estadoTeclado.botonAtras ? -1f : 0f);
            estadoTeclado.joystickX = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            estadoTeclado.bateria = -1;
        }

        /// <summary>Ordena al control reproducir un patrón de vibración.</summary>
        public void Vibrar(PatronVibracion patron, int intensidad = 255)
        {
            intensidad = Mathf.Clamp(intensidad, 0, 255);
            UltimaVibracion = patron;
            TiempoUltimaVibracion = Time.time;
            transporte?.Enviar("H," + (int)patron + "," + intensidad);
            AlVibrar?.Invoke(patron, intensidad);
        }

        /// <summary>Toma la posición actual de la mano como "cero".</summary>
        public void CalibrarControl()
        {
            transporte?.Enviar("C");
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            transporte?.Detener();
            transporte = null;
        }

        void OnApplicationQuit()
        {
            transporte?.Detener();
        }
    }
}
