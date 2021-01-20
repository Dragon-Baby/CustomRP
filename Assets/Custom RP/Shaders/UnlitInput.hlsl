#ifndef CUSTOM_UNLIT_INPUT_INCLUDED
#define CUSTOM_UNLIT_INPUT_INCLUDED

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

float2 TransformBaseUV(float2 baseUV);
float4 GetBase(float2 baseUV);
float GetCutoff(float2 baseUV);
float3 GetEmission(float2 baseUV);
float GetFresnel(float2 baseUV);



float2 TransformBaseUV(float2 baseUV)
{
	float4 baseST = INPUT_PROP(_BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase(float2 baseUV)
{
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
	float4 color = INPUT_PROP(_BaseColor);
	return map * color;
}

float GetCutoff(float2 baseUV)
{
	return INPUT_PROP(_Cutoff);
}

float3 GetEmission(float2 baseUV)
{
	return GetBase(baseUV).rgb;
}

float GetFresnel(float2 baseUV)
{
	return 0.0;
}

#endif