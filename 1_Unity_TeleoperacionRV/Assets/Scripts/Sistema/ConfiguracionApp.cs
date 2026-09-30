using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// Ajustes generales y atajos:
    ///   Celular: 1 toque = recentrar vista · 2 dedos = calibrar control · 3 dedos = HUD
    ///            4 dedos = voltear la imagen 180° (si la rotación automática está apagada)
    ///            botón "atrás" = salir
    ///   PC:      R = reiniciar prueba · C = calibrar control · H = HUD · M = estéreo/mono
    ///            Shift derecho = recentrar vista · Esc = salir
    /// </summary>
    public class ConfiguracionApp : MonoBehaviour
    {
        public SeguimientoCabeza cabeza;
        public HUDVR hud;
        public MisionTarea mision;
        public int fpsObjetivo = 60;

        float tToque;
        int maxDedos;

        void Awake()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.targetFrameRate = fpsObjetivo;
            QualitySettings.vSyncCount = 0;
            if (Application.platform == RuntimePlatform.Android)
                PermitirGiro();
        }

        // La app gira entre las dos posiciones horizontales (nunca vertical), según
        // el celular, si la rotación automática del sistema está activada.
        void PermitirGiro()
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            girada = false;
        }

        bool girada;

        /// <summary>Voltea la imagen 180° a mano (por si la rotación automática está apagada).</summary>
        public void VoltearPantalla()
        {
            if (Application.platform != RuntimePlatform.Android) return;
            if (girada) { PermitirGiro(); return; }
            Screen.orientation = Screen.orientation == ScreenOrientation.LandscapeRight
                ? ScreenOrientation.LandscapeLeft : ScreenOrientation.LandscapeRight;
            girada = true;
        }

        void Update()
        {
            // --- Teclado ---
            if (Input.GetKeyDown(KeyCode.R) && mision) mision.Reiniciar();
            if (Input.GetKeyDown(KeyCode.C)) EnlaceHaptico.Instancia?.CalibrarControl();
            if (Input.GetKeyDown(KeyCode.RightShift) && cabeza) cabeza.Recentrar();
            if (Input.GetKeyDown(KeyCode.Escape)) Salir();

            // --- Pantalla táctil: se decide al levantar todos los dedos ---
            if (Input.touchCount > 0)
            {
                if (maxDedos == 0) tToque = Time.time;
                maxDedos = Mathf.Max(maxDedos, Input.touchCount);
            }
            else if (maxDedos > 0)
            {
                if (Time.time - tToque < 0.6f)
                {
                    if (maxDedos == 1 && cabeza) cabeza.Recentrar();
                    else if (maxDedos == 2) EnlaceHaptico.Instancia?.CalibrarControl();
                    else if (maxDedos == 3 && hud) hud.Alternar();
                    else if (maxDedos >= 4) VoltearPantalla();
                }
                else if (maxDedos == 1 && mision && Time.time - tToque > 1.5f)
                {
                    mision.Reiniciar();       // toque largo (>1.5 s) = reiniciar la prueba
                }
                maxDedos = 0;
            }
        }

        void Salir()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
