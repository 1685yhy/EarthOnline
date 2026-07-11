Shader "Hidden/EarthOnline/PostProcess"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            sampler2D _MainTex;
            float _BloomThreshold, _BloomIntensity;
            float4 _BloomColor;
            float _Saturation, _Contrast;
            float4 _ColorFilter;
            float _VignetteIntensity;
            float4 _VignetteColor;
            float _MotionBlur;

            v2f vert (appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                // Bloom (simple luminance threshold)
                float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                float bloom = max(0, lum - _BloomThreshold) * _BloomIntensity;
                col.rgb += bloom * _BloomColor.rgb;

                // Saturation
                float gray = dot(col.rgb, float3(0.299, 0.587, 0.114));
                col.rgb = lerp(gray, col.rgb, _Saturation);

                // Contrast
                col.rgb = (col.rgb - 0.5) * _Contrast + 0.5;

                // Color Filter (warm tone for cultivation world)
                col.rgb *= _ColorFilter.rgb;

                // Vignette
                float2 uv_center = i.uv - 0.5;
                float vig = 1.0 - dot(uv_center, uv_center) * 4.0;
                vig = saturate(vig);
                col.rgb = lerp(_VignetteColor.rgb, col.rgb, vig * (1-_VignetteIntensity) + (1-_VignetteIntensity));

                return col;
            }
            ENDCG
        }
    }
}
