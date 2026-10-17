#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Utility.Primitives
{
    public static class PrimitivesMenuItems
    {
        [MenuItem("GameObject/3D Object/Custom Cylinder")]
        public static void CreateCylinder()
        {
            var go = new GameObject("Custom Cylinder");

            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();

            Mesh mesh = null;
            CylinderVariantsFactory.Create(ref mesh);
            meshFilter.mesh = mesh;

            AssignDefaultMaterial(meshRenderer);
        }

        [MenuItem("GameObject/3D Object/Custom Cone")]
        public static void CreateCone()
        {
            var go = new GameObject("Custom Truncated Cone");

            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();

            Mesh mesh = null;
            CylinderVariantsFactory.Create(ref mesh, 0f);
            meshFilter.mesh = mesh;

            AssignDefaultMaterial(meshRenderer);
        }

        [MenuItem("GameObject/3D Object/Custom Truncated Cone")]
        public static void CreateTruncatedCone()
        {
            var go = new GameObject("Custom Truncated Cone");

            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();

            Mesh mesh = null;
            CylinderVariantsFactory.Create(ref mesh, 0.25f);
            meshFilter.mesh = mesh;

            AssignDefaultMaterial(meshRenderer);
        }

        private static void AssignDefaultMaterial(MeshRenderer renderer)
        {
            renderer.material = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat");
        }
    }
}

#endif
