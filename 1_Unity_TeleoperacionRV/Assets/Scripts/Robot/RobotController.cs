using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// Robot móvil diferencial controlado por la inclinación de la mano:
    ///   Pitch (adelante/atrás)  -> velocidad lineal  v
    ///   Roll  (izquierda/derecha) -> velocidad angular ω
    ///
    /// Mapeo (modo proporcional), para cada eje:
    ///   n = clamp01( (|θ| - θ_muerta) / (θ_max - θ_muerta) )
    ///   u = signo(θ) · n^γ              (γ = exponente de la curva)
    ///   v* = u_pitch · v_max ;  ω* = u_roll · ω_max
    /// Suavizado de primer orden (constante de tiempo τ):
    ///   v[k] = v[k-1] + (v* - v[k-1]) · (1 - e^(-Δt/τ))
    /// En modo "Umbrales" u vale -1, 0 ó +1 (control todo/nada) para comparar.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RobotController : MonoBehaviour
    {
        public enum ModoControl { Proporcional, Umbrales }

        [Header("Modo")]
        public ModoControl modo = ModoControl.Proporcional;

        [Header("Pitch -> avance")]
        [Tooltip("Ángulos menores a este valor se ignoran (grados)")]
        public float zonaMuertaPitch = 6f;
        [Tooltip("Ángulo con el que se alcanza la velocidad máxima (grados)")]
        public float pitchMaximo = 30f;
        public float velocidadMaxima = 1.8f;          // m/s
        public bool invertirPitch = false;

        [Header("Roll -> giro")]
        public float zonaMuertaRoll = 6f;
        public float rollMaximo = 30f;
        public float velocidadGiroMaxima = 90f;       // °/s
        public bool invertirRoll = false;

        [Header("Curva y suavizado")]
        [Range(1f, 3f)] public float exponente = 1.5f;
        [Tooltip("Constante de tiempo del filtro (s). Mayor = más suave pero más lento")]
        public float constanteTiempo = 0.15f;

        [Header("Ruedas (solo visual)")]
        public Transform[] ruedasIzquierdas;
        public Transform[] ruedasDerechas;
        public float radioRueda = 0.15f;
        public float anchoVia = 0.7f;

        public float ComandoLineal { get; private set; }     // -1..1
        public float ComandoAngular { get; private set; }    // -1..1
        public float VelocidadLineal { get; private set; }   // m/s
        public float VelocidadAngular { get; private set; }  // °/s
        public bool Habilitado { get; set; } = true;

        Rigidbody rb;
        Vector3 posInicial;
        Quaternion rotInicial;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            posInicial = transform.position;
            rotInicial = transform.rotation;
        }

        /// <summary>Mapeo de un ángulo a un comando normalizado -1..1.</summary>
        public static float Mapear(float angulo, float zonaMuerta, float maximo, float exponente, bool proporcional)
        {
            float a = Mathf.Abs(angulo);
            if (a < zonaMuerta) return 0f;
            if (!proporcional) return Mathf.Sign(angulo);
            float n = Mathf.Clamp01((a - zonaMuerta) / Mathf.Max(0.01f, maximo - zonaMuerta));
            return Mathf.Sign(angulo) * Mathf.Pow(n, exponente);
        }

        void FixedUpdate()
        {
            var enlace = EnlaceHaptico.Instancia;
            float pitch = enlace != null ? enlace.Estado.pitch : 0f;
            float roll = enlace != null ? enlace.Estado.roll : 0f;
            bool prop = modo == ModoControl.Proporcional;

            float u = Mapear(pitch, zonaMuertaPitch, pitchMaximo, exponente, prop) * (invertirPitch ? -1f : 1f);
            float w = Mapear(roll, zonaMuertaRoll, rollMaximo, exponente, prop) * (invertirRoll ? -1f : 1f);
            if (!Habilitado) { u = 0; w = 0; }
            ComandoLineal = u;
            ComandoAngular = w;

            float k = 1f - Mathf.Exp(-Time.fixedDeltaTime / Mathf.Max(0.001f, constanteTiempo));
            VelocidadLineal += (u * velocidadMaxima - VelocidadLineal) * k;
            VelocidadAngular += (w * velocidadGiroMaxima - VelocidadAngular) * k;

            Vector3 v = transform.forward * VelocidadLineal;
            v.y = rb.linearVelocity.y;                    // conserva la gravedad
            rb.linearVelocity = v;
            rb.angularVelocity = Vector3.zero;
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, VelocidadAngular * Time.fixedDeltaTime, 0f));
        }

        void Update()
        {
            // Giro visual de las ruedas (cinemática diferencial)
            float wRad = VelocidadAngular * Mathf.Deg2Rad;
            float vIzq = VelocidadLineal + wRad * anchoVia * 0.5f;
            float vDer = VelocidadLineal - wRad * anchoVia * 0.5f;
            GirarRuedas(ruedasIzquierdas, vIzq);
            GirarRuedas(ruedasDerechas, vDer);
        }

        void GirarRuedas(Transform[] ruedas, float v)
        {
            if (ruedas == null) return;
            float grados = v / Mathf.Max(0.01f, radioRueda) * Mathf.Rad2Deg * Time.deltaTime;
            foreach (var r in ruedas) if (r) r.Rotate(Vector3.up, grados, Space.Self);
        }

        public void Reiniciar()
        {
            VelocidadLineal = 0; VelocidadAngular = 0;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = posInicial;
            rb.rotation = rotInicial;
            transform.SetPositionAndRotation(posInicial, rotInicial);
        }
    }
}
