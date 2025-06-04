Shader "FI/GodRays"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _GlowIntensity("Glow Intensity", Range(0.0, 1.0)) = 1.0
        _FresnelExponent("Fresnel Exponent", Float) = 5.0
        _MaxDistance("Max Distance", Float) = 100.0

        _Scale_A("Scale A", Float) = 500.0
        _Scale_B("Scale B", Float) = 500.0
        _Panner_A("Panner A", Float) = 25.0
        _Panner_B("Panner B", Float) = 5.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Blend One One
            Cull Off
            ZWrite Off

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            fixed _GlowIntensity;
            half _FresnelExponent, _MaxDistance;
            half _Panner_A, _Panner_B, _Scale_A, _Scale_B;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.z = normalize(ObjSpaceViewDir(v.vertex));
                o.uv.z = pow(1 - saturate(dot(v.normal, o.uv.z)), _FresnelExponent);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.uv.w = 1.0 - saturate(length(_WorldSpaceCameraPos.xyz - worldPos) / _MaxDistance); 

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half mask = (sin(i.uv.x * _Scale_A + _Panner_A * _Time.y) + 0.8) * (sin(i.uv.x * _Scale_B + _Panner_B * _Time.y) + 0.8);
                fixed4 col = tex2D(_MainTex, i.uv) * _GlowIntensity * mask * i.uv.z * i.uv.w;

                return col;
            }
            ENDCG
        }
    }
}
