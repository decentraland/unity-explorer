using UnityEngine.Rendering;

namespace ECS.Unity.GLTFContainer.Components
{
    public static class GltfContainerComponentExtensions
    {
        public static void ResetOriginalMaterials(this ref GltfContainerComponent component)
        {
            if (component.OriginalMaterials == null) return;

            foreach (var rendererMaterialKeyValuePair in component.OriginalMaterials)
            {
                rendererMaterialKeyValuePair.Key.sharedMaterial = rendererMaterialKeyValuePair.Value;
                rendererMaterialKeyValuePair.Key.shadowCastingMode = ShadowCastingMode.On;
            }

            component.OriginalMaterials.Clear();
            component.OriginalMaterials = null;
        }
    }
}
