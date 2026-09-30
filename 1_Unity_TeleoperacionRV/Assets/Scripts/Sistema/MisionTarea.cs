using System.Linq;
using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// Tarea de prueba estandarizada ("Misión Ares"): llevar todas las rocas marcianas
    /// de muestra a la base de recolección.
    /// El cronómetro arranca con el primer movimiento del robot y se detiene al
    /// completar la tarea. Sirve como métrica de "tiempo de tarea" para el informe.
    /// </summary>
    public class MisionTarea : MonoBehaviour
    {
        public ZonaObjetivo zona;
        public RobotController robot;
        public RobotGripper gripper;
        public RobotHapticaColisiones colisiones;
        public AvatarNavegacion avatar;

        public int ObjetosTotales { get; private set; }
        public int ObjetosEnZona { get; private set; }
        public bool Iniciada { get; private set; }
        public bool Completada { get; private set; }
        public float TiempoTarea => Iniciada ? (Completada ? tFin : Time.time) - tInicio : 0f;

        Manipulable[] objetos;
        float tInicio, tFin;
        int enZonaAntes;

        void Start()
        {
            objetos = FindObjectsByType<Manipulable>(FindObjectsSortMode.None);
            ObjetosTotales = objetos.Count(o => o.cuentaParaMision);
        }

        void Update()
        {
            if (!Iniciada && robot && (Mathf.Abs(robot.ComandoLineal) > 0.01f || Mathf.Abs(robot.ComandoAngular) > 0.01f))
            {
                Iniciada = true;
                tInicio = Time.time;
                RegistroMetricas.Evento("inicio_tarea", "", 0);
            }

            ObjetosEnZona = zona ? zona.Contar() : 0;
            // Cada roca que queda asegurada en la base se confirma con dos pulsos de vibración
            if (ObjetosEnZona > enZonaAntes) EnlaceHaptico.Instancia?.Vibrar(PatronVibracion.Exito, 255);
            enZonaAntes = ObjetosEnZona;
            if (Iniciada && !Completada && ObjetosTotales > 0 && ObjetosEnZona >= ObjetosTotales)
            {
                Completada = true;
                tFin = Time.time;
                RegistroMetricas.Evento("tarea_completada", "", TiempoTarea);
                RegistroMetricas.Instancia?.GuardarResumen(this, colisiones);
            }
        }

        /// <summary>Vuelve todo a la posición inicial para repetir la prueba.</summary>
        public void Reiniciar()
        {
            if (gripper) gripper.Soltar();
            foreach (var o in objetos) if (o) o.Reiniciar();
            if (zona) zona.Limpiar();
            if (robot) robot.Reiniciar();
            if (avatar) avatar.Reiniciar();
            if (colisiones) colisiones.Reiniciar();
            Iniciada = false;
            Completada = false;
            enZonaAntes = 0;
            RegistroMetricas.Evento("reinicio", "", 0);
        }
    }
}
