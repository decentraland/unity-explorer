using UnityEngine;
using Drakkar.GameUtils;
using System.Collections.Generic;

public class VisibilityEvent : MonoBehaviour
{
    public VisibilityDriver Driver;
    public List<Renderer> renderers = new List<Renderer>();

    private void Start()
    {
        Driver.VisibilityGroup.visibleAction += OnVisible;
        Driver.VisibilityGroup.invisibleAction += OnInvisible;
    }

    private void OnDestroy()
    {
        Driver.VisibilityGroup.visibleAction -= OnVisible;
        Driver.VisibilityGroup.invisibleAction -= OnInvisible;
    }

    private void OnVisible()
    {
        foreach (Renderer rend in renderers)
            rend.enabled = true;
    }

    private void OnInvisible()
    {
        foreach (Renderer rend in renderers)
            rend.enabled = false;
    }

    // Helper to visualize
    public static void DrawSpheres(List<Renderer> renderers)
    {
        Gizmos.color = Color.green;

        foreach (Renderer rend in renderers)
        {
            VisibilitySphere[] viSpheres = rend.gameObject.GetComponents<VisibilitySphere>();

            for (int i = 0; i < viSpheres.Length; ++i)
            {
                Gizmos.DrawWireSphere(rend.bounds.center + viSpheres[i].Visibility.Offset, viSpheres[i].Visibility.Radius);
            }
        }
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

        DrawSpheres(renderers);

        Gizmos.color = Color.red;
        foreach (Renderer rend in renderers)
        {
            Gizmos.DrawWireCube(rend.bounds.center, rend.bounds.size);
        }
    }
}
