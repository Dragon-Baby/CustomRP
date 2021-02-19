#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection

#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
	#define SHADOWS_SHADOWMASK
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

float Square(float v);
float DistanceSquared(float3 pA, float3 pB);
void ClipLOD(float2 positionCS, float fade);
float3 DecodeNormal(float4 sample, float scale);
float3 NormalTangentToWorld(float3 normalTS, float3 normalWS, float4 tangentWS);


float Square(float v)
{
	return v * v;
}

float DistanceSquared(float3 pA, float3 pB)
{
	return dot(pA - pB, pA - pB);
}

void ClipLOD(float2 positionCS, float fade)
{
#if defined(LOD_FADE_CROSSFADE)
	float dither = InterleavedGradientNoise(positionCS.xy, 0);
	clip(fade + (fade < 0.0 ? dither : -dither));
#endif
}

float3 DecodeNormal(float4 sample, float scale)
{
#if defined(UNITY_NO_DXT5nm)
	    return UnpackNormalRGB(sample, scale);
#else
	return UnpackNormalmapRGorAG(sample, scale);
#endif
}

float3 NormalTangentToWorld(float3 normalTS, float3 normalWS, float4 tangentWS)
{
	float3x3 tangentToWorld =
		CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
	return TransformTangentToWorld(normalTS, tangentToWorld);
}

float2 EncodeViewNormal(float3 n)
{
	float kScale = 1.7777;
	float2 enc;
	enc = n.xy / (n.z + 1);
	enc /= kScale;
	enc = enc * 0.5 + 0.5;
	return enc;
}

float2 EncodeFloatRG(float v)
{
	float2 kEncodeMul = float2(1.0, 255.0);
	float kEncodeBit = 1.0 / 255.0;
	float2 enc = kEncodeMul * v;
	enc = frac(enc);
	enc.x -= enc.y * kEncodeBit;
	return enc;
}

float4 EncodeDepthNormal(float depth, float3 normal)
{
	float4 enc;
	enc.xy = EncodeViewNormal(normal);
	enc.zw = EncodeFloatRG(depth);
	return enc;
}

float DecodeFloatRG(float2 enc)
{
	float2 kDecodeDot = float2(1.0, 1 / 255.0);
	return dot(enc, kDecodeDot);
}

float3 DecodeViewNormal(float4 enc4)
{
	float kScale = 1.7777;
	float3 nn = enc4.xyz * float3(2 * kScale, 2 * kScale, 0) + float3(-kScale, -kScale, 1);
	float g = 2.0 / dot(nn.xyz, nn.xyz);
	float3 n;
	n.xy = g * nn.xy;
	n.z = g - 1;
	return n;
}

#endif