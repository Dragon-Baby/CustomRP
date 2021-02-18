using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Rendering;


[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
	[SerializeField]
	bool DynamicBatching = true, GPUInstancing = true, SRPBatcher = true, LightsPerObject = true;

	[SerializeField]
	ShadowSettings shadows = default;

	[SerializeField]
	PostFXSettings postFXSettings = default;

	[SerializeField]
	bool HDR = true;

	[SerializeField]
	float RenderScale = 1f;

	public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }

	[SerializeField]
	ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

	[SerializeField]
	Shader TAAShader = default;

	[NonSerialized]
	Material TAAMaterial;
	public Material Material
	{
		get
		{
			if (TAAMaterial == null && TAAShader != null)
			{
				TAAMaterial = new Material(TAAShader);
				TAAMaterial.hideFlags = HideFlags.HideAndDontSave;
			}
			return TAAMaterial;
		}
	}

	public enum MSAAMode
    {
		Off = 1,
		_2x = 2,
		_4x = 4,
		_8x = 8
    }

	[SerializeField]
	MSAAMode MSAA = MSAAMode.Off;

	protected override RenderPipeline CreatePipeline()
	{
		return new CustomRenderPipeline(HDR, DynamicBatching, GPUInstancing, SRPBatcher, 
				LightsPerObject, shadows, postFXSettings, (int)colorLUTResolution, (int)MSAA, RenderScale);
	}
}