Shader "Hidden/TeleoperacionRV/DistorsionLente"
{
    Properties
    {
        _MainTex ("Imagen", 2D) = "white" {}
        _K1 ("K1", Float) = 0.22
        _K2 ("K2", Float) = 0.08
        _Escala ("Escala", Float) = 1.3
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _K1;
            float _K2;
            float _Escala;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 c = i.uv * 2.0 - 1.0;          // centro del ojo = (0,0)
                float r2 = dot(c, c);
                float f = (1.0 + _K1 * r2 + _K2 * r2 * r2) / _Escala;
                float2 uv = c * f * 0.5 + 0.5;
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return fixed4(0, 0, 0, 1);
                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
    Fallback Off
}
