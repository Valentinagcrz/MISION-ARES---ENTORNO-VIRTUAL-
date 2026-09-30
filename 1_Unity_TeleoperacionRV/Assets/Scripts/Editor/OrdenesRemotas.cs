using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TeleoperacionRV.EditorTools
{
    /// <summary>
    /// Automatización sin clics: Unity revisa la carpeta "Ordenes" (junto a Assets)
    /// y ejecuta los archivos que aparezcan allí:
    ///   escena.txt   -> reconstruye la escena
    ///   captura.txt  -> guarda imágenes de la escena en Ordenes/Capturas
    ///   apk.txt      -> genera el APK en Builds/
    ///   pc.txt       -> genera el programa espectador de Windows en Builds/Espectador_PC
    /// El resultado se anota en Ordenes/resultado.txt.
    /// </summary>
    [InitializeOnLoad]
    static class OrdenesRemotas
    {
        const string Carpeta = "Ordenes";
        static double proxima;
        static bool ocupado;

        static OrdenesRemotas()
        {
            EditorApplication.update += Revisar;
        }

        static void Revisar()
        {
            if (ocupado) return;
            if (enviando && EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= proximoEnvio) EnviarPrueba();
            if (EditorApplication.timeSinceStartup < proxima) return;
            proxima = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!Directory.Exists(Carpeta)) return;
            if (!EditorApplication.isPlaying) enviando = false;

            string[] ordenes = EditorApplication.isPlaying ? new[] { "prueba_espejo", "pantalla", "detener" }
                             : EditorApplication.isPlayingOrWillChangePlaymode ? new string[0]
                             : new[] { "escena", "captura", "apk", "pc", "jugar" };
            foreach (var orden in ordenes)
            {
                string f = Path.Combine(Carpeta, orden + ".txt");
                if (!File.Exists(f)) continue;
                try { File.Delete(f); } catch (Exception) { continue; }
                ocupado = true;
                try { Anotar(orden + ": " + Ejecutar(orden)); }
                catch (Exception e) { Anotar(orden + ": EXCEPCION " + e); }
                finally { ocupado = false; }
                return;   // una orden por ciclo
            }
        }

        static string Ejecutar(string orden)
        {
            switch (orden)
            {
                case "escena":
                    ConstructorEscena.ConstruirSinPreguntar();
                    return "OK escena construida";
                case "captura":
                    return Capturas();
                case "apk":
                    var r = ConfiguracionAndroid.ConstruirApk(out float mb);
                    return r == null ? "OK APK " + mb.ToString("F1") + " MB" : "ERROR " + r;
                case "jugar":
                    EditorApplication.isPlaying = true;
                    return "OK entrando en Play";
                case "detener":
                    enviando = false;
                    EditorApplication.isPlaying = false;
                    return "OK Play detenido";
                case "prueba_espejo":
                    IniciarPrueba();
                    return "OK enviando datos de prueba a 127.0.0.1:" + PaqueteEspejo.Puerto;
                case "pantalla":
                    Directory.CreateDirectory(Path.Combine(Carpeta, "Capturas"));
                    string archivo = Path.Combine(Carpeta, "Capturas", "juego_" + DateTime.Now.ToString("HHmmss") + ".png");
                    ScreenCapture.CaptureScreenshot(archivo);
                    return "OK " + archivo;
                case "pc":
                    var r2 = ConfiguracionAndroid.ConstruirPC(out float mb2);
                    return r2 == null ? "OK PC " + mb2.ToString("F1") + " MB" : "ERROR " + r2;
            }
            return "orden desconocida";
        }

        // ------------------------------------------------------------------
        //  Emisor de prueba: simula al celular (rover dando vueltas) en el mismo PC
        static bool enviando;
        static double proximoEnvio, inicioPrueba;
        static System.Net.Sockets.UdpClient udpPrueba;
        static Vector3[] posObjetos;
        static int seqPrueba;

        static void IniciarPrueba()
        {
            udpPrueba ??= new System.Net.Sockets.UdpClient();
            var objs = PaqueteEspejo.ObjetosOrdenados();
            posObjetos = new Vector3[objs.Length];
            for (int i = 0; i < objs.Length; i++) posObjetos[i] = objs[i].transform.position;
            inicioPrueba = EditorApplication.timeSinceStartup;
            enviando = true;
        }

        static void EnviarPrueba()
        {
            proximoEnvio = EditorApplication.timeSinceStartup + 1.0 / 30.0;
            float t = (float)(EditorApplication.timeSinceStartup - inicioPrueba);
            if (t > 120f) { enviando = false; return; }
            var p = new PaqueteEspejo { secuencia = ++seqPrueba, tiempo = t };
            float a = t * 0.35f;
            p.robotPos = new Vector3(Mathf.Cos(a) * 3f, 0.02f, -2f + Mathf.Sin(a) * 3f);
            p.robotRot = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)));
            p.apertura = 0.21f; p.velLineal = 1.05f; p.velAngular = 20f;
            p.avatarPos = new Vector3(0f, 0.05f, -8.5f); p.avatarYaw = 0f;
            p.cabezaPos = p.avatarPos + Vector3.up * 1.65f;
            p.cabezaRot = Quaternion.LookRotation(p.robotPos + Vector3.up * 0.3f - p.cabezaPos);
            int n = posObjetos.Length;
            p.objPos = new Vector3[n]; p.objRot = new Quaternion[n]; p.objSostenido = new bool[n];
            for (int i = 0; i < n; i++) { p.objPos[i] = posObjetos[i]; p.objRot[i] = Quaternion.identity; }
            if (n > 0)
            {
                p.objPos[0] = p.robotPos + p.robotRot * new Vector3(0, 0.35f, 0.82f);
                p.objRot[0] = p.robotRot; p.objSostenido[0] = true; p.sostenidoNombre = "Hematita";
            }
            p.enZona = t > 8f ? 2 : 1; p.total = n; p.tiempoTarea = t; p.iniciada = true;
            p.conectado = true; p.pitch = 12f; p.roll = 4f; p.joyX = 0f; p.joyY = 0.3f; p.rtt = 24f; p.bateria = 87;
            p.ultimaVibracion = 1; p.edadVibracion = t % 3f;
            var d = p.Serializar();
            try { udpPrueba.Send(d, d.Length, "127.0.0.1", PaqueteEspejo.Puerto); } catch (Exception) { }
        }

        static void Anotar(string linea)
        {
            File.AppendAllText(Path.Combine(Carpeta, "resultado.txt"),
                DateTime.Now.ToString("HH:mm:ss") + " " + linea + Environment.NewLine, Encoding.UTF8);
            Debug.Log("[Ordenes] " + linea);
        }

        // ------------------------------------------------------------------
        static string Capturas()
        {
            if (EditorSceneManager.GetActiveScene().path != ConstructorEscena.RutaEscena)
                EditorSceneManager.OpenScene(ConstructorEscena.RutaEscena);
            string dir = Path.Combine(Carpeta, "Capturas");
            Directory.CreateDirectory(dir);

            var vistas = new (string nombre, Vector3 pos, Vector3 mira)[]
            {
                ("1_avatar", new Vector3(0f, 1.7f, -8.5f), new Vector3(0f, 0.8f, 0f)),
                ("2_rover", new Vector3(1.8f, 1.2f, -7.2f), new Vector3(0f, 0.3f, -5f)),
                ("3_base", new Vector3(3.5f, 2.2f, 3.5f), new Vector3(0f, 0.3f, 9f)),
                ("4_aerea", new Vector3(0f, 22f, -19f), new Vector3(0f, 0f, 1f)),
                ("5_horizonte", new Vector3(-6f, 1.7f, -8f), new Vector3(-30f, 3f, 20f)),
            };

            var hud = GameObject.Find("Avatar/Cabeza/HUD");
            var go = new GameObject("CamaraCaptura") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 180f;
            cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(1280, 720, 24);
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            int n = 0;
            try
            {
                foreach (var v in vistas)
                {
                    go.transform.position = v.pos;
                    go.transform.LookAt(v.mira);
                    Guardar(cam, rt, tex, Path.Combine(dir, v.nombre + ".png"));
                    n++;
                }
                // Vista con el HUD, desde la cabeza del avatar
                var cabeza = GameObject.Find("Avatar/Cabeza");
                if (cabeza)
                {
                    go.transform.SetPositionAndRotation(cabeza.transform.position, cabeza.transform.rotation);
                    var hudT = GameObject.Find("Avatar/Cabeza/HUD")?.GetComponent<TextMesh>();
                    if (hudT && hudT.text == "HUD")
                        hudT.text = "<color=#ffa552>== MISIÓN ARES · ROVER UMNG-1 ==</color>\nRocas en la base  <color=#ffe066>[#][#][ ][ ][ ]</color>  2/5     T+ 35.2 s\nPinza: <color=#ffe066>Hematita asegurada</color>\n<color=#7dff8a>ENLACE OK</color>  <color=#d9c2b0>BT: ControlHaptico_RV</color>\n<color=#6fe3ff>v 0.52 m/s   ω 12°/s   RTT 24 ms   Bat 87%\nPitch 5.1°  Roll -3.0°  Joy 0.00, 0.20</color>";
                    var avisoT = GameObject.Find("Avatar/Cabeza/Aviso")?.GetComponent<TextMesh>();
                    if (avisoT) avisoT.text = "<color=#7dff8a>¡ROCA ASEGURADA!</color>\n<size=44>2 de 5 en la base</size>";
                    Guardar(cam, rt, tex, Path.Combine(dir, "6_vista_con_hud.png"));
                    if (hudT) hudT.text = "HUD";
                    if (avisoT) avisoT.text = "";
                    n++;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
            }
            return "OK " + n + " capturas";
        }

        static void Guardar(Camera cam, RenderTexture rt, Texture2D tex, string ruta)
        {
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
        }
    }
}
