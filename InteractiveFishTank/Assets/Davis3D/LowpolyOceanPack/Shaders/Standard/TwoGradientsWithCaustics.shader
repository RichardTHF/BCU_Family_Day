Shader "FI/TwoGradientsWithCaustics"
{
    Properties
    {
        _Color ("Color 01", Color) = (1,1,1,1)
        _Color2 ("Color 02", Color) = (1,1,1,1)
        _MainTex ("Mask", 2D) = "white" {}
        [HDR]_EmissiveColor("Emissive Color", Color) = (0,0,0,0)
        _NormalTex("Normal", 2D) = "bump" {}
        _NormalDetailTex("Normal Detailed", 2D) = "bump" {}
        _NormalIntensity("Normal Intensity", Float) = 1.0
        _NormalDetailIntensity("Normal Detail Intensity", Float) = 1.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        [Header(Caustics)] _CausticsIntensity("Caustics Intensity", float) = 1.0
        _CausticsScale("Caustics Scale", float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM

        #include "FIUtils.cginc"

        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        half _NormalIntensity, _NormalDetailIntensity;
        sampler2D _MainTex;
        sampler2D _NormalTex, _NormalDetailTex;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_NormalDetailTex;
            float3 worldPos;
            float3 wNormal;
        };

        void vert(inout appdata_full v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.wNormal = UnityObjectToWorldNormal(v.normal);
        }

        half _Glossiness;
        half _Metallic;
        fixed4 _Color, _Color2;
        float _EmissiveColor;

        // Caustics
        float _CausticsIntensity, _CausticsScale;
        
        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_MainTex;
            float3 worldPos = IN.worldPos;
            float3 worldNormal = IN.wNormal;

            fixed gradient = saturate(tex2D(_MainTex, IN.uv_MainTex));
            fixed4 c1 = gradient * _Color;
            fixed4 c2 = (1 - gradient) * _Color2;
            o.Albedo = (c1 + c2).rgb;

            fixed3 normal = UnpackScaleNormal(tex2D(_NormalTex, IN.uv_MainTex), _NormalIntensity);
            fixed3 normal_detail = UnpackScaleNormal(tex2D(_NormalDetailTex, IN.uv_NormalDetailTex), _NormalDetailIntensity);
            o.Normal = BlendNormals(normal, normal_detail);

            o.Emission = GetCaustics(worldPos.xz, worldNormal, _CausticsScale, _CausticsIntensity) + _EmissiveColor;

            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
