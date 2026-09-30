using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// Visualización estereoscópica para visor tipo Cardboard: dos cámaras (ojo
    /// izquierdo y derecho) separadas por la distancia interpupilar y dibujadas cada
    /// una en media pantalla. En el Editor se usa una sola cámara (tecla M alterna).
    /// Opcionalmente aplica una corrección de distorsión de barril para compensar
    /// las lentes del visor.
    /// </summary>
    public class CamaraEstereoCardboard : MonoBehaviour
    {
        public Camera ojoIzquierdo;
        public Camera ojoDerecho;
        public Camera camaraMono;

        [Header("Estéreo")]
        public bool estereoEnCelular = true;
        public bool estereoEnEditor = false;
        [Tooltip("Distancia entre los ojos (m). Promedio adulto: 0.063")]
        public float distanciaInterpupilar = 0.064f;
        [Tooltip("Campo de visión vertical de cada ojo (grados)")]
        public float campoVisualOjo = 85f;
        public float campoVisualMono = 70f;

        [Header("Corrección de lente (barril)")]
        public bool corregirLente = true;
        [Range(0f, 0.6f)] public float k1 = 0.22f;
        [Range(0f, 0.6f)] public float k2 = 0.08f;

        public bool Estereo { get; private set; }

        void Start()
        {
            Estereo = Application.isEditor ? estereoEnEditor : estereoEnCelular;
            Aplicar();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.M)) { Estereo = !Estereo; Aplicar(); }
        }

        public void Aplicar()
        {
            if (ojoIzquierdo)
            {
                ojoIzquierdo.enabled = Estereo;
                ojoIzquierdo.rect = new Rect(0f, 0f, 0.5f, 1f);
                ojoIzquierdo.fieldOfView = campoVisualOjo;
                ojoIzquierdo.transform.localPosition = new Vector3(-distanciaInterpupilar * 0.5f, 0f, 0f);
                ConfigurarDistorsion(ojoIzquierdo);
            }
            if (ojoDerecho)
            {
                ojoDerecho.enabled = Estereo;
                ojoDerecho.rect = new Rect(0.5f, 0f, 0.5f, 1f);
                ojoDerecho.fieldOfView = campoVisualOjo;
                ojoDerecho.transform.localPosition = new Vector3(distanciaInterpupilar * 0.5f, 0f, 0f);
                ConfigurarDistorsion(ojoDerecho);
            }
            if (camaraMono)
            {
                camaraMono.enabled = !Estereo;
                camaraMono.fieldOfView = campoVisualMono;
            }
        }

        void ConfigurarDistorsion(Camera c)
        {
            var d = c.GetComponent<DistorsionLente>();
            if (!d) return;
            d.enabled = Estereo && corregirLente;
            d.k1 = k1;
            d.k2 = k2;
        }
    }
}
