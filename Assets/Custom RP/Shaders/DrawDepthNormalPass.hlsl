#ifndef CUSTOM_DRAW_GBUFFER_PASS_INCLUDED
#define CUSTOM_DRAW_GBUFFER_PASS_INCLUDED

struct Attributes
{
	float3 positionOS : POSITION;
	float3 normalOS : NORMAL;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS : SV_POSITION;
	float4 normalDepth : TEXCOORD0;
	float3 positionVS : TEXCOORD1;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct MRT
{
	float4 depthNomral : SV_TARGET0;
	float4 positionVS : SV_TARGET1;
};

Varyings DrawDepthNormalPassVertex(Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	output.positionCS = TransformObjectToHClip(input.positionOS);
	output.normalDepth.xyz = normalize(mul((float3x3)unity_MatrixITMV, input.normalOS));
	output.normalDepth.w = -(TransformWorldToView(input.positionOS).z * _ProjectionParams.w);
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionVS = TransformWorldToView(positionWS);
	return output;
}

MRT DrawDepthNormalPassFragment(Varyings input)
{
	MRT output;
	UNITY_SETUP_INSTANCE_ID(input);
	float4 normalDepth = input.normalDepth;
	normalDepth.xyz = normalDepth.xyz * 0.5 + 0.5;
	output.depthNomral = normalDepth;
	float3 positionVS = input.positionVS;
	output.positionVS = float4(positionVS, 1.0);
	return output;
}

#endif