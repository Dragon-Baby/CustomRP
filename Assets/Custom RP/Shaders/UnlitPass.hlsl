#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED


struct Attributes
{
	float4 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS : SV_POSITION;
	float2 baseUV : VAR_BASE_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};


Varyings UnlitPassVertex(Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
	output.positionCS = TransformWorldToHClip(positionWS);
	
	output.baseUV = TransformBaseUV(input.baseUV);
	return output;
}

float4 UnlitPassFragment(Varyings input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	float4 base = GetBase(input.baseUV);
#if defined(_CLIPPING)
	clip(base.a - GetCutoff(input.baseUV));
#endif
	base.rgb += GetEmission(input.baseUV);
	return base;
}

#endif