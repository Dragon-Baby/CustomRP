#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_CameraDepthNormalTexture);
TEXTURE2D(_CameraPositionVSTexture);
TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);
SAMPLER(sampler_linear_clamp);
TEXTURE2D(_ColorGradingLUT);

TEXTURE2D(_SSAONoiseTex);
SAMPLER(sampler_SSAONoiseTex);

struct Varyings
{
	float4 positionCS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};

float4 _PostFXSource_TexelSize;
int _SSAOKernelSize;
float _SSAOKernelRadius;
float _SSAOStrength;
float4 _SSAOKernels[64];
float2 _SSAONoiseScale;
bool _BloomBicubicUpsampling;
float _BloomIntensity;
float4 _BloomThreshold;
float4 _ColorAdjustments;
float4 _ColorFilter;
float4 _WhiteBalance;
float4 _SplitToningShadows, _SplitToningHighlights;
float4 _ChannelMixerRed, _ChannelMixerGreen, _ChannelMixerBlue;
float4 _SMHShadows, _SMHMidtones, _SMHHighlights, _SMHRange;
float4 _ColorGradingLUTParameters;
bool _ColorGradingLUTInLogC;

float4 GetSource(float2 screenUV);
float4 GetSource2(float2 screenUV);
float4 GetSourceTexelSize();
float4 GetSourceBicubic(float2 screenUV);
float3 ApplyBloomThreshold(float3 color);
float Luminance(float3 color, bool useACES);
float3 GetColorGradedLUT(float2 uv, bool useACES = false);
float3 ColorGradePostExposure(float3 color);
float3 ColorGradeWhiteBalance(float3 color);
float3 ColorGradingContrast(float3 color, bool useACES);
float3 ColorGradeColorFilter(float3 color);
float3 ColorGradeSplitToning(float3 color, bool useACES);
float3 ColorGradingChannelMixer(float3 color);
float3 ColorGradingShadowsMidtonesHighlights(float3 color, bool useACES);
float3 ColorGradingHueShift(float3 color);
float3 ColorGradingSaturation(float3 color, bool useACES);
float3 ColorGrade(float3 color, bool useACES = false);
float3 ApplyColorGradingLUT(float3 color);


/***************************************/

float4 GetSourceTexelSize()
{
	return _PostFXSource_TexelSize;
}

float4 GetSource(float2 screenUV)
{
	return SAMPLE_TEXTURE2D(_PostFXSource, sampler_linear_clamp, screenUV);
}

float4 GetSourceBicubic(float2 screenUV)
{
	return SampleTexture2DBicubic(TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), screenUV, 
		_PostFXSource_TexelSize.zwxy, 1.0, 0.0);
}

float4 GetSource2(float2 screenUV)
{
	return SAMPLE_TEXTURE2D(_PostFXSource2, sampler_linear_clamp, screenUV);
}

float3 ApplyBloomThreshold(float3 color)
{
	float brightness = Max3(color.r, color.g, color.b);
	float soft = brightness + _BloomThreshold.y;
	soft = clamp(soft, 0.0, _BloomThreshold.z);
	soft = soft * soft * _BloomThreshold.w;
	float contribution = max(soft, brightness - _BloomThreshold.x);
	contribution /= max(brightness, 0.00001);
	return color * contribution;
}

float Luminance(float3 color, bool useACES)
{
	return useACES ? AcesLuminance(color) : Luminance(color);
}

float3 ColorGradePostExposure(float3 color)
{
	return color * _ColorAdjustments.x;
}

float3 ColorGradeWhiteBalance(float3 color)
{
	color = LinearToLMS(color);
	color *= _WhiteBalance.rgb;
	return LMSToLinear(color);
}

float3 ColorGradingContrast(float3 color, bool useACES)
{
	color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) : LinearToLogC(color);
	color = (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
	return useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) : LogCToLinear(color);
}

float3 ColorGradeColorFilter(float3 color)
{
	return color * _ColorFilter.rgb;
}

float3 ColorGradingHueShift(float3 color)
{
	color = RgbToHsv(color);
	float hue = color.x + _ColorAdjustments.z;
	color.x = RotateHue(hue, 0.0, 1.0);
	return HsvToRgb(color);
}

float3 ColorGradingSaturation(float3 color, bool useACES)
{
	float luminance = Luminance(color, useACES);
	return (color - luminance) * _ColorAdjustments.w + luminance;
}

float3 ColorGradeSplitToning(float3 color, bool useACES)
{
	color = PositivePow(color, 1.0 / 2.2);
	float t = saturate(Luminance(saturate(color), useACES) + _SplitToningShadows.w);
	float3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1.0 - t);
	float3 highlights = lerp(0.5, _SplitToningHighlights.rgb, t);
	color = SoftLight(color, shadows);
	color = SoftLight(color, highlights);
	return PositivePow(color, 2.2);
}

float3 ColorGradingChannelMixer(float3 color)
{
	return mul(float3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb), color);
}

float3 ColorGradingShadowsMidtonesHighlights(float3 color, bool useACES)
{
	float luminance = Luminance(color, useACES);
	float shadowsWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
	float highlightsWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
	float midtonesWeight = 1.0 - shadowsWeight - highlightsWeight;
	return color * _SMHShadows.rgb * shadowsWeight + color * _SMHMidtones.rgb * midtonesWeight + 
		color * _SMHHighlights.rgb * highlightsWeight;
}

float3 ColorGrade(float3 color, bool useACES)
{
	color = ColorGradePostExposure(color);
	color = ColorGradeWhiteBalance(color);
	color = ColorGradingContrast(color, useACES);
	color = ColorGradeColorFilter(color);
	color = max(color, 0.0);
	color = ColorGradeSplitToning(color, useACES);
	color = ColorGradingChannelMixer(color);
	color = max(color, 0.0);
	color = ColorGradingShadowsMidtonesHighlights(color, useACES);
	color = ColorGradingHueShift(color);
	color = ColorGradingSaturation(color, useACES);
	return max(useACES ? ACEScg_to_ACES(color) : color, 0.0);
}

float3 GetColorGradedLUT(float2 uv, bool useACES)
{
	float3 color = GetLutStripValue(uv, _ColorGradingLUTParameters);
	return ColorGrade(_ColorGradingLUTInLogC ? LogCToLinear(color) : color, useACES);
}

float3 ApplyColorGradingLUT(float3 color)
{
	return ApplyLut2D(TEXTURE2D_ARGS(_ColorGradingLUT, sampler_linear_clamp),
		saturate(_ColorGradingLUTInLogC ? LinearToLogC(color) : color), _ColorGradingLUTParameters.xyz);
}

/******************************************************/

Varyings DefaultPassVertex(uint vertexID : SV_VertexID)
{
	Varyings output;
	output.positionCS = float4(vertexID <= 1 ? -1.0 : 3.0, vertexID == 1 ? 3.0 : -1.0, 0.0, 1.0);
	output.screenUV = float2(vertexID <= 1 ? 0.0 : 2.0, vertexID == 1 ? 2.0 : 0.0);
	if (_ProjectionParams.x < 0.0)
	{
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}

float4 SSAOPassFragment(Varyings input) : SV_TARGET
{
	float4 depthNormal = SAMPLE_TEXTURE2D(_CameraDepthNormalTexture, sampler_linear_clamp, input.screenUV);
	float3 normal = normalize((depthNormal.xyz - 0.5) * 2);
	float depth = depthNormal.w;
	float3 random = SAMPLE_TEXTURE2D(_SSAONoiseTex, sampler_linear_clamp, input.screenUV * _SSAONoiseScale).rgb;
	float3 tangent = normalize(random - normal * dot(random, normal));
	float3 bitangent = cross(normal, tangent);
	float3x3 TBN = float3x3(tangent, bitangent, normal);
	float occlusion = 0.0;
	float3 positionVS = SAMPLE_TEXTURE2D(_CameraPositionVSTexture, sampler_linear_clamp, input.screenUV).xyz;
	for (int i = 0; i < _SSAOKernelSize; i++)
	{
		float3 sample = mul(TBN, _SSAOKernels[i].xyz);
		sample = positionVS + sample * _SSAOKernelRadius;
		float4 offset = float4(sample, 1.0);
		offset = mul(glstate_matrix_projection, offset);
		offset.xyz /= offset.w;
		offset.xyz = offset.xyz * 0.5 + 0.5;
		float sampleDepth = -SAMPLE_TEXTURE2D(_CameraDepthNormalTexture, sampler_linear_clamp, offset.xy).w;
		float rangeCheck = smoothstep(0.0, 1.0, _SSAOKernelRadius / abs(depth - sampleDepth));
		occlusion += (sampleDepth >= sample.z ? 1.0 : 0.0) * rangeCheck;
	}
	occlusion = max(0, (1.0 - (occlusion / _SSAOKernelSize)));
	occlusion = pow(occlusion, _SSAOStrength);
	return float4(occlusion, 0.0f, 0.0f, 0.0f);
}

float4 SSAOBlurPassFragment(Varyings input) : SV_TARGET
{
	float2 texelSize = GetSourceTexelSize().xy;
	float result = 0.0;
	for (int x = -2; x < 2; x++)
	{
		for (int y = -2; y < 2; y++)
		{
			float2 offset = float2(float(x), float(y)) * texelSize;
			result += GetSource(input.screenUV).r;
		}
	}
	return float4(result / (4.0 * 4.0), 0.0, 0.0, 0.0);
}

float4 SSAOCombinePassFragment(Varyings input) : SV_TARGET
{
	float ao = GetSource(input.screenUV).r;
	float3 source = GetSource2(input.screenUV).rgb;
	float brightness = Max3(source.r, source.g, source.b);
	float finalAO = (brightness - 0.8) ? 1 : ao;
	source *= finalAO;
	return float4(source, 1.0);
}

float4 BloomAddPassFragment(Varyings input) : SV_TARGET
{
	float3 lowRes;
	if (_BloomBicubicUpsampling)
	{
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else
	{
		lowRes = GetSource(input.screenUV).rgb;
	}
	float4 highRes = GetSource2(input.screenUV);
	return float4(lowRes * _BloomIntensity + highRes.rgb, highRes.a);
}

float4 BloomHorizontalPassFragment(Varyings input) : SV_TARGET
{
	float3 color = 0.0;
	float offsets[] =
	{
		-4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
	};
	float weights[] =
	{
		0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		0.19459459, 0.12162162, 0.05405405, 0.01621622
	};
	for (int i = 0; i < 9; i++)
	{
		float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
		color += GetSource(input.screenUV + float2(offset, 0.0)).rgb * weights[i];
	}
	return float4(color, 1.0);
}

float4 BloomPrefilterPassFragment(Varyings input) : SV_TARGET
{
	float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
	return float4(color, 1.0);
}

float4 BloomPrefilterFirefliesPassFragment(Varyings input) : SV_TARGET
{
	float3 color = 0.0;
	float weightSum = 0.0;
	float2 offsets[] =
	{
		float2(0.0, 0.0),
		float2(-1.0, -1.0), float2(-1.0, 1.0), float2(1.0, -1.0), float2(1.0, 1.0)
	};
	for (int i = 0; i < 5; i++)
	{
		float3 c = GetSource(input.screenUV + offsets[i] * GetSourceTexelSize().xy * 2.0).rgb;
		c = ApplyBloomThreshold(c);
		float w = 1.0 / (Luminance(c) + 1.0);
		color += c * w;
		weightSum += w;
	}
	color /= weightSum;
	return float4(color, 1.0);
}

float4 BloomScatterPassFragment(Varyings input) : SV_TARGET
{
	float3 lowRes;
	if (_BloomBicubicUpsampling)
	{
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else
	{
		lowRes = GetSource(input.screenUV).rgb;
	}
	float3 highRes = GetSource2(input.screenUV).rgb;
	return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}

float4 BloomScatterFinalPassFragment(Varyings input) : SV_TARGET
{
	float3 lowRes;
	if (_BloomBicubicUpsampling)
	{
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else
	{
		lowRes = GetSource(input.screenUV).rgb;
	}
	float4 highRes = GetSource2(input.screenUV);
	lowRes += highRes.rgb - ApplyBloomThreshold(highRes.rgb);
	return float4(lerp(highRes.rgb, lowRes, _BloomIntensity), highRes.a);
}

float4 BloomVerticalPassFragment(Varyings input) : SV_TARGET
{
	float3 color = 0.0;
	float offsets[] =
	{
		-3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
	};
	float weights[] =
	{
		0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
	};
	for (int i = 0; i < 5; i++)
	{
		float offset = offsets[i] * GetSourceTexelSize().y;
		color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
	}
	return float4(color, 1.0);
}

float4 CopyPassFragment(Varyings input) : SV_TARGET
{
	return GetSource(input.screenUV);
}

float4 ColorGradingNonePassFragment(Varyings input) : SV_TARGET
{
	float3 color = GetColorGradedLUT(input.screenUV);
	return float4(color, 1.0);
}

float4 ColorGradingACESPassFragment(Varyings input) : SV_TARGET
{
	float3 color = GetColorGradedLUT(input.screenUV, true);
	color = AcesTonemap(color);
	return float4(color, 1.0);
}

float4 ColorGradingNeutralPassFragment(Varyings input) : SV_TARGET
{
	float3 color = GetColorGradedLUT(input.screenUV);
	color = NeutralTonemap(color);
	return float4(color, 1.0);
}

float4 ColorGradingReinhardPassFragment(Varyings input) : SV_TARGET
{
	float3 color = GetColorGradedLUT(input.screenUV);
	color /= color + 1.0;
	return float4(color, 1.0);
}

float4 FinalPassFragment(Varyings input) : SV_TARGET
{
	float4 color = GetSource(input.screenUV);
	color.rgb = ApplyColorGradingLUT(color.rgb);
	return color;
}

#endif