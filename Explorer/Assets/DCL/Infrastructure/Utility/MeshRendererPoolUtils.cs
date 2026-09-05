using UnityEngine;

namespace Utility
{
    public static class MeshRendererPoolUtils
    {
        public static MeshRenderer CreateMeshRendererComponent()
        {
            var go = new GameObject($"POOL_OBJECT_{typeof(MeshRenderer)}");
            var mesh = new Mesh();
            go.AddComponent<MeshFilter>().mesh = mesh;
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
