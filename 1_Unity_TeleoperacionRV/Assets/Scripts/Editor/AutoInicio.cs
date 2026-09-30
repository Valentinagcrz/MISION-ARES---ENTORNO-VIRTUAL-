using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TeleoperacionRV.EditorTools
{
    /// <summary>
    /// La primera vez que se abre el proyecto construye la escena automáticamente
    /// y la deja abierta, para que solo haya que presionar Play.
    /// </summary>
    [InitializeOnLoad]
    static class AutoInicio
    {
        const string Clave = "TeleoperacionRV_EscenaAbierta";

        static AutoInicio()
        {
            EditorApplication.delayCall += Revisar;
        }

        static void Revisar()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

            if (!File.Exists(ConstructorEscena.RutaEscena))
            {
                ConfiguracionAndroid.AplicarAjustes();
                ConstructorEscena.ConstruirSinPreguntar();
                SessionState.SetBool(Clave, true);
                return;
            }

            if (!SessionState.GetBool(Clave, false))
            {
                SessionState.SetBool(Clave, true);
                var activa = EditorSceneManager.GetActiveScene();
                if (activa.path != ConstructorEscena.RutaEscena && string.IsNullOrEmpty(activa.path) && !activa.isDirty)
                    EditorSceneManager.OpenScene(ConstructorEscena.RutaEscena);
            }
        }
    }
}
