using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// Detecta los choques del robot y ordena la vibración correspondiente:
    ///   - contra un objeto manipulable  -> patrón OBJETO (toques suaves)
    ///   - contra paredes / obstáculos    -> patrón PARED  (zumbido fuerte)
    /// La intensidad se escala con la velocidad del impacto.
    /// </summary>
    public class RobotHapticaColisiones : MonoBehaviour
    {
        public string etiquetaLimite = "Limite";
        public string etiquetaObstaculo = "Obstaculo";
        [Tooltip("Tiempo mínimo entre vibraciones del mismo tipo (s)")]
        public float enfriamiento = 0.3f;
        [Tooltip("Velocidad de impacto (m/s) que da la intensidad mínima y la máxima")]
        public Vector2 rangoImpacto = new Vector2(0.15f, 2.0f);
        [Range(0, 255)] public int intensidadMinima = 200;

        public int ColisionesObjeto { get; private set; }
        public int ColisionesPared { get; private set; }

        float tObjeto = -99f, tPared = -99f;
        RobotGripper gripper;

        void Awake()
        {
            gripper = GetComponent<RobotGripper>();
        }

        void OnCollisionEnter(Collision c)
        {
            var col = c.collider;
            float impacto = c.relativeVelocity.magnitude;
            int intensidad = Mathf.RoundToInt(Mathf.Lerp(intensidadMinima, 255,
                Mathf.InverseLerp(rangoImpacto.x, rangoImpacto.y, impacto)));

            var m = col.attachedRigidbody ? col.attachedRigidbody.GetComponent<Manipulable>() : null;
            if (m != null)
            {
                if (gripper && gripper.Sostenido == m) return;
                if (Time.time - tObjeto < enfriamiento) return;
                tObjeto = Time.time;
                ColisionesObjeto++;
                EnlaceHaptico.Instancia?.Vibrar(PatronVibracion.Objeto, intensidad);
                RegistroMetricas.Evento("colision_objeto", m.name, impacto);
            }
            else if (col.CompareTag(etiquetaLimite) || col.CompareTag(etiquetaObstaculo))
            {
                if (Time.time - tPared < enfriamiento) return;
                tPared = Time.time;
                ColisionesPared++;
                EnlaceHaptico.Instancia?.Vibrar(PatronVibracion.Pared, intensidad);
                RegistroMetricas.Evento(col.CompareTag(etiquetaLimite) ? "colision_limite" : "colision_obstaculo", col.name, impacto);
            }
        }

        public void Reiniciar()
        {
            ColisionesObjeto = 0;
            ColisionesPared = 0;
        }
    }
}
