using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
	static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
						litShaderTagId = new ShaderTagId("CustomLit"),
						depthNormalShaderTagId = new ShaderTagId("DrawDepthNormal");


	static int colorBufferId = Shader.PropertyToID("_CameraColorBuffer"),
				depthBufferId = Shader.PropertyToID("_CameraDepthBuffer"),
				depthNormalTextureId = Shader.PropertyToID("_CameraDepthNormalTexture"),
				positionVSTextureId = Shader.PropertyToID("_CameraPositionVSTexture");

	ScriptableRenderContext context;

	Camera camera;

	const string bufferName = "Render Camera";	

	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};


	CullingResults cullingResults;

	Lighting lighting = new Lighting();

	PostFXStack postFXStack = new PostFXStack();

	bool useHDR;

	bool useScaledRendering;

	Vector2Int bufferSize;

	bool useDepthNormal;

	public void Render(ScriptableRenderContext context, Camera camera, bool allowHDR, bool useDynamicBatching, 
		bool useGPUInstancing, bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings,
		int colorLUTResolution, int MSAA, float renderScale)
	{
		this.context = context;
		this.camera = camera;

		useDepthNormal = true;
		useScaledRendering = renderScale < 0.99f || renderScale > 1.01f;
		useHDR = allowHDR && camera.allowHDR;

		PrepareBuffer();
		PrepareForSceneWindow();

		if (!Cull(shadowSettings.maxDistance))
		{
			return;
		}

		if(useScaledRendering)
        {
			bufferSize.x = (int)(camera.pixelWidth * renderScale);
			bufferSize.y = (int)(camera.pixelHeight * renderScale);
        }
		else
        {
			bufferSize.x = camera.pixelWidth;
			bufferSize.y = camera.pixelHeight;
        }

		if (useDepthNormal)
		{
			SetupDepthNormal(MSAA);
			DrawDepthNormal(useDynamicBatching, useGPUInstancing);
			buffer.ReleaseTemporaryRT(depthNormalTextureId);
			buffer.ReleaseTemporaryRT(depthBufferId);
			Submit();
		}

		buffer.BeginSample(SampleName);
		ExecuteBuffer();
		lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject);
		postFXStack.Setup(context, camera, bufferSize, postFXSettings, useHDR, colorLUTResolution);
		buffer.EndSample(SampleName);

		Setup(MSAA);
		DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject);	
		DrawUnsupportedShaders();

		DrawGizmosBeforeFX();

		if (postFXStack.IsActive)
        {
			postFXStack.Render(colorBufferId);
        }
		DrawGizmosAfterFX();

		Cleanup();
		Submit();
	}

	bool Cull(float maxShadowDistance)
	{
		if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))	
		{
			p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
			cullingResults = context.Cull(ref p);	
			return true;
		}
		return false;
	}

	void Setup(int MSAA)
	{
		context.SetupCameraProperties(camera);

		CameraClearFlags flags = camera.clearFlags;

		if(postFXStack.IsActive)
		{
			int renderSamples = camera.allowMSAA ? MSAA : 1;
			if (flags > CameraClearFlags.Color)
            {
				flags = CameraClearFlags.Color;
            }
			buffer.GetTemporaryRT(colorBufferId, bufferSize.x, bufferSize.y, 0,
				FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default, 
				RenderTextureReadWrite.Default, renderSamples);
			buffer.GetTemporaryRT(depthBufferId, bufferSize.x, bufferSize.y, 32,
				FilterMode.Bilinear, RenderTextureFormat.Depth, RenderTextureReadWrite.Default, renderSamples);
			buffer.SetRenderTarget(colorBufferId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
				depthBufferId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }

		buffer.ClearRenderTarget(
			flags <= CameraClearFlags.Depth, 
			flags == CameraClearFlags.Color,
			flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear
		);
		buffer.BeginSample(SampleName);
		ExecuteBuffer();
	}

	void SetupDepthNormal(int MSAA)
    {
		context.SetupCameraProperties(camera);
		CameraClearFlags flags = camera.clearFlags;
		int renderSamples = camera.allowMSAA ? MSAA : 1;
		buffer.GetTemporaryRT(depthNormalTextureId, bufferSize.x, bufferSize.y, 0, FilterMode.Point, 
			RenderTextureFormat.Default, RenderTextureReadWrite.Default, renderSamples);
		buffer.GetTemporaryRT(positionVSTextureId, bufferSize.x, bufferSize.y, 0, FilterMode.Point, 
			RenderTextureFormat.Default, RenderTextureReadWrite.Default, renderSamples);
		buffer.GetTemporaryRT(depthBufferId, bufferSize.x, bufferSize.y, 32, FilterMode.Point, 
			RenderTextureFormat.Depth, RenderTextureReadWrite.Default, renderSamples);
		RenderTargetIdentifier[] colorBuffersId = new RenderTargetIdentifier[2];
		colorBuffersId[0] = depthNormalTextureId;
		colorBuffersId[1] = positionVSTextureId;
		buffer.SetRenderTarget(colorBuffersId, depthBufferId);
		buffer.ClearRenderTarget(
			flags <= CameraClearFlags.Depth,
			flags == CameraClearFlags.Color,
			flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear
		);
		buffer.BeginSample(SampleName);
		ExecuteBuffer();
	}

	void Cleanup()
    {
		lighting.Cleanup();
		if(postFXStack.IsActive)
        {
			buffer.ReleaseTemporaryRT(colorBufferId);
			buffer.ReleaseTemporaryRT(depthBufferId);
		}
    }

	void Submit()
	{
		buffer.EndSample(SampleName);

		ExecuteBuffer();

		context.Submit();
	}

	void ExecuteBuffer()
	{
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

	void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject)
	{
		PerObjectData lightsPerObjectFlags = useLightsPerObject 
			? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;

		var sortingSettings = new SortingSettings(camera)
		{
			criteria = SortingCriteria.CommonOpaque
		};

		var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
		{
			enableDynamicBatching = useDynamicBatching,
			enableInstancing = useGPUInstancing,
			perObjectData = PerObjectData.ReflectionProbes |
							PerObjectData.Lightmaps | PerObjectData.ShadowMask |
							PerObjectData.LightProbe | PerObjectData.OcclusionProbe | 
							PerObjectData.LightProbeProxyVolume | PerObjectData.OcclusionProbeProxyVolume |
							lightsPerObjectFlags
		};
		drawingSettings.SetShaderPassName(1, litShaderTagId);
		var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
		context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

		context.DrawSkybox(camera);


		sortingSettings.criteria = SortingCriteria.CommonTransparent;
		drawingSettings.sortingSettings = sortingSettings;
		filteringSettings.renderQueueRange = RenderQueueRange.transparent;
		context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
	}

	void DrawDepthNormal(bool useDynamicBatching, bool useGPUInstancing)
	{

		var sortingSettings = new SortingSettings(camera)
		{
			criteria = SortingCriteria.CommonOpaque
		};
		var drawingSettings = new DrawingSettings(depthNormalShaderTagId, sortingSettings)
		{
			enableDynamicBatching = useDynamicBatching,
			enableInstancing = useGPUInstancing,
		};
		var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

		context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
	}
}