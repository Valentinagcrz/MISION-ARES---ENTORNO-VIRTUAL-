using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TeleoperacionRV.EditorTools
{
    /// <summary>
    /// Genera (en el Editor) las mallas y texturas procedurales del escenario de Marte:
    /// rocas de estilo "low-poly", terreno con dunas y montañas, cráteres y la textura
    /// de arena. Todo se guarda como assets en Assets/Materiales para que la escena
    /// no dependa de modelos externos.
    /// </summary>
    public static class GeneradorMarte
    {
        const string CarpetaMallas = "Assets/Materiales/Mallas";
        const string CarpetaTex = "Assets/Materiales/Texturas";

        // Límite plano del área de juego (medio ancho / medio largo) + margen
        public const float MedioAncho = 8.6f;
        public const float MedioLargo = 10.6f;

        // ------------------------------------------------------------------
        //  Relieve
        // ------------------------------------------------------------------
        static float Suave(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        /// <summary>Altura del terreno en (x, z). Es 0 dentro del área de juego.</summary>
        public static float Altura(float x, float z)
        {
            float dx = Mathf.Max(Mathf.Abs(x) - MedioAncho, 0f);
            float dz = Mathf.Max(Mathf.Abs(z) - MedioLargo, 0f);
            float e = Mathf.Sqrt(dx * dx + dz * dz);
            if (e <= 0f) return 0f;
            float dunas = (Mathf.PerlinNoise(x * 0.06f + 13f, z * 0.06f + 7f) * 0.7f +
                           Mathf.PerlinNoise(x * 0.17f + 3f, z * 0.17f + 91f) * 0.3f) * 4f * Suave(0f, 22f, e);
            float cresta = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.018f + 50f, z * 0.018f + 20f) * 2f - 1f);
            float montes = Suave(40f, 95f, e) * (cresta * cresta * 32f + Mathf.PerlinNoise(x * 0.05f, z * 0.05f) * 6f);
            return dunas + montes;
        }

        public static Mesh Terreno(float tam = 260f, int n = 120)
        {
            var v = new Vector3[(n + 1) * (n + 1)];
            var uv = new Vector2[v.Length];
            for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    float x = -tam / 2 + tam * i / n, z = -tam / 2 + tam * j / n;
                    v[j * (n + 1) + i] = new Vector3(x, Altura(x, z), z);
                    uv[j * (n + 1) + i] = new Vector2(x / 3f, z / 3f);
                }
            var t = new int[n * n * 6];
            int k = 0;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                    t[k++] = a; t[k++] = c; t[k++] = b;
                    t[k++] = b; t[k++] = c; t[k++] = d;
                }
            var m = new Mesh { name = "Terreno_Marte", vertices = v, uv = uv, triangles = t };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return Guardar(m, "Terreno_Marte");
        }

        // ------------------------------------------------------------------
        //  Rocas (icosfera deformada con ruido, sombreado plano)
        // ------------------------------------------------------------------
        static float Ruido3(Vector3 p) =>
            (Mathf.PerlinNoise(p.x, p.y) + Mathf.PerlinNoise(p.y + 31.7f, p.z) + Mathf.PerlinNoise(p.z + 7.3f, p.x + 17.1f)) / 3f;

        public static Mesh Roca(string nombre, int semilla, float rugosidad, Vector3 forma)
        {
            BaseIcosfera(2, out var verts, out var tris);
            var rnd = new System.Random(semilla);
            var off1 = new Vector3((float)rnd.NextDouble() * 100f, (float)rnd.NextDouble() * 100f, (float)rnd.NextDouble() * 100f);
            var off2 = new Vector3((float)rnd.NextDouble() * 100f, (float)rnd.NextDouble() * 100f, (float)rnd.NextDouble() * 100f);
            for (int i = 0; i < verts.Count; i++)
            {
                var d = verts[i].normalized;
                float r = 0.5f * (1f + rugosidad * (Ruido3(d * 1.6f + off1) - 0.5f) * 2.2f
                                     + rugosidad * 0.45f * (Ruido3(d * 4.2f + off2) - 0.5f) * 2f);
                var p = d * r;
                if (p.y < -0.25f) p.y = -0.25f + (p.y + 0.25f) * 0.45f;   // base algo plana para que se apoye
                verts[i] = Vector3.Scale(p, forma);
            }
            // Sombreado plano: cada triángulo con sus propios vértices
            var v = new Vector3[tris.Count];
            var uv = new Vector2[tris.Count];
            var t = new int[tris.Count];
            for (int i = 0; i < tris.Count; i++)
            {
                v[i] = verts[tris[i]];
                uv[i] = new Vector2(v[i].x + v[i].z, v[i].y);
                t[i] = i;
            }
            var m = new Mesh { name = nombre, vertices = v, uv = uv, triangles = t };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return Guardar(m, nombre);
        }

        static void BaseIcosfera(int subdiv, out List<Vector3> v, out List<int> t)
        {
            float f = (1f + Mathf.Sqrt(5f)) / 2f;
            v = new List<Vector3>
            {
                new Vector3(-1, f, 0), new Vector3(1, f, 0), new Vector3(-1, -f, 0), new Vector3(1, -f, 0),
                new Vector3(0, -1, f), new Vector3(0, 1, f), new Vector3(0, -1, -f), new Vector3(0, 1, -f),
                new Vector3(f, 0, -1), new Vector3(f, 0, 1), new Vector3(-f, 0, -1), new Vector3(-f, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            t = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };
            for (int s = 0; s < subdiv; s++)
            {
                var cache = new Dictionary<long, int>();
                var nt = new List<int>();
                var vv = v;
                int Medio(int a, int b)
                {
                    long clave = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                    if (cache.TryGetValue(clave, out int idx)) return idx;
                    vv.Add(((vv[a] + vv[b]) * 0.5f).normalized);
                    cache[clave] = vv.Count - 1;
                    return vv.Count - 1;
                }
                for (int i = 0; i < t.Count; i += 3)
                {
                    int a = t[i], b = t[i + 1], c = t[i + 2];
                    int ab = Medio(a, b), bc = Medio(b, c), ca = Medio(c, a);
                    nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                t = nt;
            }
        }

        // ------------------------------------------------------------------
        //  Cráter (anillo con borde elevado, solo decorativo)
        // ------------------------------------------------------------------
        public static Mesh Crater(string nombre, float radio, int semilla)
        {
            float[] rr = { 0f, 0.5f, 0.72f, 0.88f, 1f, 1.15f, 1.45f };
            float[] hh = { 0.012f, 0.014f, 0.04f, 0.09f, 0.085f, 0.035f, 0.002f };
            const int seg = 40;
            var rnd = new System.Random(semilla);
            float fase = (float)rnd.NextDouble() * 10f;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            for (int a = 0; a < seg; a++)
            {
                float ang = a * Mathf.PI * 2f / seg;
                float irreg = 0.85f + 0.3f * Mathf.PerlinNoise(fase + a * 0.35f, 3.3f);
                for (int p = 0; p < rr.Length; p++)
                {
                    float r = rr[p] * radio;
                    var pos = new Vector3(Mathf.Cos(ang) * r, hh[p] * radio * (p > 0 ? irreg : 1f), Mathf.Sin(ang) * r);
                    v.Add(pos);
                    uv.Add(new Vector2(pos.x / 3f, pos.z / 3f));
                }
            }
            var t = new List<int>();
            int np = rr.Length;
            for (int a = 0; a < seg; a++)
            {
                int a2 = (a + 1) % seg;
                for (int p = 0; p < np - 1; p++)
                {
                    int i0 = a * np + p, i1 = a * np + p + 1, j0 = a2 * np + p, j1 = a2 * np + p + 1;
                    t.AddRange(new[] { i0, j0, i1, i1, j0, j1 });
                }
            }
            var m = new Mesh { name = nombre };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return Guardar(m, nombre);
        }

        // ------------------------------------------------------------------
        //  Texturas
        // ------------------------------------------------------------------
        /// <summary>Arena/regolito marciano, repetible sin costuras.</summary>
        public static Texture2D TexturaArena()
        {
            string ruta = CarpetaTex + "/Arena_Marte.png";
            if (!File.Exists(ruta))
            {
                const int N = 512;
                var tex = new Texture2D(N, N, TextureFormat.RGB24, false);
                var oscuro = new Color(0.46f, 0.22f, 0.12f);
                var medio = new Color(0.70f, 0.37f, 0.21f);
                var claro = new Color(0.86f, 0.56f, 0.36f);
                var rnd = new System.Random(7);
                float Fbm(float x, float y)
                {
                    float s = 0f, a = 0.5f, f = 1f;
                    for (int o = 0; o < 5; o++) { s += a * Mathf.PerlinNoise(x * f * 0.012f + 11f, y * f * 0.012f + 5f); a *= 0.5f; f *= 2.1f; }
                    return s;
                }
                float Rep(float x, float y) =>   // mezcla de 4 muestras -> repetible
                    (Fbm(x, y) * (N - x) * (N - y) + Fbm(x - N, y) * x * (N - y) +
                     Fbm(x, y - N) * (N - x) * y + Fbm(x - N, y - N) * x * y) / (N * (float)N);
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float n = Mathf.Clamp01((Rep(x, y) - 0.25f) * 2.2f);
                        var c = n < 0.5f ? Color.Lerp(oscuro, medio, n * 2f) : Color.Lerp(medio, claro, (n - 0.5f) * 2f);
                        float grano = (float)rnd.NextDouble();
                        c *= 0.9f + 0.2f * grano;
                        if (grano > 0.995f) c *= 0.55f;           // piedritas oscuras
                        else if (grano < 0.004f) c *= 1.25f;      // granos claros
                        c.a = 1f;
                        tex.SetPixel(x, y, c);
                    }
                Escribir(tex, ruta, true);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        }

        /// <summary>Punto suave para las partículas de polvo.</summary>
        public static Texture2D TexturaPunto()
        {
            string ruta = CarpetaTex + "/Punto_Polvo.png";
            if (!File.Exists(ruta))
            {
                const int N = 64;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                        float a = Mathf.Clamp01(1f - d);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                Escribir(tex, ruta, false);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        }

        static void Escribir(Texture2D tex, string ruta, bool repetir)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(ruta);
            var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
            imp.wrapMode = repetir ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.alphaIsTransparency = !repetir;
            imp.anisoLevel = repetir ? 8 : 1;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }

        static Mesh Guardar(Mesh m, string nombre)
        {
            Directory.CreateDirectory(CarpetaMallas);
            string ruta = CarpetaMallas + "/" + nombre + ".asset";
            if (File.Exists(ruta)) AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(m, ruta);
            return m;
        }
    }
}
