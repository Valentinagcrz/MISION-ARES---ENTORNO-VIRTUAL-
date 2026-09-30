// Cielo de Marte (skybox procedural): degradado "caramelo" en el horizonte,
// más oscuro en el cénit, y un sol pequeño con halo.
Shader "TeleoperacionRV/CieloMarte"
{
    Properties
    {
        _ColorHorizonte ("Horizonte", Color) = (0.86, 0.60, 0.42, 1)
        _ColorCenit     ("Cénit", Color)     = (0.50, 0.30, 0.20, 1)
        _ColorSuelo     ("Bajo el horizonte", Color) = (0.45, 0.27, 0.17, 1)
        _ColorSol       ("Sol", Color)       = (1.0, 0.95, 0.85, 1)
        _DirSol         ("Dirección al sol", Vector) = (0.3, 0.55, 0.6, 0)
        _TamSol         ("Tamaño del sol", Range(0.0001, 0.01)) = 0.0012
        _Halo           ("Halo", Range(0, 2)) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ColorHorizonte, _ColorCenit, _ColorSuelo, _ColorSol;
            float4 _DirSol;
            float _TamSol, _Halo;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float3 col = y >= 0 ? lerp(_ColorHorizonte.rgb, _ColorCenit.rgb, pow(saturate(y), 0.55))
                                    : lerp(_ColorHorizonte.rgb, _ColorSuelo.rgb, saturate(-y * 5));
                float s = dot(d, normalize(_DirSol.xyz));
                float disco = smoothstep(1 - _TamSol, 1 - _TamSol * 0.6, s);
                float halo = pow(saturate(s), 90) * _Halo + pow(saturate(s), 7) * 0.12;
                col += _ColorSol.rgb * (disco * 2 + halo);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
