using System.Text;
using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// HUD de la "Misión Ares" flotando frente a la vista (se ve en ambos ojos):
    /// enlace con el control, rocas aseguradas, cronómetro, pinza y telemetría.
    /// Además muestra avisos grandes en el centro (roca asegurada, misión cumplida,
    /// enlace perdido). Tecla H (o tocar con 3 dedos) para ocultar/mostrar.
    /// </summary>
    public class HUDVR : MonoBehaviour
    {
        public TextMesh texto;
        [Tooltip("Texto grande y centrado para avisos cortos")]
        public TextMesh aviso;
        public RobotController robot;
        public RobotGripper gripper;
        public MisionTarea mision;
        public bool visible = true;

        const string Naranja = "#ffa552", Cian = "#6fe3ff", Verde = "#7dff8a", Amarillo = "#ffe066", Rojo = "#ff6b5a", Gris = "#d9c2b0";

        readonly StringBuilder sb = new StringBuilder(512);
        float tRefresco, tAviso = -99f, duracionAviso;
        int enZonaAntes;
        bool conectadoAntes, completadaAntes;

        void Start()
        {
            PrepararFuente(texto);
            PrepararFuente(aviso);
            if (aviso) aviso.text = "";
            Aplicar();
            MostrarAviso("<color=" + Naranja + ">MISIÓN ARES</color>\n<size=40>Lleva las rocas marcianas a la base</size>", 4f);
        }

        static void PrepararFuente(TextMesh t)
        {
            if (t == null || t.font != null) return;
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.font = f;
            var r = t.GetComponent<MeshRenderer>();
            if (r && f) r.sharedMaterial = f.material;
        }

        public void Alternar() { visible = !visible; Aplicar(); }

        void Aplicar()
        {
            if (texto) texto.gameObject.SetActive(visible);
        }

        public void MostrarAviso(string mensaje, float segundos = 2.5f)
        {
            if (!aviso) return;
            aviso.text = mensaje;
            tAviso = Time.time;
            duracionAviso = segundos;
            aviso.gameObject.SetActive(true);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.H)) Alternar();
            RevisarEventos();

            if (aviso && aviso.gameObject.activeSelf && Time.time - tAviso > duracionAviso)
                aviso.gameObject.SetActive(false);

            if (!visible || texto == null || Time.unscaledTime - tRefresco < 0.1f) return;
            tRefresco = Time.unscaledTime;

            var e = EnlaceHaptico.Instancia;
            sb.Length = 0;
            sb.Append("<color=").Append(Naranja).Append(">== MISIÓN ARES · ROVER UMNG-1 ==</color>\n");

            if (mision)
            {
                sb.Append("Rocas en la base  <color=").Append(Amarillo).Append('>');
                for (int i = 0; i < mision.ObjetosTotales; i++) sb.Append(i < mision.ObjetosEnZona ? "[#]" : "[ ]");
                sb.Append("</color>  ").Append(mision.ObjetosEnZona).Append('/').Append(mision.ObjetosTotales);
                sb.AppendFormat("     T+ {0:F1} s\n", mision.TiempoTarea);
            }
            if (gripper)
            {
                var m = gripper.Sostenido;
                if (m) sb.Append("Pinza: <color=").Append(Amarillo).Append(">").Append(m.Nombre).Append(" asegurada</color>\n");
                else sb.Append("Pinza: <color=").Append(Gris).Append(">vacía</color>\n");
            }
            if (e)
            {
                var s = e.Estado;
                sb.Append(e.Conectado ? "<color=" + Verde + ">ENLACE OK</color>  " : "<color=" + Rojo + ">SIN ENLACE</color>  ")
                  .Append("<color=").Append(Gris).Append('>').Append(e.DescripcionConexion).Append("</color>\n");
                var em = EmisorEspejo.Instancia;
                if (em && em.isActiveAndEnabled && !string.IsNullOrEmpty(em.IpEspectador))
                    sb.Append("<color=").Append(Cian).Append(">PC espectador: ").Append(em.IpEspectador).Append("</color>\n");
                sb.Append("<color=").Append(Cian).Append('>');
                if (robot) sb.AppendFormat("v {0:F2} m/s   ω {1:F0}°/s   ", robot.VelocidadLineal, robot.VelocidadAngular);
                sb.Append("RTT ").Append(e.LatenciaRTTms >= 0 ? e.LatenciaRTTms.ToString("F0") + " ms" : "--");
                if (s.bateria >= 0) sb.Append("   Bat ").Append(s.bateria).Append('%');
                sb.AppendFormat("\nPitch {0:F1}°  Roll {1:F1}°  Joy {2:F2}, {3:F2}</color>", s.pitch, s.roll, s.joystickX, s.joystickY);
                if (Time.time - e.TiempoUltimaVibracion < 0.8f && e.UltimaVibracion != PatronVibracion.Ninguno)
                    sb.Append("\n<color=").Append(Amarillo).Append(">>> VIBRACIÓN: ").Append(e.UltimaVibracion.ToString().ToUpper()).Append("</color>");
            }
            texto.text = sb.ToString();
        }

        void RevisarEventos()
        {
            if (mision)
            {
                if (mision.ObjetosEnZona > enZonaAntes && !mision.Completada)
                    MostrarAviso("<color=" + Verde + ">¡ROCA ASEGURADA!</color>\n<size=44>" + mision.ObjetosEnZona + " de " + mision.ObjetosTotales + " en la base</size>");
                enZonaAntes = mision.ObjetosEnZona;
                if (mision.Completada && !completadaAntes)
                    MostrarAviso("<color=" + Verde + ">¡MISIÓN CUMPLIDA!</color>\n<size=44>Tiempo: " + mision.TiempoTarea.ToString("F1") + " s</size>", 6f);
                completadaAntes = mision.Completada;
            }
            var e = EnlaceHaptico.Instancia;
            if (e)
            {
                if (e.Conectado && !conectadoAntes) MostrarAviso("<color=" + Cian + ">ENLACE CON EL CONTROL</color>", 2f);
                else if (!e.Conectado && conectadoAntes) MostrarAviso("<color=" + Rojo + ">ENLACE PERDIDO</color>", 2.5f);
                conectadoAntes = e.Conectado;
            }
        }
    }
}
