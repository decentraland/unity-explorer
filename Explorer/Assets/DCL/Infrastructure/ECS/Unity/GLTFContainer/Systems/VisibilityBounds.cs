using UnityEngine;
using Drakkar.GameUtils;
using System.Collections.Generic;

public class VisibilityBounds : MonoBehaviour
{
    public List<Bounds> visBounds = new List<Bounds>();

    private void Start()
    {

    }

    private void OnDestroy()
    {

    }

    private void OnVisible()
    {

    }

    private void OnInvisible()
    {

    }

    void OnDrawGizmos()
    {
        // Get the scene view camera
        Camera sceneCamera = null;

        #if UNITY_EDITOR
        if (UnityEditor.SceneView.lastActiveSceneView != null)
        {
            sceneCamera = UnityEditor.SceneView.lastActiveSceneView.camera;
        }
        #endif

        if (sceneCamera == null)
            return;

        // Early out if too far from the scene camera
        float maxDistance = 5f;
        if (Vector3.Distance(transform.position, sceneCamera.transform.position) > maxDistance)
            return;

        Gizmos.color = Color.red;
        foreach (Bounds vBound in visBounds)
        {
            Gizmos.DrawWireCube(vBound.center, vBound.size);
        }
    }
}
