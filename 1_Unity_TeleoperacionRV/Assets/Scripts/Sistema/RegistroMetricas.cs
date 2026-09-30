using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// Guarda datos para el análisis de resultados en archivos CSV:
    ///   muestras_*.csv : 20 muestras/s de pitch, roll, comandos, velocidades, latencia...
    ///   eventos_*.csv  : colisiones, agarres, misión completada, etc.
    ///   resumen_*.txt  : latencia media / p95 / máx, jitter, tramas perdidas, colisiones, tiempo de tarea.
    /// En el PC los archivos quedan en la carpeta "Metricas" del proyecto.
    /// En Android: Android/data/com.umng.teleoperacionrv/files/Metricas
    /// </summary>
    public class RegistroMetricas : MonoBehaviour
    {
        public static RegistroMetricas Instancia { get; private set; }

        public bool registrar = true;
        public float muestrasPorSegundo = 20f;
        public RobotController robot;

        public string Carpeta { get; private set; }
        public List<float> Latencias { get; } = new List<float>();
        public List<float> Periodos { get; } = new List<float>();

        StreamWriter muestras, eventos;
        string sello;
        float tMuestra;
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        void Awake()
        {
            Instancia = this;
        }

        void Start()
        {
            if (!registrar) return;
            try
            {
                Carpeta = Application.isEditor
                    ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Metricas")
                    : Path.Combine(Application.persistentDataPath, "Metricas");
                Directory.CreateDirectory(Carpeta);
                sello = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                muestras = new StreamWriter(Path.Combine(Carpeta, $"muestras_{sello}.csv"), false, Encoding.UTF8);
                muestras.WriteLine("t_s,pitch_deg,roll_deg,cmd_lineal,cmd_angular,v_ms,w_degs,rtt_ms,periodo_trama_ms,conectado,teclado,boton_agarre,boton_adelante,boton_atras,bateria,joystick_y,joystick_x");
                eventos = new StreamWriter(Path.Combine(Carpeta, $"eventos_{sello}.csv"), false, Encoding.UTF8);
                eventos.WriteLine("t_s,evento,detalle,valor");
                Debug.Log("[Métricas] Guardando en: " + Carpeta);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Métricas] No se pudo crear el registro: " + e.Message);
                registrar = false;
            }

            var enlace = EnlaceHaptico.Instancia;
            if (enlace)
            {
                enlace.AlMedirLatencia += rtt => { Latencias.Add(rtt); };
                enlace.AlRecibirTrama += p => { if (Periodos.Count < 200000) Periodos.Add((float)p); };
                enlace.AlVibrar += (pat, i) => Evento("vibracion", pat.ToString(), i);
            }
        }

        void Update()
        {
            if (!registrar || muestras == null) return;
            if (Time.time - tMuestra < 1f / muestrasPorSegundo) return;
            tMuestra = Time.time;
            var e = EnlaceHaptico.Instancia;
            if (e == null) return;
            var s = e.Estado;
            muestras.WriteLine(string.Join(",",
                Time.time.ToString("F3", CI),
                s.pitch.ToString("F1", CI), s.roll.ToString("F1", CI),
                (robot ? robot.ComandoLineal : 0).ToString("F3", CI),
                (robot ? robot.ComandoAngular : 0).ToString("F3", CI),
                (robot ? robot.VelocidadLineal : 0).ToString("F3", CI),
                (robot ? robot.VelocidadAngular : 0).ToString("F2", CI),
                e.LatenciaRTTms.ToString("F1", CI),
                e.PeriodoTramaMs.ToString("F1", CI),
                e.Conectado ? 1 : 0, e.UsandoTeclado ? 1 : 0,
                s.botonAgarre ? 1 : 0, s.botonAdelante ? 1 : 0, s.botonAtras ? 1 : 0,
                s.bateria, s.joystickY.ToString("F2", CI), s.joystickX.ToString("F2", CI)));
        }

        /// <summary>Registra un evento (colisión, agarre, etc.).</summary>
        public static void Evento(string nombre, string detalle, float valor)
        {
            var r = Instancia;
            if (r == null || !r.registrar || r.eventos == null) return;
            r.eventos.WriteLine($"{Time.time.ToString("F3", CI)},{nombre},{(detalle ?? "").Replace(',', ' ')},{valor.ToString("F3", CI)}");
            r.eventos.Flush();
        }

        static float Percentil(List<float> datos, float p)
        {
            if (datos.Count == 0) return -1;
            var o = datos.OrderBy(x => x).ToList();
            int i = Mathf.Clamp(Mathf.CeilToInt(p / 100f * o.Count) - 1, 0, o.Count - 1);
            return o[i];
        }

        static float Desviacion(List<float> d)
        {
            if (d.Count < 2) return 0;
            float m = d.Average();
            return Mathf.Sqrt(d.Sum(x => (x - m) * (x - m)) / (d.Count - 1));
        }

        /// <summary>Texto con las estadísticas principales (también se muestra en el HUD).</summary>
        public string Resumen(MisionTarea mision = null, RobotHapticaColisiones colisiones = null)
        {
            var e = EnlaceHaptico.Instancia;
            var sb = new StringBuilder();
            sb.AppendLine("RESUMEN DE LA PRUEBA - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine($"Duración de la sesión (s): {Time.time:F1}");
            if (Latencias.Count > 0)
            {
                sb.AppendLine($"Pings medidos: {Latencias.Count}");
                sb.AppendLine($"Latencia ida y vuelta (RTT) media (ms): {Latencias.Average():F1}");
                sb.AppendLine($"RTT mediana (ms): {Percentil(Latencias, 50):F1}");
                sb.AppendLine($"RTT percentil 95 (ms): {Percentil(Latencias, 95):F1}");
                sb.AppendLine($"RTT mínima / máxima (ms): {Latencias.Min():F1} / {Latencias.Max():F1}");
                sb.AppendLine($"Latencia de un sentido estimada (RTT/2) (ms): {Latencias.Average() / 2f:F1}");
            }
            else sb.AppendLine("Latencia: sin datos (¿control conectado?)");
            if (Periodos.Count > 0)
            {
                sb.AppendLine($"Periodo medio entre tramas (ms): {Periodos.Average():F2}  (ideal 20 ms = 50 Hz)");
                sb.AppendLine($"Jitter (desviación del periodo) (ms): {Desviacion(Periodos):F2}");
            }
            if (e)
            {
                long total = e.TramasRecibidas + e.TramasPerdidas;
                sb.AppendLine($"Tramas recibidas: {e.TramasRecibidas}   perdidas: {e.TramasPerdidas}   ({(total > 0 ? 100.0 * e.TramasPerdidas / total : 0):F2} %)");
            }
            if (colisiones)
                sb.AppendLine($"Colisiones con objetos: {colisiones.ColisionesObjeto}   con paredes/obstáculos: {colisiones.ColisionesPared}");
            if (mision)
            {
                sb.AppendLine($"Objetos en la zona objetivo: {mision.ObjetosEnZona}/{mision.ObjetosTotales}");
                sb.AppendLine(mision.Completada ? $"Tiempo de la tarea (s): {mision.TiempoTarea:F1}" : "Tarea no completada");
            }
            return sb.ToString();
        }

        public void GuardarResumen(MisionTarea mision, RobotHapticaColisiones colisiones)
        {
            if (!registrar || string.IsNullOrEmpty(Carpeta)) return;
            try
            {
                File.WriteAllText(Path.Combine(Carpeta, $"resumen_{sello}.txt"), Resumen(mision, colisiones), Encoding.UTF8);
                muestras?.Flush();
                eventos?.Flush();
            }
            catch (Exception ex) { Debug.LogWarning("[Métricas] " + ex.Message); }
        }

        void OnApplicationPause(bool pausa)
        {
            if (pausa) { muestras?.Flush(); eventos?.Flush(); }
        }

        void OnDestroy()
        {
            var m = FindAnyObjectByType<MisionTarea>();
            var c = FindAnyObjectByType<RobotHapticaColisiones>();
            GuardarResumen(m, c);
            muestras?.Close(); muestras = null;
            eventos?.Close(); eventos = null;
            if (Instancia == this) Instancia = null;
        }
    }
}
