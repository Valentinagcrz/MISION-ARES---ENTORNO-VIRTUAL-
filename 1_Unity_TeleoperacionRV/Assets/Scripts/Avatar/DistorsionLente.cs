using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// Pre-distorsión de barril por ojo (pipeline integrado de Unity).
    /// Las lentes del Cardboard producen distorsión de cojín; al aplicar la inversa
    /// (barril) la imagen se ve recta a través del visor.
    ///   r' = r · (1 + k1·r² + k2·r⁴) / escala
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DistorsionLente : MonoBehaviour
    {
        public Shader shader;
        public float k1 = 0.22f;
        public float k2 = 0.08f;

        Material mat;

        void OnRenderImage(RenderTexture origen, RenderTexture destino)
        {
            if (mat == null)
            {
                var s = shader ? shader : Shader.Find("Hidden/TeleoperacionRV/DistorsionLente");
                if (s == null || !s.isSupported) { Graphics.Blit(origen, destino); return; }
                mat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
            }
            mat.SetFloat("_K1", k1);
            mat.SetFloat("_K2", k2);
            mat.SetFloat("_Escala", 1f + k1 + k2);
            Graphics.Blit(origen, destino, mat);
        }

        void OnDestroy()
        {
            if (mat) Destroy(mat);
        }
    }
}
