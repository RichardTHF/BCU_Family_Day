Shader "FI/TerrainWithCaustics"
{
    Properties
    {
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        _SplatTex("Splatmap (RGBA)", 2D) = "white" {}

        _Scale01("Map 01 Scale", float) = 1.0
        _Scale02("Map 02 Scale", float) = 1.0
        _Scale03("Map 03 Scale", float) = 1.0
        _Scale04("Map 04 Scale", float) = 1.0

        _Albedo01("Albedo01 (RGB)", 2D) = "white" {}
        _Albedo02("Albedo02 (RGB)", 2D) = "white" {}
        _Albedo03("Albedo03 (RGB)", 2D) = "white" {}
        _Albedo04("Albedo04 (RGB)", 2D) = "white" {}

        _NormalIntensity01("Normal Intensity 01", float) = 1.0
        _NormalIntensity02("Normal Intensity 02", float) = 1.0
        _NormalIntensity03("Normal Intensity 03", float) = 1.0
        _NormalIntensity04("Normal Intensity 04", float) = 1.0

        _Normal01("Normal01 (RGB)", 2D) = "bump" {}
        _Normal02("Normal02 (RGB)", 2D) = "bump" {}
        _Normal03("Normal03 (RGB)", 2D) = "bump" {}
        _Normal04("Normal04 (RGB)", 2D) = "bump" {}

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

        half _NormalIntensity;
        sampler2D _SplatTex;

        float _Scale01, _Scale02, _Scale03, _Scale04;

        sampler2D _Albedo01, _Albedo02, _Albedo03, _Albedo04;
        sampler2D _Normal01, _Normal02, _Normal03, _Normal04;

        float _NormalIntensity01, _NormalIntensity02, _NormalIntensity03, _NormalIntensity04;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 wNormal;
            float2 uv_SplatTex;
        };

        void vert(inout appdata_full v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.wNormal = UnityObjectToWorldNormal(v.normal);
        }

        half _Glossiness;
        half _Metallic;

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

            float4 mask = tex2D(_SplatTex, uv);

            fixed4 c1 = tex2D(_Albedo01, uv * _Scale01) * mask.r;
            fixed4 c2 = tex2D(_Albedo02, uv * _Scale02) * mask.g;
            fixed4 c3 = tex2D(_Albedo03, uv * _Scale03) * mask.b;
            fixed4 c4 = tex2D(_Albedo04, uv * _Scale04) * mask.a;
            
            o.Albedo = saturate(c1 + c2 + c3 + c4);
            //o.Albedo = mask.b;

            fixed3 n1 = UnpackScaleNormal(tex2D(_Normal01, uv * _Scale01), _NormalIntensity01) * mask.r;
            fixed3 n2 = UnpackScaleNormal(tex2D(_Normal02, uv * _Scale02), _NormalIntensity02) * mask.g;
            fixed3 n3 = UnpackScaleNormal(tex2D(_Normal03, uv * _Scale03), _NormalIntensity03) * mask.b;
            fixed3 n4 = UnpackScaleNormal(tex2D(_Normal04, uv * _Scale04), _NormalIntensity04) * mask.a;
            o.Normal = n1 + n2 + n3 + n4;

            o.Emission = GetCaustics(worldPos.xz, worldNormal, _CausticsScale, _CausticsIntensity);

            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
