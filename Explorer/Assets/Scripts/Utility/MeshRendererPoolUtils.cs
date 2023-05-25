using UnityEngine;

namespace Utility
{
    public static class MeshRendererPoolUtils
    {
        public static MeshRenderer CreateMeshRendererComponent()
        {
            var go = new GameObject($"POOL_OBJECT_{typeof(MeshRenderer)}");
            go.AddComponent<MeshFilter>();
            go.gameObject.SetActive(false);
            return go.AddComponent<MeshRenderer>();
        }

        public static void ReleaseMeshRendererComponent(MeshRenderer renderer)
        {
            renderer.GetComponent<MeshFilter>().mesh = null;
            renderer.material = null;
        }
    }
}
