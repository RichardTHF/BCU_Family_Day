#ifndef FI_UTILS
#define FI_UTILS

sampler2D _FlowMap, _CausticsMap;

float _FlowMapIntensity, _FlowMapScale_1, _FlowMapScale_2,
            _FlowMapScale_3, _CausticsScale_1, _CausticsScale_2,
            _CausticsScale_3, _CausticsSpeed_1, _CausticsSpeed_2,
            _CausticsSpeed_3, _CausticsWave1_Multiply, _GlobalScale, 
			_GlobalSpeed;

// Wind Movement
float _WindIntensity, _WindWeight, _WindSpeed, _WindScale, _WindMaskInvert;
sampler2D _WindGradientTex;

// From Valve's Lab Renderer, Copyright (c) Valve Corporation, All rights reserved. 
float3 Vec3TsToWs(float3 vVectorTs, float3 vNormalWs, float3 vTangentUWs, float3 vTangentVWs)
{
    float3 vVectorWs;
    vVectorWs.xyz = vVectorTs.x * vTangentUWs.xyz;
    vVectorWs.xyz += vVectorTs.y * vTangentVWs.xyz;
    vVectorWs.xyz += vVectorTs.z * vNormalWs.xyz;
    return vVectorWs.xyz; // Return without normalizing
}

// From Valve's Lab Renderer, Copyright (c) Valve Corporation, All rights reserved. 
float3 Vec3TsToWsNormalized(float3 vVectorTs, float3 vNormalWs, float3 vTangentUWs, float3 vTangentVWs)
{
    return normalize(Vec3TsToWs(vVectorTs.xyz, vNormalWs.xyz, vTangentUWs.xyz, vTangentVWs.xyz));
}

float ThreePointLevels(float col, float2 uv, float black, float middle, float white)
{
	float col2 = 1 - col;
	float middle2 = 1 - middle;

	float alpha1 = saturate(col2 / middle2);
	float alpha2 = saturate(col / middle);

	float alpha3 = floor(alpha2);

	float col11 = lerp(black, middle, alpha2);
	float col12 = lerp(white, middle, alpha1);

	return lerp(col11, col12, alpha3);
}

float GetCaustics(float2 uv, float3 worldNormal, float localScale, float localIntensity)
{
    float2 uv1 = _CausticsScale_1 * localScale * _GlobalScale * lerp(uv, tex2D(_FlowMap, uv * _FlowMapScale_1 * _GlobalScale * localScale).rg, _FlowMapIntensity);
    float2 uv2 = _CausticsScale_2 * localScale * _GlobalScale * lerp(uv, tex2D(_FlowMap, uv * _FlowMapScale_2 * _GlobalScale * localScale).rg, _FlowMapIntensity);
    float2 uv3 = _CausticsScale_3 * localScale * _GlobalScale * lerp(uv, tex2D(_FlowMap, uv * _FlowMapScale_3 * _GlobalScale * localScale).rg, _FlowMapIntensity);

	float waves_1a = tex2D(_CausticsMap, uv1 + _Time.y * _CausticsSpeed_1 * _GlobalSpeed + .1);
	float waves_2a = tex2D(_CausticsMap, uv2 + _Time.y * _CausticsSpeed_2 * _GlobalSpeed + .2);
    float waves_3a = tex2D(_CausticsMap, uv3 + _Time.y * _CausticsSpeed_3 * _GlobalSpeed + .3);
	
    float waves_a = localIntensity * _CausticsWave1_Multiply * (waves_1a + waves_2a) * waves_3a;
		
	// Normal Mask
	float mask = (worldNormal.y + 0.7);
	mask = saturate(mask * mask);
	
	return waves_a * mask;
}

float3 RotateAboutAxis(float4 NormalizedRotationAxisAndAngle, float3 PositionOnAxis, float3 Position)
{
    float3 ClosestPointOnAxis = PositionOnAxis + NormalizedRotationAxisAndAngle.xyz * dot(NormalizedRotationAxisAndAngle.xyz, Position - PositionOnAxis);
    float3 UAxis = Position - ClosestPointOnAxis;
    float3 VAxis = cross(NormalizedRotationAxisAndAngle.xyz, UAxis);
    float CosAngle;
    float SinAngle;
    sincos(NormalizedRotationAxisAndAngle.w, SinAngle, CosAngle);
    float3 R = UAxis * CosAngle + VAxis * SinAngle;
    float3 RotatedPosition = ClosestPointOnAxis + R;
    return RotatedPosition - Position;
}

float3 GrassWindMovement(float3 WorldPosition, float3 AdditionalWPO)
{
    float3 NormalizedRotationAxis = cross(normalize(float3(0, 1, 0)), float3(0, 0, 1));
    
    float3 Section01 = WorldPosition / _WindScale;
    float Section00 = _Time.y * _WindSpeed * -0.5;
    float3 Section02 = normalize(float3(0, 1, 0)) * Section00;
    Section02 += 2 * WorldPosition;
    Section02 = frac(Section02 + 0.5) * 2.0 - 1.0;
    Section02 = abs(Section02);
    float3 Section04 = Section02 * Section02 * (3 - (Section02 * 2));
    Section01 = frac(Section01 + 0.5) * 2.0 - 1.0;
    float3 Section10 = abs(Section01);
    float3 Section11 = (3 - (Section10 * 2)) * Section10 * Section10;
    float RotationAngle = dot(Section04, normalize(float3(0, 1, 0))) + distance(Section11, 0);
    
    float3 Position = AdditionalWPO;
    float3 PivotPoint = AdditionalWPO + float3(0, 0, -10) * 0.02;
    
    float3 Result = RotateAboutAxis(float4(NormalizedRotationAxis, RotationAngle), Position, PivotPoint);
    Result = (Result * _WindWeight * _WindIntensity) + AdditionalWPO;

    return Result;
}

float4 GradientMap_Multi(float GreyscaleToGradient, float Index, sampler2D Gradient, float NumberOfGradients)
{
    NumberOfGradients = 1.0 / ceil(NumberOfGradients);
    Index = ceil(Index);
    float Section01 = NumberOfGradients * Index;
    Section01 += 0.5 * NumberOfGradients;
    float2 uv = float2(GreyscaleToGradient, Section01);
    
    return tex2Dlod(Gradient, float4(uv, 0, 0));
}

void MS_MorphTargets(float2 uv, float MorphAnimation, sampler2D MorphNormal, sampler2D MorphTexture, float NumberOfMorphTargets, out float3 WorldPositionOffset)
{
    float s = MorphAnimation * (NumberOfMorphTargets - 1.0);
    float s00 = floor(s);
    float s01 = s00 + 1.0;
    float s02 = frac(s);
    
    float4 Gradient01 = GradientMap_Multi(uv.r, s01, MorphTexture, NumberOfMorphTargets);
    float4 Gradient02 = GradientMap_Multi(uv.r, s00, MorphTexture, NumberOfMorphTargets);

    float3 vertexOffset = lerp(Gradient02, Gradient01, s02);

    WorldPositionOffset = vertexOffset;
}

#endif