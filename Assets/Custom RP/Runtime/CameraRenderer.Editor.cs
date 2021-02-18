using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

partial class CameraRenderer
{
// the prototype for CameraRenderer
	partial void DrawGizmosBeforeFX();

	partial void DrawGizmosAfterFX();

	partial void DrawUnsupportedShaders();

	partial void PrepareForSceneWindow();

	partial void PrepareBuffer();

#if UNITY_EDITOR

	// legacy shader
	static ShaderTagId[] legacyShaderTagIds = {
		new ShaderTagId("Always"),
		new ShaderTagId("ForwardBase"),
		new ShaderTagId("PrepassBase"),
		new ShaderTagId("Vertex"),
		new ShaderTagId("VertexLMRGBM"),
		new ShaderTagId("VertexLM")
	};

	// the material used for error
	static Material errorMaterial;

	string SampleName { get; set; }

	partial void DrawGizmosBeforeFX()
	{
		if (Handles.ShouldRenderGizmos())
		{
			context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
		}
	}

	partial void DrawGizmosAfterFX()
	{
		if (Handles.ShouldRenderGizmos())
		{
			context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
		}
	}

	partial void DrawUnsupportedShaders()
	{
		if (errorMaterial == null)
		{
			// create error meterial
			errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
		}
		var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings(camera))
		{
			overrideMaterial = errorMaterial
		};

		// draw all the legact shader
		for (int i = 1; i < legacyShaderTagIds.Length; i++)
		{
			drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
		}
		var filteringSettings = FilteringSettings.defaultValue;
		context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
	}

	partial void PrepareForSceneWindow()
	{
		if (camera.cameraType == CameraType.SceneView)
		{
			ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
			useScaledRendering = false;
		}
	}

	partial void PrepareBuffer()
	{
		Profiler.BeginSample("Editor Only");
		buffer.name = SampleName = camera.name;
		Profiler.EndSample();
	}

#else

	const string SampleName = bufferName;

#endif
}