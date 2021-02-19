using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

partial class PostFXStack
{
    partial void ApplySceneViewState();

#if UNITY_EDITOR
    partial void ApplySceneViewState()
    {
        if(camera.cameraType == CameraType.SceneView && !SceneView.currentDrawingSceneView.sceneViewState.showImageEffects)
        {
            settings = null;
        }
        if (camera.cameraType == CameraType.SceneView)
        {
            editorNoAO = true;
        }
        else
        {
            editorNoAO = false;
        }
    }
#endif
}
