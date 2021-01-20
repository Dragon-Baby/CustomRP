﻿using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;

public partial class CustomRenderPipeline
{
    partial void InitializeForEditor();

#if UNITY_EDITOR

    static Lightmapping.RequestLightsDelegate lightsDelegate = (Light[] lights, NativeArray<LightDataGI> output) => {
			var lightData = new LightDataGI();
			for (int i = 0; i < lights.Length; i++)
			{
				Light light = lights[i];
				switch (light.type)
				{
					case LightType.Directional:
						var directionalLight = new DirectionalLight();
						LightmapperUtils.Extract(light, ref directionalLight);
						lightData.Init(ref directionalLight);
						break;
					case LightType.Point:
						var pointLight = new PointLight();
						LightmapperUtils.Extract(light, ref pointLight);
						lightData.Init(ref pointLight);
						break;
					case LightType.Spot:
						var spotLight = new SpotLight();
						LightmapperUtils.Extract(light, ref spotLight);
						lightData.Init(ref spotLight);
						break;
					case LightType.Area:
						var rectangleLight = new RectangleLight();
						LightmapperUtils.Extract(light, ref rectangleLight);
						rectangleLight.mode = LightMode.Baked;
						lightData.Init(ref rectangleLight);
						break;
					default:
						lightData.InitNoBake(light.GetInstanceID());
						break;
				}
				lightData.falloff = FalloffType.InverseSquared;
				output[i] = lightData;
			}
		};

	partial void InitializeForEditor()
	{
		Lightmapping.SetDelegate(lightsDelegate);
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		Lightmapping.ResetDelegate();
	}

#endif
}
