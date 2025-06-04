Shader "FI/StandardDetailWithCaustics"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        [HDR]_EmissiveColor("Emissive Color", Color) = (0,0,0,0)
        [Header(Texture)]
        _MainTex("Diffuse", 2D) = "white" {}
        _DetailTex("Detail Diffuse", 2D) = "white" {}
        _DetailAmount("Detail Amount", Range(0,1)) = 1.0
        [Header(Normals)]
        _NormalTex("Normal", 2D) = "bump" {}
        _NormalDetailTex("Detail Normal", 2D) = "bump" {}
        _NormalIntensity("Normal Intensity", Float) = 1.0
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0

        [Header(Caustics)] _CausticsIntensity("Caustics Intensity", float) = 1.0
        _CausticsScale("Caustics Scale", float) = 1.0
    }
        SubShader
        {
            Tags { "RenderType" = "Opaque" }
            LOD 200

            CGPROGRAM
            #include "FIUtils.cginc"
            #pragma surface surf Standard fullforwardshadows vertex:vert

            // Use shader model 3.0 target, to get nicer looking lighting
            #pragma target 3.0

            half _NormalIntensity;
            sampler2D _MainTex;
            sampler2D _DetailTex;
            sampler2D _NormalTex;
            sampler2D _NormalDetailTex;

            struct Input
            {
                float2 uv_MainTex;
                float2 uv2_DetailTex;
                float2 uv3_NormalTex;
                float2 uv4_NormalDetailTex;
                float3 worldPos;
                float3 wNormal;
            };

            void vert(inout appdata_full v, out Input o) {
                UNITY_INITIALIZE_OUTPUT(Input, o);
                o.wNormal = UnityObjectToWorldNormal(v.normal);
            }

            half _DetailAmount;
            half _Glossiness;
            half _Metallic;
            fixed4 _Color;
            half4 _EmissiveColor;

            // Caustics
            float _CausticsIntensity, _CausticsScale;

            // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
            // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
            // #pragma instancing_options assumeuniformscaling
            UNITY_INSTANCING_BUFFER_START(Props)
                // put more per-instance properties here
            UNITY_INSTANCING_BUFFER_END(Props)

            void surf(Input IN, inout SurfaceOutputStandard o)
            {
                float2 uv = IN.uv_MainTex;
                float3 worldPos = IN.worldPos;
                float3 worldNormal = IN.wNormal;

                half4 baseColor = tex2D(_MainTex, IN.uv_MainTex);
                half4 detail  = tex2D(_DetailTex, IN.uv2_DetailTex);

                fixed4 c = lerp(baseColor, (detail + 0.5) * baseColor, _DetailAmount) * _Color;
                o.Albedo = c.rgb;

                half3 baseNormal = UnpackScaleNormal(tex2D(_NormalTex, IN.uv3_NormalTex), _NormalIntensity);
                half3 detailNormal = UnpackScaleNormal(tex2D(_NormalDetailTex, IN.uv4_NormalDetailTex), _NormalIntensity);

                fixed3 normal = BlendNormals(baseNormal, detailNormal);
                o.Normal = normal;

                o.Emission = GetCaustics(worldPos.xz, worldNormal, _CausticsScale, _CausticsIntensity) + _EmissiveColor;

                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;
                o.Alpha = c.a;
            }
            ENDCG
        }
            FallBack "Diffuse"
}
