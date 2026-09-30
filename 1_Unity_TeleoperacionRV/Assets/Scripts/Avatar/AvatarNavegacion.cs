using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// Navegación del avatar en primera persona: avanza o retrocede en la dirección
    /// en la que se mira (solo en el plano horizontal) con el eje Y del joystick del
    /// control háptico, de forma proporcional (o con las flechas ↑ ↓ del teclado).
    /// El eje X del joystick (o ← →) gira el cuerpo del avatar, útil si el usuario
    /// está sentado y no puede darse la vuelta físicamente. Las paredes lo detienen gracias al
    /// CharacterController y, al chocar con un límite, el control vibra.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class AvatarNavegacion : MonoBehaviour
    {
        public Transform cabeza;
        public float velocidad = 1.6f;
        [Tooltip("Valores del joystick menores a esto se ignoran (0..1)")]
        public float zonaMuertaJoystick = 0.1f;
        [Tooltip("Velocidad de giro del avatar con el eje X del joystick (°/s)")]
        public float velocidadGiro = 90f;
        public float gravedad = -9.81f;
        public bool vibrarAlChocarConLimites = true;
        public string etiquetaLimite = "Limite";

        CharacterController cc;
        float velVertical;
        float tVibracion = -99f;
        float eje;
        Vector3 posInicial;
        Quaternion rotInicial;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            posInicial = transform.position;
            rotInicial = transform.rotation;
        }

        void Update()
        {
            eje = 0f;
            var e = EnlaceHaptico.Instancia;
            if (e != null)
            {
                float j = e.Estado.joystickY;
                eje = Mathf.Abs(j) < zonaMuertaJoystick ? 0f : j;
                float g = e.Estado.joystickX;
                if (Mathf.Abs(g) >= zonaMuertaJoystick)
                    transform.Rotate(0f, g * velocidadGiro * Time.deltaTime, 0f, Space.World);
            }
            eje = Mathf.Clamp(eje, -1f, 1f);

            Vector3 dir = cabeza ? cabeza.forward : transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) dir.Normalize();

            velVertical = cc.isGrounded ? -1f : velVertical + gravedad * Time.deltaTime;
            cc.Move((dir * eje * velocidad + Vector3.up * velVertical) * Time.deltaTime);
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!vibrarAlChocarConLimites || Mathf.Approximately(eje, 0f)) return;
            if (!hit.collider.CompareTag(etiquetaLimite)) return;
            if (Time.time - tVibracion < 1f) return;
            tVibracion = Time.time;
            EnlaceHaptico.Instancia?.Vibrar(PatronVibracion.Pared, 230);
            RegistroMetricas.Evento("avatar_limite", hit.collider.name, velocidad);
        }

        public void Reiniciar()
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(posInicial, rotInicial);
            cc.enabled = true;
        }
    }
}
