using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/* the custom RP class */

public partial class CustomRenderPipeline : RenderPipeline
{
	// create a camera renderer
	CameraRenderer renderer = new CameraRenderer();

	bool allowHDR, useDynamicBatching, useGPUInstancing, useLightsPerObject;

	ShadowSettings shadowSettings;

	PostFXSettings postFXSettings;

	int colorLUTResolution;

	public CustomRenderPipeline(bool allowHDR, bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, 
		bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution)
	{
		this.colorLUTResolution = colorLUTResolution;
		this.allowHDR = allowHDR;
		this.shadowSettings = shadowSettings;
		this.useDynamicBatching = useDynamicBatching;
		this.useGPUInstancing = useGPUInstancing;
		this.useLightsPerObject = useLightsPerObject;
		this.postFXSettings = postFXSettings;
		GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
		GraphicsSettings.lightsUseLinearIntensity = true;
		InitializeForEditor();
	}


	protected override void Render(ScriptableRenderContext context, Camera[] cameras)
	{
		foreach (Camera camera in cameras)
		{
			renderer.Render(context, camera, allowHDR, useDynamicBatching, useGPUInstancing, useLightsPerObject, 
				shadowSettings, postFXSettings, colorLUTResolution);
		}
	}
}
