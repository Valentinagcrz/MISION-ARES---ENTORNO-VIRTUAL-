using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TeleoperacionRV.EditorTools
{
    /// <summary>
    /// Construye la escena completa del proyecto con un clic
    /// (menú "Teleoperación RV > 1. Construir escena"): la "Misión Ares" en Marte.
    /// Área de exploración de 16 x 20 m delimitada (paredes invisibles + cordón de
    /// rocas y balizas), rocas grandes como obstáculos, rocas marcianas de muestra
    /// para recoger, la base de recolección (zona objetivo), un hábitat de fondo,
    /// el rover con pinza, el avatar en primera persona con cámaras estéreo y el
    /// sistema de comunicación/métricas. Después se puede editar libremente en Unity.
    /// </summary>
    public static class ConstructorEscena
    {
        public const string RutaEscena = "Assets/Scenes/Teleoperacion.unity";
        const string CarpetaMat = "Assets/Materiales";

        // Dimensiones del cuarto (m)
        const float Ancho = 16f;     // eje X
        const float Largo = 20f;     // eje Z
        const float Alto = 3.6f;
        const float Grosor = 0.4f;

        [MenuItem("Teleoperación RV/1. Construir escena", priority = 1)]
        public static void Menu()
        {
            if (File.Exists(RutaEscena) &&
                !EditorUtility.DisplayDialog("Construir escena",
                    "Esto vuelve a crear la escena 'Teleoperacion' desde cero y se pierden los cambios que le hayas hecho a mano. ¿Continuar?",
                    "Sí, reconstruir", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ConstruirSinPreguntar();
        }

        public static void ConstruirSinPreguntar()
        {
            AsegurarEtiquetas("Limite", "Obstaculo", "Manipulable", "Robot");
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory(CarpetaMat + "/Texturas");
            AssetDatabase.Refresh();

            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rnd = new System.Random(42);
            float Azar(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            // ---------------- Materiales ----------------
            var texArena = GeneradorMarte.TexturaArena();
            var mSuelo = Mat("Marte_Suelo", Color.white, 0f, 0.08f);
            mSuelo.mainTexture = texArena;
            var mCrater = Mat("Marte_Crater", new Color(0.78f, 0.7f, 0.66f), 0f, 0.05f);
            mCrater.mainTexture = texArena;
            var mRocaA = Mat("Roca_Rojiza", new Color(0.55f, 0.27f, 0.16f), 0f, 0.2f);
            var mRocaB = Mat("Roca_Oscura", new Color(0.33f, 0.2f, 0.15f), 0.05f, 0.25f);
            var mRocaC = Mat("Roca_Clara", new Color(0.72f, 0.45f, 0.3f), 0f, 0.15f);
            var mInvisible = Mat("Limite_Invisible", Color.clear, 0f, 0f);
            var mBaliza = Mat("Baliza_Naranja", new Color(1f, 0.45f, 0.1f), 0f, 0.6f, new Color(2.2f, 0.8f, 0.15f));
            var mPoste = Mat("Poste_Metal", new Color(0.35f, 0.35f, 0.38f), 0.7f, 0.5f);
            var mPlataforma = Mat("Base_Plataforma", new Color(0.22f, 0.24f, 0.28f), 0.6f, 0.55f);
            var mLuzBase = Mat("Base_Luz_Cian", new Color(0.3f, 0.9f, 1f), 0f, 0.8f, new Color(0.35f, 1.6f, 2.2f));
            var mZona = MatTransparente("Zona_Objetivo", new Color(0.25f, 0.85f, 1f, 0.18f));
            var mBlanco = Mat("Robot_Blanco", new Color(0.93f, 0.92f, 0.9f), 0.2f, 0.6f);
            var mOscuro = Mat("Robot_Oscuro", new Color(0.12f, 0.12f, 0.14f), 0.4f, 0.5f);
            var mNaranja = Mat("Robot_Naranja", new Color(1f, 0.5f, 0.05f), 0.1f, 0.5f);
            var mDorado = Mat("Robot_Dorado", new Color(0.9f, 0.68f, 0.25f), 0.85f, 0.7f);
            var mPanel = Mat("Panel_Solar", new Color(0.06f, 0.09f, 0.28f), 0.7f, 0.9f, new Color(0.01f, 0.02f, 0.08f));
            var mLente = Mat("Robot_Lente", new Color(0.05f, 0.1f, 0.2f), 0.9f, 0.95f, new Color(0.1f, 0.4f, 0.8f));
            var mHabitat = Mat("Habitat_Blanco", new Color(0.9f, 0.9f, 0.88f), 0.1f, 0.45f);
            var mVentana = Mat("Habitat_Ventana", new Color(0.1f, 0.15f, 0.2f), 0.8f, 0.9f, new Color(0.9f, 0.7f, 0.4f));
            AssetDatabase.SaveAssets();

            // ---------------- Cielo, niebla y sol ----------------
            var cielo = CieloMarte();
            RenderSettings.skybox = cielo;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.58f, 0.46f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.43f, 0.33f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.22f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.8f, 0.55f, 0.4f);
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 150f;
            var sol = new GameObject("Sol").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.intensity = 1.15f;
            sol.color = new Color(1f, 0.88f, 0.74f);
            sol.shadows = LightShadows.Hard;
            sol.shadowStrength = 0.75f;
            sol.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            RenderSettings.sun = sol;
            cielo.SetVector("_DirSol", -sol.transform.forward);
            EditorUtility.SetDirty(cielo);
            QualitySettings.shadowDistance = 30f;

            // ---------------- Terreno ----------------
            var entorno = new GameObject("Entorno_Marte").transform;
            var terreno = new GameObject("Terreno");
            terreno.transform.SetParent(entorno, false);
            var mallaTerreno = GeneradorMarte.Terreno();
            terreno.AddComponent<MeshFilter>().sharedMesh = mallaTerreno;
            terreno.AddComponent<MeshRenderer>().sharedMaterial = mSuelo;
            terreno.AddComponent<MeshCollider>().sharedMesh = mallaTerreno;
            terreno.isStatic = true;

            // Mallas de roca reutilizables
            var rocas = new Mesh[8];
            for (int i = 0; i < rocas.Length; i++)
                rocas[i] = GeneradorMarte.Roca("Roca_" + i, 100 + i, 0.35f + 0.05f * (i % 3), new Vector3(1f, 0.8f + 0.1f * (i % 3), 1f));
            Material[] matsRoca = { mRocaA, mRocaB, mRocaC };

            // ---------------- Límites: pared invisible + cordón de rocas + balizas ----------------
            var paredes = new GameObject("Limites_Fisicos").transform;
            paredes.SetParent(entorno, false);
            Pared("Limite_Norte", paredes, new Vector3(0, Alto / 2, Largo / 2 + Grosor / 2), new Vector3(Ancho + 2 * Grosor, Alto, Grosor), mInvisible);
            Pared("Limite_Sur", paredes, new Vector3(0, Alto / 2, -Largo / 2 - Grosor / 2), new Vector3(Ancho + 2 * Grosor, Alto, Grosor), mInvisible);
            Pared("Limite_Este", paredes, new Vector3(Ancho / 2 + Grosor / 2, Alto / 2, 0), new Vector3(Grosor, Alto, Largo), mInvisible);
            Pared("Limite_Oeste", paredes, new Vector3(-Ancho / 2 - Grosor / 2, Alto / 2, 0), new Vector3(Grosor, Alto, Largo), mInvisible);
            foreach (var r in paredes.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;   // solo colisión

            var cordon = new GameObject("Cordon_Rocas").transform;
            cordon.SetParent(paredes, false);
            void Borde(Vector3 a, Vector3 b, Vector3 afuera)
            {
                float largo = Vector3.Distance(a, b);
                int n = Mathf.CeilToInt(largo / 1.25f);
                for (int i = 0; i <= n; i++)
                {
                    var p = Vector3.Lerp(a, b, i / (float)n) + afuera * Azar(0.45f, 0.75f);
                    float w = Azar(0.9f, 1.5f), h = Azar(0.55f, 1.2f);
                    Roca("Roca_Borde", cordon, rocas[rnd.Next(rocas.Length)], matsRoca[rnd.Next(3)],
                        new Vector3(p.x, h * 0.2f, p.z), new Vector3(w, h, Azar(0.9f, 1.4f)), Azar(0, 360), false);
                }
            }
            float ax = Ancho / 2, az = Largo / 2;
            Borde(new Vector3(-ax, 0, az), new Vector3(ax, 0, az), Vector3.forward);
            Borde(new Vector3(-ax, 0, -az), new Vector3(ax, 0, -az), Vector3.back);
            Borde(new Vector3(ax, 0, -az), new Vector3(ax, 0, az), Vector3.right);
            Borde(new Vector3(-ax, 0, -az), new Vector3(-ax, 0, az), Vector3.left);

            var balizas = new GameObject("Balizas").transform;
            balizas.SetParent(paredes, false);
            foreach (var p in new[] { new Vector3(-ax, 0, -az), new Vector3(ax, 0, -az), new Vector3(-ax, 0, az), new Vector3(ax, 0, az),
                                      new Vector3(-ax, 0, 0), new Vector3(ax, 0, 0), new Vector3(0, 0, -az) })
            {
                var q = p + new Vector3(Mathf.Sign(p.x) * 0.2f * (p.x != 0 ? 1 : 0), 0, Mathf.Sign(p.z) * 0.2f * (p.z != 0 ? 1 : 0));
                Prim(PrimitiveType.Cylinder, "Poste_Baliza", balizas, q + Vector3.up * 0.8f, new Vector3(0.07f, 0.8f, 0.07f), mPoste, false);
                Prim(PrimitiveType.Sphere, "Luz_Baliza", balizas, q + Vector3.up * 1.65f, Vector3.one * 0.16f, mBaliza, false);
            }

            // ---------------- Obstáculos: rocas grandes ----------------
            var obst = new GameObject("Obstaculos_Rocas").transform;
            obst.SetParent(entorno, false);
            Vector3[] posObst =
            {
                new Vector3(-3.5f, 0, -2f), new Vector3(3.5f, 0, -2f),
                new Vector3(-3.5f, 0, 3f),  new Vector3(3.5f, 0, 3f),
                new Vector3(-1.2f, 0, 1f),  new Vector3(1.8f, 0, 5.5f),
            };
            for (int i = 0; i < posObst.Length; i++)
            {
                float h = i % 2 == 0 ? 2.2f : 1.5f;
                var o = Roca("Roca_Obstaculo_" + (i + 1), obst, rocas[i % rocas.Length], matsRoca[i % 3],
                    posObst[i] + Vector3.up * h * 0.22f, new Vector3(1.2f, h, 1.1f), i * 47f, true);
                o.tag = "Obstaculo";
            }
            var cresta1 = Roca("Cresta_Rocosa_1", obst, rocas[6], mRocaB, new Vector3(-5.2f, 0.12f, -6f), new Vector3(2.6f, 0.75f, 0.7f), 0f, true);
            cresta1.tag = "Obstaculo";
            var cresta2 = Roca("Cresta_Rocosa_2", obst, rocas[7], mRocaA, new Vector3(5.2f, 0.12f, 7f), new Vector3(2.6f, 0.75f, 0.7f), 0f, true);
            cresta2.tag = "Obstaculo";

            // Cráteres (decorativos, sin colisión)
            var crateres = new GameObject("Crateres").transform;
            crateres.SetParent(entorno, false);
            Decorado("Crater_1", crateres, GeneradorMarte.Crater("Crater_1", 1.2f, 1), mCrater, new Vector3(4.8f, 0.001f, -8f));
            Decorado("Crater_2", crateres, GeneradorMarte.Crater("Crater_2", 1.0f, 2), mCrater, new Vector3(-5.6f, 0.001f, 3.3f));

            // ---------------- Rocas marcianas de muestra (manipulables) ----------------
            var objs = new GameObject("Rocas_Marcianas").transform;
            objs.SetParent(entorno, false);
            CrearRocaMuestra("Roca_Hematita", "Hematita", objs, new Vector3(-5.5f, 0.3f, -1f), 0.46f, 11,
                new Color(0.45f, 0.1f, 0.1f), new Color(1.6f, 0.15f, 0.1f), 0.6f, 1.1f);
            CrearRocaMuestra("Roca_Olivino", "Olivino", objs, new Vector3(5.5f, 0.3f, 0.5f), 0.44f, 12,
                new Color(0.35f, 0.55f, 0.15f), new Color(0.4f, 1.3f, 0.1f), 0.2f, 1.0f);
            CrearRocaMuestra("Roca_Basalto", "Basalto", objs, new Vector3(-2.2f, 0.3f, 5f), 0.46f, 13,
                new Color(0.2f, 0.2f, 0.22f), new Color(0.7f, 0.25f, 1.4f), 0.1f, 1.2f);
            CrearRocaMuestra("Roca_Jarosita", "Jarosita", objs, new Vector3(5.0f, 0.3f, -5f), 0.42f, 14,
                new Color(0.85f, 0.65f, 0.15f), new Color(1.4f, 1.0f, 0.1f), 0.1f, 0.9f);
            CrearRocaMuestra("Roca_Meteorito", "Meteorito", objs, new Vector3(-5.5f, 0.3f, 6.5f), 0.40f, 15,
                new Color(0.3f, 0.32f, 0.38f), new Color(0.2f, 0.8f, 1.8f), 0.8f, 1.3f);

            // ---------------- Base de recolección (zona objetivo) ----------------
            var zonaGo = new GameObject("ZonaObjetivo_BaseAres");
            zonaGo.transform.SetParent(entorno, false);
            zonaGo.transform.position = new Vector3(0f, 0f, 8.2f);
            var bc = zonaGo.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = new Vector3(0, 0.75f, 0);
            bc.size = new Vector3(3.4f, 1.5f, 2.6f);
            var zona = zonaGo.AddComponent<ZonaObjetivo>();
            var zt = zonaGo.transform;
            Prim(PrimitiveType.Cube, "Plataforma", zt, new Vector3(0, 0.012f, 0), new Vector3(3.4f, 0.024f, 2.6f), mPlataforma, false);
            Prim(PrimitiveType.Cube, "Brillo_Zona", zt, new Vector3(0, 0.03f, 0), new Vector3(3.3f, 0.01f, 2.5f), mZona, false);
            foreach (float sx in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Borde_Luz", zt, new Vector3(sx * 1.68f, 0.03f, 0), new Vector3(0.05f, 0.03f, 2.6f), mLuzBase, false);
                Prim(PrimitiveType.Cube, "Borde_Luz", zt, new Vector3(0, 0.03f, sx * 1.28f), new Vector3(3.4f, 0.03f, 0.05f), mLuzBase, false);
                foreach (float sz in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cylinder, "Faro_Esquina", zt, new Vector3(sx * 1.68f, 0.08f, sz * 1.28f), new Vector3(0.1f, 0.08f, 0.1f), mLuzBase, false);
            }
            Texto3D("Letrero_Base", zt, new Vector3(0, 0.045f, -0.35f), Quaternion.Euler(90, 0, 0), "BASE ARES", 0.07f, new Color(0.55f, 0.95f, 1f));
            Texto3D("Letrero_Instruccion", zt, new Vector3(0, 0.045f, -0.85f), Quaternion.Euler(90, 0, 0), "DEPOSITA LAS ROCAS AQUÍ", 0.028f, new Color(1f, 0.8f, 0.5f));

            ConstruirHabitat(entorno, mHabitat, mVentana, mPanel, mPoste, mDorado, mOscuro, mBaliza);

            // Rocas sueltas y montículos fuera del área (decoración lejana)
            var lejos = new GameObject("Rocas_Lejanas").transform;
            lejos.SetParent(entorno, false);
            for (int i = 0; i < 70; i++)
            {
                float ang = Azar(0, Mathf.PI * 2), dist = Azar(14f, 70f);
                float x = Mathf.Cos(ang) * dist, z = Mathf.Sin(ang) * dist;
                if (Mathf.Abs(x) < 10 && Mathf.Abs(z) < 12) continue;
                if (z > 11f && z < 22f && Mathf.Abs(x) < 14f) continue;   // deja libre la zona del hábitat
                float s = Azar(0.4f, 1f) * Mathf.Lerp(1f, 4f, (dist - 14f) / 56f);
                Roca("Roca_Lejana", lejos, rocas[rnd.Next(rocas.Length)], matsRoca[rnd.Next(3)],
                    new Vector3(x, GeneradorMarte.Altura(x, z) + s * 0.15f, z), new Vector3(s * Azar(0.8f, 1.4f), s * Azar(0.5f, 1f), s), Azar(0, 360), false);
            }

            // ---------------- Robot móvil (rover) ----------------
            var robot = ConstruirRobot(mBlanco, mOscuro, mNaranja, mLente, mDorado, mPanel, out var gripper, out var colisiones);

            // ---------------- Avatar (usuario) ----------------
            var avatar = ConstruirAvatar(mBlanco, mDorado, out var cabeza, out var hudTexto, out var hudAviso, out var cuerpoAstro, out var cascoAstro);
            PolvoMarciano(avatar.transform);

            // ---------------- Sistema ----------------
            var sistema = new GameObject("Sistema");
            var enlace = sistema.AddComponent<EnlaceHaptico>();
            var registro = sistema.AddComponent<RegistroMetricas>();
            registro.robot = robot;
            var mision = sistema.AddComponent<MisionTarea>();
            mision.zona = zona;
            mision.robot = robot;
            mision.gripper = gripper;
            mision.colisiones = colisiones;
            mision.avatar = avatar;
            var hud = sistema.AddComponent<HUDVR>();
            hud.texto = hudTexto;
            hud.aviso = hudAviso;
            hud.robot = robot;
            hud.gripper = gripper;
            hud.mision = mision;
            var app = sistema.AddComponent<ConfiguracionApp>();
            app.cabeza = cabeza;
            app.hud = hud;
            app.mision = mision;

            // Modo espectador: el celular envía el estado por Wi-Fi y el PC lo muestra
            var emisor = sistema.AddComponent<EmisorEspejo>();
            emisor.robot = robot;
            emisor.gripper = gripper;
            emisor.avatar = avatar;
            emisor.cabeza = cabeza.transform;
            emisor.mision = mision;
            var receptor = sistema.AddComponent<ReceptorEspejo>();
            receptor.robot = robot;
            receptor.gripper = gripper;
            receptor.avatar = avatar;
            receptor.cabeza = cabeza.transform;
            receptor.cuerpoAvatar = cuerpoAstro;
            receptor.cascoAvatar = cascoAstro;
            receptor.desactivarEnEspectador = new Behaviour[]
            {
                robot, gripper, colisiones, avatar, cabeza, cabeza.GetComponent<CamaraEstereoCardboard>(),
                mision, hud, app, registro
            };
            receptor.ocultarEnEspectador = new[] { hudTexto.gameObject, hudAviso.gameObject };

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena, RutaEscena);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(RutaEscena, true) };
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = robot.gameObject;
            Debug.Log("[Teleoperación RV] Escena de Marte construida: " + RutaEscena + ". Presiona Play y usa W/A/S/D, G y las flechas.");
        }

        // =====================================================================
        static Material CieloMarte()
        {
            string ruta = CarpetaMat + "/Cielo_Marte.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            var sh = Shader.Find("TeleoperacionRV/CieloMarte");
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, ruta);
            }
            else m.shader = sh;
            return m;
        }

        static GameObject Roca(string nombre, Transform padre, Mesh malla, Material mat, Vector3 pos, Vector3 escala, float giroY, bool conCollider)
        {
            var g = new GameObject(nombre);
            g.transform.SetParent(padre, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0, giroY, 0);
            g.transform.localScale = escala;
            g.AddComponent<MeshFilter>().sharedMesh = malla;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (conCollider)
            {
                var mc = g.AddComponent<MeshCollider>();
                mc.sharedMesh = malla;
                mc.convex = true;
            }
            g.isStatic = true;
            return g;
        }

        static void Decorado(string nombre, Transform padre, Mesh malla, Material mat, Vector3 pos)
        {
            var g = new GameObject(nombre);
            g.transform.SetParent(padre, false);
            g.transform.localPosition = pos;
            g.AddComponent<MeshFilter>().sharedMesh = malla;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            g.isStatic = true;
        }

        static void CrearRocaMuestra(string nombre, string mostrado, Transform padre, Vector3 pos, float tam, int semilla,
            Color color, Color brillo, float metal, float masa)
        {
            var malla = GeneradorMarte.Roca("Muestra_" + mostrado, semilla, 0.3f, new Vector3(1f, 0.85f, 1.1f));
            var mat = Mat("Muestra_" + mostrado, color, metal, 0.45f, brillo);
            var g = new GameObject(nombre);
            g.transform.SetParent(padre, false);
            g.transform.localPosition = pos;
            g.transform.localScale = Vector3.one * tam;
            g.transform.localRotation = Quaternion.Euler(0, semilla * 37f, 0);
            g.AddComponent<MeshFilter>().sharedMesh = malla;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var mc = g.AddComponent<MeshCollider>();
            mc.sharedMesh = malla;
            mc.convex = true;
            g.tag = "Manipulable";
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = masa;
            rb.linearDamping = 0.3f;
            rb.angularDamping = 0.8f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var m = g.AddComponent<Manipulable>();
            m.nombreMostrado = mostrado;
        }

        static void ConstruirHabitat(Transform entorno, Material blanco, Material ventana, Material panel, Material metal,
            Material dorado, Material oscuro, Material baliza)
        {
            var hab = new GameObject("Habitat_Ares").transform;
            hab.SetParent(entorno, false);
            float Y(float x, float z) => GeneradorMarte.Altura(x, z);

            // Domo principal y módulos
            Prim(PrimitiveType.Sphere, "Domo", hab, new Vector3(-3f, Y(-3f, 16f) - 0.3f, 16f), new Vector3(6f, 4f, 6f), blanco, false);
            Prim(PrimitiveType.Cylinder, "Anillo_Domo", hab, new Vector3(-3f, Y(-3f, 16f) + 0.2f, 16f), new Vector3(6.1f, 0.25f, 6.1f), dorado, false);
            Prim(PrimitiveType.Cube, "Ventanal", hab, new Vector3(-3f, Y(-3f, 16f) + 0.9f, 13.05f), new Vector3(2.2f, 0.5f, 0.1f), ventana, false);
            Prim(PrimitiveType.Cylinder, "Tunel", hab, new Vector3(1.2f, Y(1.2f, 16f) + 0.7f, 16f), new Vector3(1.4f, 2.2f, 1.4f), blanco, false, new Vector3(0, 0, 90));
            Prim(PrimitiveType.Sphere, "Domo_Pequeño", hab, new Vector3(4.5f, Y(4.5f, 16f) - 0.2f, 16f), new Vector3(3.4f, 2.6f, 3.4f), blanco, false);
            Prim(PrimitiveType.Cube, "Puerta", hab, new Vector3(4.5f, Y(4.5f, 16f) + 0.5f, 14.35f), new Vector3(0.8f, 1.1f, 0.1f), oscuro, false);

            // Paneles solares
            for (int i = 0; i < 4; i++)
            {
                float x = -10.5f + i * 2.1f, z = 19.5f;
                float y = Y(x, z);
                Prim(PrimitiveType.Cylinder, "Soporte_Panel", hab, new Vector3(x, y + 0.5f, z), new Vector3(0.1f, 0.5f, 0.1f), metal, false);
                Prim(PrimitiveType.Cube, "Panel_Solar", hab, new Vector3(x, y + 1.05f, z), new Vector3(1.9f, 0.05f, 1.2f), panel, false, new Vector3(-30f, 0, 0));
            }

            // Antena
            float xa = 8f, za = 18f, ya = Y(xa, za);
            Prim(PrimitiveType.Cylinder, "Torre_Antena", hab, new Vector3(xa, ya + 2f, za), new Vector3(0.15f, 2f, 0.15f), metal, false);
            Prim(PrimitiveType.Sphere, "Plato_Antena", hab, new Vector3(xa, ya + 4.1f, za - 0.2f), new Vector3(1.4f, 1.4f, 0.35f), blanco, false, new Vector3(-25f, 0, 0));
            Prim(PrimitiveType.Sphere, "Luz_Antena", hab, new Vector3(xa, ya + 4.2f, za), Vector3.one * 0.18f, baliza, false);

            // Módulo de aterrizaje
            float xl = 12.5f, zl = 14f, yl = Y(xl, zl);
            Prim(PrimitiveType.Cylinder, "Cuerpo_Lander", hab, new Vector3(xl, yl + 2.2f, zl), new Vector3(2.2f, 1.2f, 2.2f), blanco, false);
            Prim(PrimitiveType.Sphere, "Punta_Lander", hab, new Vector3(xl, yl + 3.4f, zl), new Vector3(2.2f, 1.6f, 2.2f), blanco, false);
            Prim(PrimitiveType.Cylinder, "Faldon_Lander", hab, new Vector3(xl, yl + 1.05f, zl), new Vector3(2.6f, 0.15f, 2.6f), dorado, false);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                var pie = new Vector3(xl, 0, zl) + dir * 1.9f;
                Prim(PrimitiveType.Cylinder, "Pata_Lander", hab, new Vector3(pie.x, Y(pie.x, pie.z) + 0.55f, pie.z) - dir * 0.35f,
                    new Vector3(0.1f, 0.65f, 0.1f), metal, false, Quaternion.LookRotation(dir).eulerAngles + new Vector3(-30f, 0, 0) + new Vector3(90f, 0, 0));
                Prim(PrimitiveType.Cylinder, "Pie_Lander", hab, new Vector3(pie.x, Y(pie.x, pie.z) + 0.03f, pie.z), new Vector3(0.45f, 0.03f, 0.45f), metal, false);
            }

            // Bandera UMNG
            float xf = 2.6f, zf = 11.3f, yf = Y(xf, zf);
            Prim(PrimitiveType.Cylinder, "Asta", hab, new Vector3(xf, yf + 1.2f, zf), new Vector3(0.05f, 1.2f, 0.05f), metal, false);
            Prim(PrimitiveType.Cube, "Bandera", hab, new Vector3(xf + 0.46f, yf + 2.1f, zf), new Vector3(0.9f, 0.55f, 0.02f), MatBandera(), false);
            Texto3D("Texto_Bandera", hab, new Vector3(xf + 0.46f, yf + 2.1f, zf - 0.02f), Quaternion.identity, "UMNG", 0.03f, Color.white);
        }

        static Material MatBandera() => Mat("Bandera_UMNG", new Color(0.05f, 0.25f, 0.55f), 0f, 0.3f);

        static void PolvoMarciano(Transform avatar)
        {
            var g = new GameObject("Polvo_Marciano");
            g.transform.SetParent(avatar, false);
            g.transform.localPosition = new Vector3(0, 1.5f, 0);
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.startLifetime = 9f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.05f);
            main.startColor = new Color(1f, 0.78f, 0.6f, 0.55f);
            main.maxParticles = 260;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var em = ps.emission;
            em.rateOverTime = 28f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(16f, 3f, 16f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            string ruta = CarpetaMat + "/Polvo_Marciano.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            var shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Particles/Standard Unlit");
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, ruta); }
            else m.shader = shader;
            m.mainTexture = GeneradorMarte.TexturaPunto();
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(m);
            var r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = m;
            r.renderMode = ParticleSystemRenderMode.Billboard;
        }

        // =====================================================================
        static RobotController ConstruirRobot(Material blanco, Material oscuro, Material naranja, Material lente,
            Material dorado, Material panel, out RobotGripper gripper, out RobotHapticaColisiones colisiones)
        {
            var raiz = new GameObject("Rover_UMNG1");
            raiz.tag = "Robot";
            raiz.transform.position = new Vector3(0f, 0.02f, -5f);

            var rb = raiz.AddComponent<Rigidbody>();
            rb.mass = 15f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var col = raiz.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.22f, 0f);
            col.size = new Vector3(0.9f, 0.4f, 1.0f);
            col.sharedMaterial = MaterialFisicoSinFriccion();

            var vis = new GameObject("Visual").transform;
            vis.SetParent(raiz.transform, false);
            Prim(PrimitiveType.Cube, "Chasis", vis, new Vector3(0, 0.32f, 0), new Vector3(0.66f, 0.18f, 0.86f), blanco, false);
            Prim(PrimitiveType.Cube, "Aislante_Dorado", vis, new Vector3(0, 0.24f, 0), new Vector3(0.62f, 0.06f, 0.82f), dorado, false);
            Prim(PrimitiveType.Cube, "Panel_Solar", vis, new Vector3(0, 0.43f, -0.05f), new Vector3(0.84f, 0.025f, 0.8f), panel, false);
            Prim(PrimitiveType.Cube, "Franja", vis, new Vector3(0, 0.32f, 0.435f), new Vector3(0.6f, 0.06f, 0.02f), naranja, false);

            // 6 ruedas con balancines (rocker-bogie)
            var izq = new Transform[3];
            var der = new Transform[3];
            float[] zs = { 0.36f, 0f, -0.36f };
            for (int k = 0; k < 3; k++)
            {
                izq[k] = Prim(PrimitiveType.Cylinder, "Rueda_Izq_" + (k + 1), vis, new Vector3(-0.44f, 0.15f, zs[k]), new Vector3(0.3f, 0.06f, 0.3f), oscuro, false, new Vector3(0, 0, 90)).transform;
                der[k] = Prim(PrimitiveType.Cylinder, "Rueda_Der_" + (k + 1), vis, new Vector3(0.44f, 0.15f, zs[k]), new Vector3(0.3f, 0.06f, 0.3f), oscuro, false, new Vector3(0, 0, 90)).transform;
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Balancin", vis, new Vector3(sx * 0.38f, 0.27f, 0.18f), new Vector3(0.03f, 0.03f, 0.42f), oscuro, false, new Vector3(-12f, 0, 0));
                Prim(PrimitiveType.Cube, "Balancin", vis, new Vector3(sx * 0.38f, 0.27f, -0.18f), new Vector3(0.03f, 0.03f, 0.42f), oscuro, false, new Vector3(12f, 0, 0));
            }

            // Mástil con cámara y antena
            Prim(PrimitiveType.Cylinder, "Base_Brazo", vis, new Vector3(0, 0.46f, 0.25f), new Vector3(0.16f, 0.03f, 0.16f), naranja, false);
            Prim(PrimitiveType.Cube, "Brazo", vis, new Vector3(0, 0.5f, 0.38f), new Vector3(0.07f, 0.07f, 0.3f), blanco, false);
            Prim(PrimitiveType.Cylinder, "Mastil", vis, new Vector3(0.22f, 0.62f, 0.25f), new Vector3(0.04f, 0.2f, 0.04f), oscuro, false);
            Prim(PrimitiveType.Cube, "Cabeza_Camara", vis, new Vector3(0.22f, 0.85f, 0.27f), new Vector3(0.2f, 0.09f, 0.1f), blanco, false);
            Prim(PrimitiveType.Sphere, "Lente", vis, new Vector3(0.18f, 0.85f, 0.325f), new Vector3(0.045f, 0.045f, 0.02f), lente, false);
            Prim(PrimitiveType.Sphere, "Lente", vis, new Vector3(0.26f, 0.85f, 0.325f), new Vector3(0.045f, 0.045f, 0.02f), lente, false);
            Prim(PrimitiveType.Cylinder, "Soporte_Antena", vis, new Vector3(-0.25f, 0.52f, -0.32f), new Vector3(0.02f, 0.08f, 0.02f), oscuro, false);
            Prim(PrimitiveType.Sphere, "Antena_Plato", vis, new Vector3(-0.25f, 0.61f, -0.32f), new Vector3(0.2f, 0.2f, 0.05f), blanco, false, new Vector3(-35f, 0, 0));
            Texto3D("Nombre_Rover", vis, new Vector3(0.335f, 0.33f, -0.05f), Quaternion.Euler(0, -90, 0), "UMNG-1", 0.012f, new Color(0.1f, 0.1f, 0.12f));

            // Pinza
            var pinza = new GameObject("Pinza").transform;
            pinza.SetParent(raiz.transform, false);
            pinza.localPosition = new Vector3(0f, 0.3f, 0.55f);
            Prim(PrimitiveType.Cube, "Palma", pinza, Vector3.zero, new Vector3(0.7f, 0.1f, 0.06f), naranja, false);
            var dIzq = Prim(PrimitiveType.Cube, "Dedo_Izquierdo", pinza, new Vector3(-0.3f, 0, 0.22f), new Vector3(0.04f, 0.14f, 0.42f), oscuro, false).transform;
            var dDer = Prim(PrimitiveType.Cube, "Dedo_Derecho", pinza, new Vector3(0.3f, 0, 0.22f), new Vector3(0.04f, 0.14f, 0.42f), oscuro, false).transform;
            var punto = new GameObject("PuntoAgarre").transform;
            punto.SetParent(pinza, false);
            punto.localPosition = new Vector3(0f, 0.05f, 0.27f);

            var zonaAgarre = new GameObject("ZonaAgarre");
            zonaAgarre.transform.SetParent(raiz.transform, false);
            zonaAgarre.transform.localPosition = new Vector3(0f, 0.3f, 0.9f);
            var zc = zonaAgarre.AddComponent<BoxCollider>();
            zc.isTrigger = true;
            zc.size = new Vector3(0.9f, 0.7f, 0.8f);
            var zona = zonaAgarre.AddComponent<GripperZona>();

            var ctrl = raiz.AddComponent<RobotController>();
            ctrl.ruedasIzquierdas = izq;
            ctrl.ruedasDerechas = der;
            ctrl.radioRueda = 0.15f;
            ctrl.anchoVia = 0.88f;

            gripper = raiz.AddComponent<RobotGripper>();
            gripper.puntoAgarre = punto;
            gripper.zona = zona;
            gripper.dedoIzquierdo = dIzq;
            gripper.dedoDerecho = dDer;
            gripper.aperturaAbierta = 0.3f;
            gripper.aperturaCerrada = 0.21f;

            colisiones = raiz.AddComponent<RobotHapticaColisiones>();
            return ctrl;
        }

        static AvatarNavegacion ConstruirAvatar(Material traje, Material visor, out SeguimientoCabeza cabeza, out TextMesh hud, out TextMesh aviso,
            out GameObject cuerpo, out GameObject casco)
        {
            var avatar = new GameObject("Avatar");
            avatar.tag = "Player";
            avatar.transform.position = new Vector3(0f, 0.05f, -8.5f);
            var cc = avatar.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.3f;

            var cab = new GameObject("Cabeza");
            cab.transform.SetParent(avatar.transform, false);
            cab.transform.localPosition = new Vector3(0, 1.65f, 0);
            cab.AddComponent<AudioListener>();
            cabeza = cab.AddComponent<SeguimientoCabeza>();

            var shader = Shader.Find("Hidden/TeleoperacionRV/DistorsionLente");
            Camera Cam(string n, bool principal)
            {
                var g = new GameObject(n);
                g.transform.SetParent(cab.transform, false);
                var c = g.AddComponent<Camera>();
                c.nearClipPlane = 0.05f;
                c.farClipPlane = 180f;
                c.clearFlags = CameraClearFlags.Skybox;
                c.backgroundColor = new Color(0.8f, 0.55f, 0.4f);
                if (principal) g.tag = "MainCamera";
                return c;
            }
            var mono = Cam("CamaraMono", true);
            var ojoI = Cam("OjoIzquierdo", false);
            var ojoD = Cam("OjoDerecho", false);
            ojoI.gameObject.AddComponent<DistorsionLente>().shader = shader;
            ojoD.gameObject.AddComponent<DistorsionLente>().shader = shader;

            var estereo = cab.AddComponent<CamaraEstereoCardboard>();
            estereo.ojoIzquierdo = ojoI;
            estereo.ojoDerecho = ojoD;
            estereo.camaraMono = mono;

            // HUD flotante frente a la vista + aviso grande al centro
            hud = Texto3D("HUD", cab.transform, new Vector3(0f, -0.3f, 1.2f), Quaternion.identity, "HUD", 0.0045f, Color.white);
            hud.anchor = TextAnchor.MiddleCenter;
            hud.alignment = TextAlignment.Left;
            aviso = Texto3D("Aviso", cab.transform, new Vector3(0f, 0.12f, 1.2f), Quaternion.identity, "", 0.0075f, Color.white);
            aviso.anchor = TextAnchor.MiddleCenter;
            aviso.alignment = TextAlignment.Center;

            // Astronauta visible solo en el PC (modo espectador); en el celular está oculto
            cuerpo = new GameObject("Cuerpo_Astronauta");
            cuerpo.transform.SetParent(avatar.transform, false);
            Prim(PrimitiveType.Capsule, "Traje", cuerpo.transform, new Vector3(0, 0.95f, 0), new Vector3(0.55f, 0.62f, 0.4f), traje, false);
            Prim(PrimitiveType.Cube, "Mochila", cuerpo.transform, new Vector3(0, 1.15f, -0.27f), new Vector3(0.42f, 0.55f, 0.2f), traje, false);
            Prim(PrimitiveType.Cube, "Parche_Naranja", cuerpo.transform, new Vector3(0.14f, 1.25f, 0.2f), new Vector3(0.1f, 0.06f, 0.01f), visor, false);
            cuerpo.SetActive(false);
            casco = new GameObject("Casco_Astronauta");
            casco.transform.SetParent(cab.transform, false);
            Prim(PrimitiveType.Sphere, "Casco", casco.transform, Vector3.zero, Vector3.one * 0.32f, traje, false);
            Prim(PrimitiveType.Sphere, "Visor", casco.transform, new Vector3(0, 0, 0.07f), new Vector3(0.27f, 0.2f, 0.2f), visor, false);
            casco.SetActive(false);

            var nav = avatar.AddComponent<AvatarNavegacion>();
            nav.cabeza = cab.transform;
            return nav;
        }

        // =====================================================================
        static GameObject Prim(PrimitiveType t, string nombre, Transform padre, Vector3 pos, Vector3 escala, Material mat,
            bool conCollider = true, Vector3? rot = null)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = nombre;
            g.transform.SetParent(padre, false);
            g.transform.localPosition = pos;
            g.transform.localScale = escala;
            if (rot.HasValue) g.transform.localEulerAngles = rot.Value;
            if (mat) g.GetComponent<Renderer>().sharedMaterial = mat;
            if (!conCollider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static void Pared(string nombre, Transform padre, Vector3 pos, Vector3 escala, Material mat)
        {
            var p = Prim(PrimitiveType.Cube, nombre, padre, pos, escala, mat);
            p.tag = "Limite";
            p.isStatic = true;
        }

        static void CrearManipulable(string nombre, PrimitiveType t, Transform padre, Vector3 pos, float tam, Material mat, float masa)
        {
            var g = Prim(t, nombre, padre, pos, Vector3.one * tam, mat);
            g.tag = "Manipulable";
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = masa;
            rb.linearDamping = 0.3f;
            rb.angularDamping = t == PrimitiveType.Sphere ? 2f : 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            g.AddComponent<Manipulable>();
        }

        static TextMesh Texto3D(string nombre, Transform padre, Vector3 pos, Quaternion rot, string txt, float tamCaracter, Color color)
        {
            var g = new GameObject(nombre);
            g.transform.SetParent(padre, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            var tm = g.AddComponent<TextMesh>();
            var fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = fuente;
            tm.fontSize = 64;
            tm.characterSize = tamCaracter;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = color;
            tm.richText = true;
            tm.text = txt;
            var r = g.GetComponent<MeshRenderer>();
            if (fuente) r.sharedMaterial = fuente.material;
            return tm;
        }

        static Material Mat(string nombre, Color c, float metal, float suavidad, Color? emision = null)
        {
            string ruta = $"{CarpetaMat}/{nombre}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, ruta);
            }
            m.color = c;
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Glossiness", suavidad);
            if (emision.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emision.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MatTransparente(string nombre, Color c)
        {
            var m = Mat(nombre, c, 0f, 0.3f, new Color(c.r, c.g, c.b) * 0.4f);
            m.SetFloat("_Mode", 2f);   // Fade
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static PhysicsMaterial MaterialFisicoSinFriccion()
        {
            string ruta = CarpetaMat + "/Robot_SinFriccion.asset";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ruta);
            if (pm == null)
            {
                pm = new PhysicsMaterial("Robot_SinFriccion");
                AssetDatabase.CreateAsset(pm, ruta);
            }
            pm.staticFriction = 0f;
            pm.dynamicFriction = 0f;
            pm.frictionCombine = PhysicsMaterialCombine.Minimum;
            pm.bounciness = 0f;
            EditorUtility.SetDirty(pm);
            return pm;
        }

        static void AsegurarEtiquetas(params string[] etiquetas)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var tags = so.FindProperty("tags");
            foreach (var e in etiquetas)
            {
                bool existe = false;
                for (int i = 0; i < tags.arraySize; i++)
                    if (tags.GetArrayElementAtIndex(i).stringValue == e) { existe = true; break; }
                if (existe) continue;
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = e;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
