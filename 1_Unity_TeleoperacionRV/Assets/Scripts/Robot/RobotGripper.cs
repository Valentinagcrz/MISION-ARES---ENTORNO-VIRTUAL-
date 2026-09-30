using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// Sistema de agarre (attach / detach) de la pinza del robot.
    /// Por defecto el botón funciona como interruptor: un toque agarra, otro suelta.
    /// </summary>
    public class RobotGripper : MonoBehaviour
    {
        public Transform puntoAgarre;
        public GripperZona zona;
        [Tooltip("true = hay que mantener el botón presionado para sostener")]
        public bool mantenerPresionado = false;

        [Header("Pinzas (visual)")]
        public Transform dedoIzquierdo;
        public Transform dedoDerecho;
        public float aperturaAbierta = 0.22f;
        public float aperturaCerrada = 0.12f;

        public Manipulable Sostenido { get; private set; }
        public int AgarresRealizados { get; private set; }

        Rigidbody rb;
        Collider[] collidersRobot;
        float apertura;

        /// <summary>Separación actual de los dedos (m). La usa el modo espectador.</summary>
        public float Apertura => apertura;

        /// <summary>Pone los dedos en una apertura dada (modo espectador, sin lógica de agarre).</summary>
        public void FijarApertura(float a)
        {
            apertura = a;
            if (dedoIzquierdo) { var p = dedoIzquierdo.localPosition; p.x = -a; dedoIzquierdo.localPosition = p; }
            if (dedoDerecho) { var p = dedoDerecho.localPosition; p.x = a; dedoDerecho.localPosition = p; }
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            collidersRobot = GetComponentsInChildren<Collider>();
            apertura = aperturaAbierta;
        }

        void OnEnable()
        {
            if (EnlaceHaptico.Instancia) Suscribir();
        }

        void Start()
        {
            Suscribir();
        }

        bool suscrito;
        void Suscribir()
        {
            if (suscrito || EnlaceHaptico.Instancia == null) return;
            EnlaceHaptico.Instancia.AlPresionarAgarre += Presionado;
            EnlaceHaptico.Instancia.AlSoltarAgarre += Liberado;
            suscrito = true;
        }

        void OnDisable()
        {
            if (!suscrito || EnlaceHaptico.Instancia == null) return;
            EnlaceHaptico.Instancia.AlPresionarAgarre -= Presionado;
            EnlaceHaptico.Instancia.AlSoltarAgarre -= Liberado;
            suscrito = false;
        }

        void Presionado()
        {
            if (mantenerPresionado) { if (!Sostenido) Agarrar(); }
            else if (Sostenido) Soltar();
            else Agarrar();
        }

        void Liberado()
        {
            if (mantenerPresionado && Sostenido) Soltar();
        }

        public void Agarrar()
        {
            if (zona == null || puntoAgarre == null) return;
            var m = zona.MasCercano(puntoAgarre.position);
            if (m == null) return;
            Sostenido = m;
            AgarresRealizados++;
            m.AlAgarrar(puntoAgarre, collidersRobot);
            EnlaceHaptico.Instancia?.Vibrar(PatronVibracion.Agarre, 255);
            RegistroMetricas.Evento("agarre", m.name, 0);
        }

        public void Soltar()
        {
            if (Sostenido == null) return;
            var m = Sostenido;
            Sostenido = null;
            m.AlSoltar(rb ? rb.linearVelocity : Vector3.zero);
            RegistroMetricas.Evento("soltar", m.name, 0);
        }

        void Update()
        {
            if (zona && puntoAgarre) zona.ActualizarResaltado(puntoAgarre.position, Sostenido == null);

            float objetivo = Sostenido ? aperturaCerrada : aperturaAbierta;
            apertura = Mathf.MoveTowards(apertura, objetivo, 0.8f * Time.deltaTime);
            if (dedoIzquierdo) { var p = dedoIzquierdo.localPosition; p.x = -apertura; dedoIzquierdo.localPosition = p; }
            if (dedoDerecho) { var p = dedoDerecho.localPosition; p.x = apertura; dedoDerecho.localPosition = p; }
        }
    }
}
