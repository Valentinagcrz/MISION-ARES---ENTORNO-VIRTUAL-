using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// (Celular) Envía el estado de la simulación por Wi-Fi al computador para el
    /// modo espectador. Primero lo difunde a toda la red (broadcast); cuando el PC
    /// responde, se lo envía directamente a su IP.
    /// </summary>
    public class EmisorEspejo : MonoBehaviour
    {
        public float envioHz = 30f;
        [Tooltip("Enviar también desde el PC/Editor (solo para pruebas)")]
        public bool enviarEnPC = false;

        public RobotController robot;
        public RobotGripper gripper;
        public AvatarNavegacion avatar;
        public Transform cabeza;
        public MisionTarea mision;

        public static EmisorEspejo Instancia { get; private set; }
        /// <summary>IP del PC espectador que confirmó recepción (vacío si ninguno).</summary>
        public string IpEspectador => espectador != null && Time.realtimeSinceStartup - tUltimaConfirmacion < 4f ? espectador.Address.ToString() : "";

        UdpClient udp;
        Thread hilo;
        volatile bool activo;
        volatile IPEndPoint espectador;
        volatile float tUltimaConfirmacionHilo;
        float tUltimaConfirmacion => tUltimaConfirmacionHilo;
        IPEndPoint[] difusion;
        Manipulable[] objetos;
        float tEnvio, tDifusion;
        int secuencia;
        readonly PaqueteEspejo p = new PaqueteEspejo();
        float reloj;

        void Awake() { Instancia = this; }

        void Start()
        {
            if (!Application.isMobilePlatform && !enviarEnPC) { enabled = false; return; }
            objetos = PaqueteEspejo.ObjetosOrdenados();
            try
            {
                udp = new UdpClient(0) { EnableBroadcast = true };
                difusion = DireccionesDifusion();
                activo = true;
                hilo = new Thread(Escuchar) { IsBackground = true, Name = "EmisorEspejo" };
                hilo.Start();
            }
            catch (Exception e) { Debug.LogWarning("[Espejo] No se pudo abrir UDP: " + e.Message); enabled = false; }
        }

        static IPEndPoint[] DireccionesDifusion()
        {
            var lista = new System.Collections.Generic.List<IPEndPoint> { new IPEndPoint(IPAddress.Broadcast, PaqueteEspejo.Puerto) };
            try
            {
                // Truco: "conectar" un socket UDP (no envía nada) para saber la IP local de la Wi-Fi
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    s.Connect("8.8.8.8", 65530);
                    var ip = ((IPEndPoint)s.LocalEndPoint).Address.GetAddressBytes();
                    ip[3] = 255;   // red /24 (la más común en casas y hotspots)
                    lista.Add(new IPEndPoint(new IPAddress(ip), PaqueteEspejo.Puerto));
                }
            }
            catch (Exception) { }
            return lista.ToArray();
        }

        void Escuchar()
        {
            var desde = new IPEndPoint(IPAddress.Any, 0);
            while (activo)
            {
                try
                {
                    var d = udp.Receive(ref desde);
                    if (d.Length >= 4 && d[0] == 'T' && d[1] == 'R' && d[2] == 'V' && d[3] == 'A')
                    {
                        espectador = new IPEndPoint(desde.Address, PaqueteEspejo.Puerto);
                        tUltimaConfirmacionHilo = reloj;
                    }
                }
                catch (Exception) { if (activo) Thread.Sleep(200); }
            }
        }

        void Update()
        {
            reloj = Time.realtimeSinceStartup;
            if (udp == null || Time.unscaledTime - tEnvio < 1f / envioHz) return;
            tEnvio = Time.unscaledTime;
            Llenar();
            var datos = p.Serializar();
            try
            {
                bool hayPC = !string.IsNullOrEmpty(IpEspectador);
                if (hayPC) udp.Send(datos, datos.Length, espectador);
                // Difusión: siempre si no hay PC, y 1 vez por segundo para que otro PC pueda unirse
                if (!hayPC || Time.unscaledTime - tDifusion > 1f)
                {
                    tDifusion = Time.unscaledTime;
                    foreach (var ep in difusion) { try { udp.Send(datos, datos.Length, ep); } catch (Exception) { } }
                }
            }
            catch (Exception) { }
        }

        void Llenar()
        {
            p.secuencia = ++secuencia;
            p.tiempo = Time.time;
            if (robot)
            {
                p.robotPos = robot.transform.position; p.robotRot = robot.transform.rotation;
                p.velLineal = robot.VelocidadLineal; p.velAngular = robot.VelocidadAngular;
            }
            if (gripper) { p.apertura = gripper.Apertura; p.sostenidoNombre = gripper.Sostenido ? gripper.Sostenido.Nombre : ""; }
            if (avatar) { p.avatarPos = avatar.transform.position; p.avatarYaw = avatar.transform.eulerAngles.y; }
            if (cabeza) { p.cabezaPos = cabeza.position; p.cabezaRot = cabeza.rotation; }
            int n = objetos?.Length ?? 0;
            if (p.objPos.Length != n) { p.objPos = new Vector3[n]; p.objRot = new Quaternion[n]; p.objSostenido = new bool[n]; }
            for (int i = 0; i < n; i++)
            {
                var o = objetos[i];
                if (!o) continue;
                p.objPos[i] = o.transform.position; p.objRot[i] = o.transform.rotation; p.objSostenido[i] = o.Sostenido;
            }
            if (mision)
            {
                p.enZona = mision.ObjetosEnZona; p.total = mision.ObjetosTotales; p.tiempoTarea = mision.TiempoTarea;
                p.iniciada = mision.Iniciada; p.completada = mision.Completada;
            }
            var e = EnlaceHaptico.Instancia;
            if (e)
            {
                var s = e.Estado;
                p.conectado = e.Conectado; p.pitch = s.pitch; p.roll = s.roll; p.joyX = s.joystickX; p.joyY = s.joystickY;
                p.rtt = e.LatenciaRTTms; p.bateria = s.bateria;
                p.ultimaVibracion = (int)e.UltimaVibracion; p.edadVibracion = Time.time - e.TiempoUltimaVibracion;
            }
        }

        void OnDestroy()
        {
            activo = false;
            try { udp?.Close(); } catch (Exception) { }
            if (Instancia == this) Instancia = null;
        }
    }
}
