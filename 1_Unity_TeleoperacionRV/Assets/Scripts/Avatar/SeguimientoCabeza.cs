using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// Orienta la "cabeza" del avatar:
    ///  - En el celular: con el giroscopio (visor tipo Cardboard, 3 grados de libertad).
    ///  - En el PC/Editor: con el ratón (mantener clic derecho y mover).
    /// Tocar la pantalla (o tecla R-Shift) recentra la vista hacia el frente.
    /// </summary>
    public class SeguimientoCabeza : MonoBehaviour
    {
        public float sensibilidadRaton = 3f;
        [Tooltip("Recentrar automáticamente al iniciar (s)")]
        public float recentrarAlInicio = 0.6f;

        bool usarGiroscopio;
        Quaternion correccion = Quaternion.identity;
        float yawRaton, pitchRaton;

        void Start()
        {
            usarGiroscopio = SystemInfo.supportsGyroscope && !Application.isEditor;
            if (usarGiroscopio)
            {
                Input.gyro.enabled = true;
                Input.gyro.updateInterval = 1f / 60f;
                Invoke(nameof(Recentrar), recentrarAlInicio);
            }
        }

        static Quaternion OrientacionGiroscopio()
        {
            // Convierte el sistema del giroscopio (mano derecha) al de Unity (mano izquierda),
            // para el celular en horizontal (Landscape Left).
            Quaternion q = Input.gyro.attitude;
            Quaternion r = Quaternion.Euler(90f, 0f, 0f) * new Quaternion(q.x, q.y, -q.z, -q.w);
            // Si la pantalla está girada al otro lado (Landscape Right) el celular
            // está volteado 180° sobre el eje de la vista: se compensa.
            if (Screen.orientation == ScreenOrientation.LandscapeRight)
                r *= Quaternion.Euler(0f, 0f, 180f);
            return r;
        }

        ScreenOrientation orientacionAnterior;

        void Update()
        {
            if (usarGiroscopio)
            {
                // Al girar la pantalla, se recentra la vista automáticamente
                if (Screen.orientation != orientacionAnterior)
                {
                    orientacionAnterior = Screen.orientation;
                    CancelInvoke(nameof(Recentrar));
                    Invoke(nameof(Recentrar), 0.3f);
                }
                transform.localRotation = correccion * OrientacionGiroscopio();
            }
            else
            {
                if (Input.GetMouseButton(1))
                {
                    yawRaton += Input.GetAxis("Mouse X") * sensibilidadRaton;
                    pitchRaton = Mathf.Clamp(pitchRaton - Input.GetAxis("Mouse Y") * sensibilidadRaton, -80f, 80f);
                }
                if (Input.GetKey(KeyCode.Q)) yawRaton -= 90f * Time.deltaTime;
                if (Input.GetKey(KeyCode.E)) yawRaton += 90f * Time.deltaTime;
                transform.localRotation = Quaternion.Euler(pitchRaton, yawRaton, 0f);
            }
        }

        /// <summary>Hace que la dirección a la que miras ahora sea "el frente".</summary>
        public void Recentrar()
        {
            if (usarGiroscopio)
            {
                float yaw = OrientacionGiroscopio().eulerAngles.y;
                correccion = Quaternion.Euler(0f, -yaw, 0f);
            }
            else
            {
                yawRaton = 0; pitchRaton = 0;
            }
        }
    }
}
