using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TeleoperacionRV.EditorTools
{
    /// <summary>
    /// Ajustes para compilar la app del celular (visor tipo Cardboard) y generar el APK.
    /// Menú "Teleoperación RV > 2. Ajustes de Android" y "3. Generar APK".
    /// </summary>
    public static class ConfiguracionAndroid
    {
        public const string Paquete = "com.umng.teleoperacionrv";
        const string RutaApk = "Builds/TeleoperacionRV.apk";

        [MenuItem("Teleoperación RV/2. Ajustes de Android (Cardboard)", priority = 2)]
        public static void Menu()
        {
            AplicarAjustes();
            EditorUtility.DisplayDialog("Ajustes de Android",
                "Listo:\n• Horizontal en ambos sentidos (gira con el celular)\n• Android 8.0 o superior\n• IL2CPP + ARM64\n• Paquete " + Paquete +
                "\n\nPara generar el APK usa 'Teleoperación RV > 3. Generar APK'.", "OK");
        }

        public static void AplicarAjustes()
        {
            PlayerSettings.companyName = "UMNG";
            PlayerSettings.productName = "Teleoperacion RV";
            // Horizontal en los dos sentidos: gira si el celular tiene activada la rotación automática
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            AjustarRotacionUsuario();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, Paquete);
            PlayerSettings.Android.forceInternetPermission = true;   // Wi-Fi para el modo espectador
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Unity_4_8);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Unity_4_8);
            AssetDatabase.SaveAssets();
        }

        // PlayerSettings.Android.autoRotationBehavior = User (respeta el bloqueo de rotación
        // del celular). Se pone por reflexión para no fallar si la versión de Unity no lo tiene.
        static void AjustarRotacionUsuario()
        {
            try
            {
                var prop = typeof(PlayerSettings.Android).GetProperty("autoRotationBehavior",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop == null) return;
                prop.SetValue(null, System.Enum.Parse(prop.PropertyType, "User"));
            }
            catch (System.Exception e) { Debug.LogWarning("autoRotationBehavior: " + e.Message); }
        }

        /// <summary>Genera el APK sin ventanas. Devuelve null si salió bien, o el error.</summary>
        public static string ConstruirApk(out float megas)
        {
            megas = 0f;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                return "no se pudo cambiar la plataforma a Android";
            AplicarAjustes();
            if (!File.Exists(ConstructorEscena.RutaEscena)) ConstructorEscena.ConstruirSinPreguntar();
            Directory.CreateDirectory(Path.GetDirectoryName(RutaApk));
            EditorUserBuildSettings.buildAppBundle = false;
            var opciones = new BuildPlayerOptions
            {
                scenes = new[] { ConstructorEscena.RutaEscena },
                locationPathName = RutaApk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };
            BuildReport reporte = BuildPipeline.BuildPlayer(opciones);
            megas = reporte.summary.totalSize / 1048576f;
            return reporte.summary.result == BuildResult.Succeeded ? null
                : "resultado " + reporte.summary.result + ", errores: " + reporte.summary.totalErrors;
        }

        const string RutaPC = "Builds/Espectador_PC/TeleoperacionRV_Espectador.exe";

        /// <summary>Genera el programa de Windows (modo espectador / simulación en PC). null = OK.</summary>
        public static string ConstruirPC(out float megas)
        {
            megas = 0f;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                return "falta el módulo de Windows en Unity Hub";
            AplicarAjustes();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            if (!File.Exists(ConstructorEscena.RutaEscena)) ConstructorEscena.ConstruirSinPreguntar();
            Directory.CreateDirectory(Path.GetDirectoryName(RutaPC));
            var opciones = new BuildPlayerOptions
            {
                scenes = new[] { ConstructorEscena.RutaEscena },
                locationPathName = RutaPC,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            BuildReport reporte = BuildPipeline.BuildPlayer(opciones);
            megas = reporte.summary.totalSize / 1048576f;
            return reporte.summary.result == BuildResult.Succeeded ? null
                : "resultado " + reporte.summary.result + ", errores: " + reporte.summary.totalErrors;
        }

        [MenuItem("Teleoperación RV/4. Generar programa espectador para PC", priority = 4)]
        public static void GenerarPC()
        {
            var r = ConstruirPC(out float mb);
            if (r == null) EditorUtility.RevealInFinder(RutaPC);
            else EditorUtility.DisplayDialog("Programa para PC", "No se pudo generar: " + r, "OK");
        }

        [MenuItem("Teleoperación RV/3. Generar APK para el celular", priority = 3)]
        public static void GenerarApk()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Falta el módulo de Android",
                    "Unity no tiene instalado 'Android Build Support'.\n\nAbre Unity Hub > Installs > (tu versión) > engranaje > Add modules > marca 'Android Build Support' (con OpenJDK y Android SDK & NDK) > Install.\nLuego vuelve a abrir el proyecto.",
                    "Entendido");
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (EditorUtility.DisplayDialog("Cambiar a Android",
                        "Primero hay que cambiar la plataforma del proyecto a Android (tarda unos minutos la primera vez).\n\nCuando termine, vuelve a usar 'Teleoperación RV > 3. Generar APK'.",
                        "Cambiar ahora", "Cancelar"))
                {
                    AplicarAjustes();
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                }
                return;
            }

            var r = ConstruirApk(out float mb);
            if (r == null)
            {
                EditorUtility.DisplayDialog("APK generado",
                    "Se creó " + RutaApk + " (" + mb.ToString("F1") + " MB).\n\n" +
                    "Cópialo al celular e instálalo (permite 'instalar apps de origen desconocido').",
                    "Abrir carpeta");
                EditorUtility.RevealInFinder(RutaApk);
            }
            else
            {
                EditorUtility.DisplayDialog("Error al generar el APK",
                    "Revisa la consola (Window > General > Console) para ver el detalle.", "OK");
            }
        }
    }
}
