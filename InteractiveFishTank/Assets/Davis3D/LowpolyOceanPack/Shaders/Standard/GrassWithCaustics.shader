Shader "FI/GrassWithCaustics"
{
    Properties
    {
        [Header(Color)]
        _ColorPrimary ("Primary Color", Color) = (1,1,1,1)
        _ColorSecondary ("Secondary Color", Color) = (1,1,1,1)
        _GradientTex ("Color Gradient", 2D) = "white" {}
        _GradientPower("GradientPower", Float) = 1.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        [Header(Glow Settings)]
        _GlowTexture ("Glow Texture", 2D) = "white" {}
        [HDR]_GlowColor ("Glow Color", Color) = (0,0,0,1)
        _GlowIntensity ("Glow Intensity", Float) = 0.0

        [Header(Wind Movement)] _WindIntensity("Wind Movement", float) = 1.0
        _WindWeight("Wind Weight", float) = 1.0
        _WindSpeed("Wind Speed", float) = 0.1
        _WindScale("Wind Scale", float) = 1.0
        _WindMaskInvert("Wind Mask Invert", float) = 1.0
        _WindGradient("Wind Gradient (R)", 2D) = "white" {}

        [Header(Caustics)] _CausticsIntensity("Caustics Intensity", float) = 1.0
        _CausticsScale("Caustics Scale", float) = 1.0
    }
    SubShader
    {
		Tags{ "RenderType" = "Opaque" "Queue" = "Transparent" }
		LOD 200

        Cull Off

        CGPROGRAM

        #include "FIUtils.cginc"

        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert alphatest:_Cutoff

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        half _NormalIntensity;
        sampler2D _GradientTex;
        sampler2D _WindGradient;
        sampler2D _GlowTexture;

        struct Input
        {
            float2 uv_GradientTex;
            float2 uv2_GlowTexture;
            float3 worldPos;
            float3 wNormal;
        };

        void vert(inout appdata_full v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.wNormal = UnityObjectToWorldNormal(v.normal);

            float3 wPos = mul(unity_ObjectToWorld, v.vertex);
            float3 grassMov = GrassWindMovement(wPos, 0);
            float grad = tex2Dlod(_WindGradient, float4(v.texcoord.xy, 0, 0));
            grassMov = lerp(0, grassMov, grad);
            v.vertex.xyz += grassMov;
        }

        half _GradientPower;
        half _Glossiness;
        half _Metallic;
        fixed4 _ColorPrimary;
        fixed4 _ColorSecondary;
        float3 _GlowColor;
        float _GlowIntensity;

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
            float2 uv = IN.uv_GradientTex;
            float3 worldPos = IN.worldPos;
            float3 worldNormal = IN.wNormal;

            fixed4 c = lerp(_ColorPrimary, _ColorSecondary, clamp(pow(tex2D(_GradientTex, uv), _GradientPower), 0, 1));
            o.Albedo = c.rgb;

            float3 glow = tex2D(_GlowTexture, IN.uv2_GlowTexture) * _GlowColor * _GlowIntensity;
            o.Emission = GetCaustics(worldPos.xz, worldNormal, _CausticsScale, _CausticsIntensity) + glow;

            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;

            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
