using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
	CameraRenderer renderer = new CameraRenderer();

	bool allowHDR, useDynamicBatching, useGPUInstancing, useLightsPerObject;

	ShadowSettings shadowSettings;

	PostFXSettings postFXSettings;

	int colorLUTResolution;

	Material TAAMaterial;

	int MSAA;

	float renderScale;

	public CustomRenderPipeline(bool allowHDR, bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, 
		bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution, 
		int MSAA, float renderScale)
	{
		this.renderScale = renderScale;
		QualitySettings.antiAliasing = MSAA;
		this.MSAA = Mathf.Max(QualitySettings.antiAliasing, 1);
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
				shadowSettings, postFXSettings, colorLUTResolution, MSAA, renderScale);
			UnityEngine.VFX.VFXManager.ProcessCamera(camera);
		}
	}
}
