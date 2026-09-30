using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// (Computador) MODO ESPECTADOR: recibe por Wi-Fi el estado que envía el celular
    /// y muestra la misma simulación en el monitor, en vivo, con varias cámaras:
    ///   1 = vista del usuario (lo que ve en el visor)   2 = seguir al rover
    ///   3 = vista aérea   4 = tercera persona (detrás del usuario)
    ///   P = mostrar/ocultar la ventanita con la vista del usuario · F2 = activar/desactivar
    /// Se activa solo cuando llegan datos del celular (misma red Wi-Fi, puerto UDP 47777).
    /// </summary>
    public class ReceptorEspejo : MonoBehaviour
    {
        [Header("Escena")]
        public RobotController robot;
        public RobotGripper gripper;
        public AvatarNavegacion avatar;
        public Transform cabeza;
        public GameObject cuerpoAvatar;       // visual del astronauta (oculto en el celular)
        public GameObject cascoAvatar;        // casco (va en la cabeza)
        public Behaviour[] desactivarEnEspectador;
        public GameObject[] ocultarEnEspectador;

        [Header("Opciones")]
        public float suavizado = 18f;
        public bool activarAutomaticamente = true;

        public bool Activo { get; private set; }

        UdpClient udp;
        Thread hilo;
        volatile bool escuchando;
        readonly object candado = new object();
        byte[] ultimoDato;
        IPEndPoint origen;
        string ipCelular = "";
        float tUltimoPaquete = -99f, tConfirmacion;
        int paquetesSegundo, cuenta; float tCuenta;
        int ultimaSecuencia, perdidos;

        PaqueteEspejo estado;
        Manipulable[] objetos;
        Camera camPrincipal, camUsuario;
        int vista = 2;
        bool insercion = true;
        Vector3 camPos; Quaternion camRot;
        string aviso = ""; float tAviso = -99f; int enZonaAntes; bool completadaAntes;

        void Start()
        {
            if (Application.isMobilePlatform) { enabled = false; return; }
            objetos = PaqueteEspejo.ObjetosOrdenados();
            MostrarAstronauta(false);
            try
            {
                udp = new UdpClient();
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, PaqueteEspejo.Puerto));
                escuchando = true;
                hilo = new Thread(Escuchar) { IsBackground = true, Name = "ReceptorEspejo" };
                hilo.Start();
            }
            catch (Exception e) { Debug.LogWarning("[Espejo] No se pudo abrir el puerto " + PaqueteEspejo.Puerto + ": " + e.Message); }
        }

        void Escuchar()
        {
            var desde = new IPEndPoint(IPAddress.Any, 0);
            while (escuchando)
            {
                try
                {
                    var d = udp.Receive(ref desde);
                    lock (candado) { ultimoDato = d; origen = desde; }
                }
                catch (Exception) { if (escuchando) Thread.Sleep(100); }
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2)) { if (Activo) Desactivar(); else Activar(); }

            byte[] d; IPEndPoint ep;
            lock (candado) { d = ultimoDato; ep = origen; ultimoDato = null; }
            if (d != null)
            {
                var p = PaqueteEspejo.Leer(d);
                if (p != null)
                {
                    if (ultimaSecuencia > 0 && p.secuencia > ultimaSecuencia + 1) perdidos += p.secuencia - ultimaSecuencia - 1;
                    if (p.secuencia < ultimaSecuencia - 100) perdidos = 0;   // la app se reinició
                    ultimaSecuencia = p.secuencia;
                    estado = p;
                    tUltimoPaquete = Time.unscaledTime;
                    ipCelular = ep.Address.ToString();
                    cuenta++;
                    if (!Activo && activarAutomaticamente) Activar();
                    // Confirmación para que el celular envíe directo a este PC
                    if (Time.unscaledTime - tConfirmacion > 1f)
                    {
                        tConfirmacion = Time.unscaledTime;
                        try { udp.Send(PaqueteEspejo.Confirmacion, 4, ep); } catch (Exception) { }
                    }
                }
            }
            if (Time.unscaledTime - tCuenta >= 1f) { paquetesSegundo = cuenta; cuenta = 0; tCuenta = Time.unscaledTime; }

            if (!Activo) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) vista = 1;
            if (Input.GetKeyDown(KeyCode.Alpha2)) vista = 2;
            if (Input.GetKeyDown(KeyCode.Alpha3)) vista = 3;
            if (Input.GetKeyDown(KeyCode.Alpha4)) vista = 4;
            if (Input.GetKeyDown(KeyCode.P)) insercion = !insercion;
            if (Input.GetMouseButtonDown(0)) vista = vista % 4 + 1;
            if (estado != null) Aplicar(estado);
            MoverCamaras();
        }

        void Aplicar(PaqueteEspejo p)
        {
            float k = 1f - Mathf.Exp(-suavizado * Time.deltaTime);
            if (robot)
            {
                var t = robot.transform;
                t.SetPositionAndRotation(Vector3.Lerp(t.position, p.robotPos, k), Quaternion.Slerp(t.rotation, p.robotRot, k));
                GirarRuedas(p.velLineal, p.velAngular);
            }
            if (gripper) gripper.FijarApertura(Mathf.Lerp(gripper.Apertura, p.apertura, k));
            if (avatar)
            {
                var t = avatar.transform;
                t.SetPositionAndRotation(Vector3.Lerp(t.position, p.avatarPos, k),
                    Quaternion.Slerp(t.rotation, Quaternion.Euler(0, p.avatarYaw, 0), k));
            }
            if (cabeza) cabeza.SetPositionAndRotation(Vector3.Lerp(cabeza.position, p.cabezaPos, k), Quaternion.Slerp(cabeza.rotation, p.cabezaRot, k));
            int n = Mathf.Min(objetos.Length, p.objPos.Length);
            for (int i = 0; i < n; i++)
            {
                var o = objetos[i];
                if (!o) continue;
                var t = o.transform;
                float ko = p.objSostenido[i] ? 1f : k;   // si está en la pinza, sin retraso
                t.SetPositionAndRotation(Vector3.Lerp(t.position, p.objPos[i], ko), Quaternion.Slerp(t.rotation, p.objRot[i], ko));
            }

            if (p.enZona > enZonaAntes && !p.completada) MostrarAviso("¡ROCA ASEGURADA!  " + p.enZona + " de " + p.total);
            enZonaAntes = p.enZona;
            if (p.completada && !completadaAntes) MostrarAviso("¡MISIÓN CUMPLIDA!  " + p.tiempoTarea.ToString("F1") + " s");
            completadaAntes = p.completada;
        }

        void GirarRuedas(float v, float w)
        {
            if (!robot || robot.radioRueda <= 0) return;
            float vI = v - Mathf.Deg2Rad * w * robot.anchoVia * 0.5f, vD = v + Mathf.Deg2Rad * w * robot.anchoVia * 0.5f;
            foreach (var r in robot.ruedasIzquierdas) if (r) r.Rotate(Vector3.up, vI / robot.radioRueda * Mathf.Rad2Deg * Time.deltaTime, Space.Self);
            foreach (var r in robot.ruedasDerechas) if (r) r.Rotate(Vector3.up, vD / robot.radioRueda * Mathf.Rad2Deg * Time.deltaTime, Space.Self);
        }

        void MostrarAviso(string s) { aviso = s; tAviso = Time.unscaledTime; }

        // ------------------------------------------------------------------
        public void Activar()
        {
            if (Activo) return;
            Activo = true;
            foreach (var b in desactivarEnEspectador) if (b) b.enabled = false;
            foreach (var g in ocultarEnEspectador) if (g) g.SetActive(false);
            foreach (var c in cabeza.GetComponentsInChildren<Camera>(true)) c.enabled = false;
            if (avatar) { var cc = avatar.GetComponent<CharacterController>(); if (cc) cc.enabled = false; }
            if (robot) { var rb = robot.GetComponent<Rigidbody>(); if (rb) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; } }
            foreach (var o in objetos)
                if (o) { var rb = o.GetComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }
            MostrarAstronauta(true);

            camPrincipal = CrearCamara("Camara_Espectador", 0, new Rect(0, 0, 1, 1), 60f);
            camUsuario = CrearCamara("Camara_Vista_Usuario", 1, new Rect(0.695f, 0.03f, 0.29f, 0.29f), 75f);
            camPos = robot ? robot.transform.position + new Vector3(0, 2f, -3f) : Vector3.up * 3f;
            camRot = Quaternion.identity;
            Debug.Log("[Espejo] Modo espectador activado");
        }

        public void Desactivar()
        {
            if (!Activo) return;
            Activo = false;
            if (camPrincipal) Destroy(camPrincipal.gameObject);
            if (camUsuario) Destroy(camUsuario.gameObject);
            MostrarAstronauta(false);
            foreach (var b in desactivarEnEspectador) if (b) b.enabled = true;
            foreach (var g in ocultarEnEspectador) if (g) g.SetActive(true);
            var estereo = cabeza ? cabeza.GetComponent<CamaraEstereoCardboard>() : null;
            if (estereo) estereo.Aplicar();
            if (avatar) { var cc = avatar.GetComponent<CharacterController>(); if (cc) cc.enabled = true; }
            if (robot) { var rb = robot.GetComponent<Rigidbody>(); if (rb) { rb.isKinematic = false; rb.interpolation = RigidbodyInterpolation.Interpolate; } }
            foreach (var o in objetos)
                if (o && !o.Sostenido) { var rb = o.GetComponent<Rigidbody>(); rb.isKinematic = false; rb.interpolation = RigidbodyInterpolation.Interpolate; }
        }

        void MostrarAstronauta(bool v)
        {
            if (cuerpoAvatar) cuerpoAvatar.SetActive(v);
            if (cascoAvatar) cascoAvatar.SetActive(v);
        }

        Camera CrearCamara(string nombre, int profundidad, Rect rect, float fov)
        {
            var g = new GameObject(nombre);
            var c = g.AddComponent<Camera>();
            c.depth = 10 + profundidad;
            c.rect = rect;
            c.fieldOfView = fov;
            c.nearClipPlane = 0.05f;
            c.farClipPlane = 180f;
            c.clearFlags = CameraClearFlags.Skybox;
            return c;
        }

        void MoverCamaras()
        {
            if (!camPrincipal) return;
            Vector3 destino; Quaternion mira;
            var rt = robot ? robot.transform : transform;
            switch (vista)
            {
                case 1:
                    destino = cabeza.position; mira = cabeza.rotation; break;
                case 3:
                    destino = new Vector3(0f, 17f, -17f); mira = Quaternion.LookRotation(new Vector3(0, 0, 1.5f) - destino); break;
                case 4:
                {
                    var fwd = Vector3.ProjectOnPlane(cabeza.forward, Vector3.up).normalized;
                    if (fwd.sqrMagnitude < 0.01f) fwd = avatar.transform.forward;
                    destino = cabeza.position - fwd * 2.6f + Vector3.up * 0.9f;
                    mira = Quaternion.LookRotation(cabeza.position + fwd * 3f - destino);
                    break;
                }
                default:
                {
                    var fwd = Vector3.ProjectOnPlane(rt.forward, Vector3.up).normalized;
                    destino = rt.position - fwd * 2.4f + Vector3.up * 1.5f;
                    mira = Quaternion.LookRotation(rt.position + fwd * 1.2f + Vector3.up * 0.2f - destino);
                    break;
                }
            }
            float k = vista == 1 ? 1f : 1f - Mathf.Exp(-5f * Time.deltaTime);
            camPos = Vector3.Lerp(camPos, destino, k);
            camRot = Quaternion.Slerp(camRot, mira, k);
            camPrincipal.transform.SetPositionAndRotation(camPos, camRot);

            if (camUsuario)
            {
                camUsuario.enabled = insercion && vista != 1;
                camUsuario.transform.SetPositionAndRotation(cabeza.position, cabeza.rotation);
            }
        }

        // ------------------------------------------------------------------
        GUIStyle titulo, texto, grande, caja;

        void OnGUI()
        {
            if (Application.isMobilePlatform) return;
            if (titulo == null)
            {
                titulo = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, richText = true };
                texto = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
                grande = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
                var fondo = new Texture2D(1, 1); fondo.SetPixel(0, 0, new Color(0.08f, 0.04f, 0.03f, 0.72f)); fondo.Apply();
                caja = new GUIStyle(GUI.skin.box); caja.normal.background = fondo;
            }

            if (!Activo)
            {
                bool hay = Time.unscaledTime - tUltimoPaquete < 2f;
                GUI.Box(new Rect(10, 10, 470, 58), GUIContent.none, caja);
                GUI.Label(new Rect(20, 14, 460, 24), "<color=#ffa552>Modo espectador:</color> " + (hay ? "hay datos del celular — F2 para activar" : "esperando al celular por Wi-Fi…"), texto);
                GUI.Label(new Rect(20, 38, 460, 24), "<color=#d9c2b0>(misma red Wi-Fi · puerto UDP " + PaqueteEspejo.Puerto + " · F2 = activar/desactivar)</color>", texto);
                return;
            }

            var p = estado;
            bool vivo = Time.unscaledTime - tUltimoPaquete < 1.5f;
            string[] nombresVista = { "", "Vista del usuario (visor)", "Siguiendo al rover", "Vista aérea", "Tercera persona" };
            GUI.Box(new Rect(10, 10, 520, 210), GUIContent.none, caja);
            GUI.Label(new Rect(22, 14, 500, 30), "<color=#ffa552>MISIÓN ARES</color> · vista del espectador", titulo);
            GUI.Label(new Rect(22, 46, 500, 22), vivo
                ? "<color=#7dff8a>● EN VIVO</color> desde el celular " + ipCelular + " · " + paquetesSegundo + " paquetes/s · perdidos " + perdidos
                : "<color=#ff6b5a>● SIN SEÑAL del celular</color> (última IP " + ipCelular + ")", texto);
            if (p != null)
            {
                string barras = "";
                for (int i = 0; i < p.total; i++) barras += i < p.enZona ? "■" : "□";
                GUI.Label(new Rect(22, 72, 500, 22), "Rocas en la base: <color=#ffe066>" + barras + "</color>  " + p.enZona + "/" + p.total +
                    "     T+ " + p.tiempoTarea.ToString("F1") + " s", texto);
                GUI.Label(new Rect(22, 96, 500, 22), "Pinza: " + (string.IsNullOrEmpty(p.sostenidoNombre) ? "<color=#d9c2b0>vacía</color>" : "<color=#ffe066>" + p.sostenidoNombre + " asegurada</color>") +
                    "     Rover: " + p.velLineal.ToString("F2") + " m/s  " + p.velAngular.ToString("F0") + " °/s", texto);
                GUI.Label(new Rect(22, 120, 500, 22), "Control: " + (p.conectado ? "<color=#7dff8a>conectado</color>" : "<color=#ff6b5a>desconectado</color>") +
                    "   Pitch " + p.pitch.ToString("F1") + "°  Roll " + p.roll.ToString("F1") + "°  Joy " + p.joyX.ToString("F2") + ", " + p.joyY.ToString("F2"), texto);
                GUI.Label(new Rect(22, 144, 500, 22), "Latencia BT (RTT): " + (p.rtt >= 0 ? p.rtt.ToString("F0") + " ms" : "--") +
                    (p.bateria >= 0 ? "     Batería: " + p.bateria + "%" : ""), texto);
                if (p.ultimaVibracion != 0 && p.edadVibracion < 0.8f)
                    GUI.Label(new Rect(22, 168, 500, 22), "<color=#ffe066>>> VIBRACIÓN: " + ((PatronVibracion)p.ultimaVibracion).ToString().ToUpper() + "</color>", texto);
            }
            GUI.Label(new Rect(22, 192, 500, 22), "<color=#6fe3ff>Cámara " + vista + ": " + nombresVista[vista] + "</color>  (1-4, clic = cambiar · P = ventanita)", texto);

            if (camUsuario && camUsuario.enabled)
            {
                var r = camUsuario.pixelRect;
                GUI.Label(new Rect(r.x, Screen.height - r.yMax - 24, r.width, 22), "<color=#ffa552>Lo que ve el usuario</color>", texto);
            }
            if (Time.unscaledTime - tAviso < 3f)
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 60), "<color=#7dff8a>" + aviso + "</color>", grande);
        }

        void OnDestroy()
        {
            escuchando = false;
            try { udp?.Close(); } catch (Exception) { }
        }
    }
}
