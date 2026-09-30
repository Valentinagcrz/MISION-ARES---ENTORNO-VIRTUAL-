using System.Collections;
using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>Objeto que el robot puede agarrar (rocas marcianas de muestra).</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Manipulable : MonoBehaviour
    {
        [Tooltip("Si cuenta para la misión de llevar objetos a la zona verde")]
        public bool cuentaParaMision = true;
        public Color colorResaltado = new Color(1f, 0.85f, 0.2f);
        [Tooltip("Nombre que se muestra en el HUD (ej. \"Hematita\")")]
        public string nombreMostrado = "";

        public string Nombre => string.IsNullOrEmpty(nombreMostrado) ? name : nombreMostrado;

        public bool Sostenido { get; private set; }
        public Rigidbody Cuerpo => rb;

        Rigidbody rb;
        Renderer rend;
        Color colorBase;
        Collider[] propios;
        Collider[] ignorados;
        Vector3 posInicial;
        Quaternion rotInicial;
        bool resaltado;
        RigidbodyInterpolation interpolacionOriginal;
        bool acomodando;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rend = GetComponentInChildren<Renderer>();
            if (rend) colorBase = rend.material.color;
            propios = GetComponentsInChildren<Collider>();
            posInicial = transform.position;
            rotInicial = transform.rotation;
        }

        public void Resaltar(bool activo)
        {
            if (resaltado == activo || rend == null) return;
            resaltado = activo;
            rend.material.color = activo ? colorResaltado : colorBase;
        }

        public void AlAgarrar(Transform puntoAgarre, Collider[] collidersRobot)
        {
            Sostenido = true;
            Resaltar(false);
            // Mientras está agarrado, el objeto va "pegado" a la pinza. Se apaga la
            // interpolación de su Rigidbody: si queda activa, Unity interpola la
            // posición vieja del objeto y este se ve adelantado/atrasado respecto al robot.
            interpolacionOriginal = rb.interpolation;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.isKinematic = true;
            ignorados = collidersRobot;
            IgnorarColisiones(true);
            transform.SetParent(puntoAgarre, true);
            StopAllCoroutines();
            StartCoroutine(Acomodar());
        }

        IEnumerator Acomodar()
        {
            // Desliza el objeto suavemente hasta quedar entre las pinzas
            acomodando = true;
            Vector3 p0 = transform.localPosition;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.2f)
            {
                if (!Sostenido) { acomodando = false; yield break; }
                transform.localPosition = Vector3.Lerp(p0, Vector3.zero, t);
                yield return null;
            }
            if (Sostenido) transform.localPosition = Vector3.zero;
            acomodando = false;
        }

        // Asegura que el objeto agarrado siga exactamente la pinza cada cuadro
        void LateUpdate()
        {
            if (Sostenido && !acomodando && transform.parent != null) transform.localPosition = Vector3.zero;
        }

        public void AlSoltar(Vector3 velocidadRobot)
        {
            StopAllCoroutines();
            Sostenido = false;
            acomodando = false;
            transform.SetParent(null, true);
            rb.isKinematic = false;
            rb.interpolation = interpolacionOriginal;
            rb.linearVelocity = velocidadRobot;
            StartCoroutine(RestaurarColisiones());
        }

        IEnumerator RestaurarColisiones()
        {
            yield return new WaitForSeconds(0.4f);
            IgnorarColisiones(false);
        }

        void IgnorarColisiones(bool ignorar)
        {
            if (ignorados == null) return;
            foreach (var a in propios)
                foreach (var b in ignorados)
                    if (a && b) Physics.IgnoreCollision(a, b, ignorar);
        }

        public void Reiniciar()
        {
            StopAllCoroutines();
            if (Sostenido) AlSoltar(Vector3.zero);
            IgnorarColisiones(false);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(posInicial, rotInicial);
        }
    }
}
