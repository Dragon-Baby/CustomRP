﻿using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{

	const string bufferName = "Shadows";

	// 可产生阴影的最大灯光数量
	const int maxShadowedDirLightCount = 4, maxShadowedOtherLightCount = 16;

	// 最大阴影层级
	const int maxCascades = 4;

	static string[] directionalFilterKeywords = {
		"_DIRECTIONAL_PCF3",
		"_DIRECTIONAL_PCF5",
		"_DIRECTIONAL_PCF7",
	};

	static string[] otherFilterKeywords = {
		"_OTHER_PCF3",
		"_OTHER_PCF5",
		"_OTHER_PCF7",
	};

	static string[] cascadeBlendKeywords = {
		"_CASCADE_BLEND_SOFT",
		"_CASCADE_BLEND_DITHER"
	};

	static string[] shadowMashKeywords = {
		"_SHADOW_MASK_ALWAYS",
		"_SHADOW_MASK_DISTANCE"
	};

	static int
		dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),	// 平行光阴影图集索引
		dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),	//平行光空间矩阵索引
		otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),
		otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),
		otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles"),
		cascadeCountId = Shader.PropertyToID("_CascadeCount"),
		cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
		cascadeDataId = Shader.PropertyToID("_CascadeData"),
		shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
		shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
		shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

	static Vector4[]
		cascadeCullingSpheres = new Vector4[maxCascades],
		cascadeData = new Vector4[maxCascades],
		otherShadowTiles = new Vector4[maxShadowedOtherLightCount];

	static Matrix4x4[]
		dirShadowMatrices = new Matrix4x4[maxShadowedDirLightCount * maxCascades],	//平行光空间矩阵
		otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];

	// 可产生阴影的平行光的结构体
	struct ShadowedDirectionalLight
	{
		public int visibleLightIndex;	// 可见光索引
		public float slopeScaleBias;
		public float nearPlaneOffset;
	}

	// 可产生阴影的平行光数组
	ShadowedDirectionalLight[] shadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirLightCount];

	struct ShadowedOtherLight
	{
		public int visibleLightIndex;
		public float slopeScaleBias;
		public float normalBias;
		public bool isPoint;
	}

	ShadowedOtherLight[] shadowedOtherLights = new ShadowedOtherLight[maxShadowedOtherLightCount];

	// 可产生阴影的平行光和其他光源的数量
	int shadowedDirLightCount, shadowedOtherLightCount;

	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};

	ScriptableRenderContext context;

	CullingResults cullingResults;

	ShadowSettings settings;

	bool useShadowMask;

	Vector4 atlasSizes;

	public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
	{
		this.context = context;
		this.cullingResults = cullingResults;
		this.settings = settings;
		shadowedDirLightCount = shadowedOtherLightCount = 0;	// 初始化置为0
		useShadowMask = false;
	}

	public void Cleanup()
	{
		// 清除平行光临时渲染纹理
		buffer.ReleaseTemporaryRT(dirShadowAtlasId);
		if (shadowedOtherLightCount > 0)
		{
			// 清除其它光源临时渲染纹理
			buffer.ReleaseTemporaryRT(otherShadowAtlasId);
		}
		ExecuteBuffer();
	}

	// 预留空间，存储渲染平行光阴影的信息
	public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
	{
		// 若平行光数量未达到最大值并产生阴影
		if (shadowedDirLightCount < maxShadowedDirLightCount &&
			light.shadows != LightShadows.None && light.shadowStrength > 0f)
		{
			float maskChannel = -1;
			LightBakingOutput lightBaking = light.bakingOutput;
			if(lightBaking.lightmapBakeType == LightmapBakeType.Mixed && 
				lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
				useShadowMask = true;
				maskChannel = lightBaking.occlusionMaskChannel;
			}

			// 如果超出可见光投射阴影的范围
			if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
			{
				return new Vector4(light.shadowStrength, 0f, 0f, maskChannel);
			}

			// 存储当前索引灯光的阴影相关信息
			shadowedDirectionalLights[shadowedDirLightCount] = new ShadowedDirectionalLight
			{
				visibleLightIndex = visibleLightIndex,
				slopeScaleBias = light.shadowBias,
				nearPlaneOffset = light.shadowNearPlane
			};
			return new Vector4(light.shadowStrength, settings.directional.cascadeCount * shadowedDirLightCount++, 
								light.shadowNormalBias, maskChannel);
		}
		return new Vector4(-light.shadowStrength, 0f, 0f, -1f);
	}

	public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
	{
		if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
		{
			return new Vector4(0f, 0f, 0f, -1f);
		}
		float maskChannel = -1f;
		LightBakingOutput lightBaking = light.bakingOutput;
		if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
			lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
		{
			useShadowMask = true;
			maskChannel = lightBaking.occlusionMaskChannel;
		}
		bool isPoint = light.type == LightType.Point;
		int newLightCount = shadowedOtherLightCount + (isPoint ? 6 : 1);
		if (newLightCount >= maxShadowedOtherLightCount ||
			!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
		{
			return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
		}
		shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight
		{
			visibleLightIndex = visibleLightIndex,
			slopeScaleBias = light.shadowBias,
			normalBias = light.shadowNormalBias,
			isPoint = isPoint
		};
		Vector4 data = new Vector4(light.shadowStrength, shadowedOtherLightCount, isPoint ? 1f : 0f, maskChannel);
		shadowedOtherLightCount = newLightCount;
		return data;
	}

	// 渲染阴影
	public void Render()
	{
		// 若平行光数量大于0则渲染阴影
		if (shadowedDirLightCount > 0)
		{
			RenderDirectionalShadows();
		}
		// 防止某些平台编译失败
		else
		{
			buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		}
		if (shadowedOtherLightCount > 0)
		{
			RenderOtherShadows();
		}
		else
		{
			buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
		}
		buffer.BeginSample(bufferName);
		SetKeywords(shadowMashKeywords, useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);
		buffer.SetGlobalInt(cascadeCountId, shadowedDirLightCount > 0 ? settings.directional.cascadeCount : 0);
		float f = 1f - settings.directional.cascadeFade;
		buffer.SetGlobalVector(shadowDistanceFadeId, 
			new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade, 1f / (1f - f * f)));
		buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}

	// 渲染平行光的阴影
	void RenderDirectionalShadows()
	{
		// 设置阴影图集大小
		int atlasSize = (int)settings.directional.atlasSize;
		atlasSizes.x = atlasSize;
		atlasSizes.y = 1f / atlasSize;
		// 获取一个渲染纹理
		buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		// 设置渲染目标到渲染纹理，数据加载时不考虑状态，同时存储数据到纹理
		buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		//清除渲染目标（深度缓冲）
		buffer.ClearRenderTarget(true, false, Color.clear);


		buffer.SetGlobalFloat(shadowPancakingId, 1f);

		buffer.BeginSample(bufferName);
		ExecuteBuffer();

		// 根据平行光数量拆分图集
		int tiles = shadowedDirLightCount * settings.directional.cascadeCount;
		int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
		int tileSize = atlasSize / split;

		// 渲染所有平行光的阴影
		for (int i = 0; i < shadowedDirLightCount; i++)
		{
			RenderDirectionalShadows(i, split, tileSize);
		}

		buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
		buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
		buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
		SetKeywords(directionalFilterKeywords, (int)settings.directional.filter - 1);
		SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1);


		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}

	void RenderDirectionalShadows(int index, int split, int tileSize)
	{
		ShadowedDirectionalLight light = shadowedDirectionalLights[index];
		var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex)
		{
			useRenderingLayerMaskTest = true
		};
		int cascadeCount = settings.directional.cascadeCount;
		int tileOffset = index * cascadeCount;
		Vector3 ratios = settings.directional.CascadeRatios;
		float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
		float tileScale = 1f / split;

		for (int i = 0; i < cascadeCount; i++)
		{
			// 计算平行光阴影的相关矩阵和投射物剔除结果
			cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
				light.visibleLightIndex, i, cascadeCount, ratios, tileSize,
				light.nearPlaneOffset, out Matrix4x4 viewMatrix,
				out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
			);

			splitData.shadowCascadeBlendCullingFactor = cullingFactor;

			// 传入剔除信息
			shadowSettings.splitData = splitData;
			if (index == 0)
			{
				SetCascadeData(i, splitData.cullingSphere, tileSize);
			}
			int tileIndex = tileOffset + i;
			dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix,
				SetTileViewport(tileIndex, split, tileSize), tileScale);	// 设置世界到灯光空间的变换矩阵

			buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);	// 传入view,projection矩阵
			buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);

			ExecuteBuffer();
			// 绘制阴影
			context.DrawShadows(ref shadowSettings);
			buffer.SetGlobalDepthBias(0f, 0f);
		}
	}

	void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
	{
		float texelSize = 2f * cullingSphere.w / tileSize;
		float filterSize = texelSize * ((float)settings.directional.filter + 1f);
		cullingSphere.w -= filterSize;
		cullingSphere.w *= cullingSphere.w;
		cascadeCullingSpheres[index] = cullingSphere;
		cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);
	}

	void RenderOtherShadows()
	{
		int atlasSize = (int)settings.other.atlasSize;
		atlasSizes.z = atlasSize;
		atlasSizes.w = 1f / atlasSize;
		buffer.GetTemporaryRT(otherShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		buffer.SetRenderTarget(otherShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		buffer.ClearRenderTarget(true, false, Color.clear);
		buffer.SetGlobalFloat(shadowPancakingId, 0f);
		buffer.BeginSample(bufferName);
		ExecuteBuffer();

		int tiles = shadowedOtherLightCount;
		int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
		int tileSize = atlasSize / split;

		for (int i = 0; i < shadowedOtherLightCount;)
		{
			if (shadowedOtherLights[i].isPoint)
			{
				RenderPointShadows(i, split, tileSize);
				i += 6;
			}
			else
			{
				RenderSpotShadows(i, split, tileSize);
				i += 1;
			}
		}
		buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
		buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);
		SetKeywords(otherFilterKeywords, (int)settings.other.filter - 1);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}

	void RenderPointShadows(int index, int split, int tileSize)
	{
		ShadowedOtherLight light = shadowedOtherLights[index];
		var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex)
		{
			useRenderingLayerMaskTest = true
		};
		float texelSize = 2f / tileSize;
		float filterSize = texelSize * ((float)settings.other.filter + 1f);
		float bias = light.normalBias * filterSize * 1.4142136f;
		float tileScale = 1f / split;
		float fovBias = Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
		for (int i = 0; i < 6; i++)
		{
			cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
				light.visibleLightIndex, (CubemapFace)i, fovBias,
				out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
				out ShadowSplitData splitData
			);

			viewMatrix.m11 = -viewMatrix.m11;
			viewMatrix.m12 = -viewMatrix.m12;
			viewMatrix.m13 = -viewMatrix.m13;

			shadowSettings.splitData = splitData;
			int tileIndex = index + i;
			Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
			SetOtherTileData(tileIndex, offset, tileScale, bias);
			otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, tileScale);

			buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
			buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
			ExecuteBuffer();
			context.DrawShadows(ref shadowSettings);
			buffer.SetGlobalDepthBias(0f, 0f);
		}
	}

	void RenderSpotShadows(int index, int split, int tileSize)
	{
		ShadowedOtherLight light = shadowedOtherLights[index];
		var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex)
		{
			useRenderingLayerMaskTest = true
		};
		cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
			light.visibleLightIndex, out Matrix4x4 viewMatrix,
			out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
		);
		shadowSettings.splitData = splitData;
		float texelSize = 2f / (tileSize * projectionMatrix.m00);
		float filterSize = texelSize * ((float)settings.other.filter + 1f);
		float bias = light.normalBias * filterSize * 1.4142136f;
		Vector2 offset = SetTileViewport(index, split, tileSize);
		float tileScale = 1f / split;
		SetOtherTileData(index, offset, tileScale, bias);
		otherShadowMatrices[index] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, tileScale);

		buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
		buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
		ExecuteBuffer();
		context.DrawShadows(ref shadowSettings);
		buffer.SetGlobalDepthBias(0f, 0f);
	}

	void SetOtherTileData(int index, Vector2 offset, float scale, float bias)
	{
		float border = atlasSizes.w * 0.5f;
		Vector4 data;
		data.x = offset.x * scale + border;
		data.y = offset.y * scale + border;
		data.z = scale - border - border;
		data.w = bias;
		otherShadowTiles[index] = data;
	}

	Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
	{
		if (SystemInfo.usesReversedZBuffer)
		{
			m.m20 = -m.m20;
			m.m21 = -m.m21;
			m.m22 = -m.m22;
			m.m23 = -m.m23;
		}
		m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
		m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
		m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
		m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
		m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
		m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
		m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
		m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
		m.m20 = 0.5f * (m.m20 + m.m30);
		m.m21 = 0.5f * (m.m21 + m.m31);
		m.m22 = 0.5f * (m.m22 + m.m32);
		m.m23 = 0.5f * (m.m23 + m.m33);
		return m;
	}

	Vector2 SetTileViewport(int index, int split, float tileSize)
	{
		Vector2 offset = new Vector2(index % split, index / split);
		buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
		return offset;
	}

	void SetKeywords(string[] keywords, int enabledIndex)
	{
		for (int i = 0; i < keywords.Length; i++)
		{
			if (i == enabledIndex)
			{
				buffer.EnableShaderKeyword(keywords[i]);
			}
			else
			{
				buffer.DisableShaderKeyword(keywords[i]);
			}
		}
	}

	void ExecuteBuffer()
	{
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
}