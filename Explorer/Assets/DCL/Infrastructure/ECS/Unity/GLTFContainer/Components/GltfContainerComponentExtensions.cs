using UnityEngine;
using UnityEngine.Rendering;

namespace ECS.Unity.GLTFContainer.Components
{
    public static class GltfContainerComponentExtensions
    {
        public static void ResetOriginalMaterials(this ref GltfContainerComponent component)
        {
            if (component.OriginalMaterials == null) return;

            foreach ((Renderer renderer, Material material) in component.OriginalMaterials)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }

            component.OriginalMaterials.Clear();
            component.OriginalMaterials = null;
        }
    }
}
