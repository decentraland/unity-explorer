using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DCL.BevyMigration.Bridge
{
    /// <summary>
    ///     Finds SDK scene content for the bridge (BevyMigration/docs/README.md § 7, step 3): every scene lives under
    ///     a root GameObject named <c>&lt;parcel&gt;_&lt;name&gt;_Container</c> (TransformsPlugin), so a periodic walk of
    ///     those roots registers new mesh renderers with their uploaded meshes and materials and collects the scene
    ///     lights. Avatars (skinned), landscape and particles are not scene content and stay with Unity until their steps.
    /// </summary>
    public class SceneContentScanner
    {
        private const string CONTAINER_SUFFIX = "_Container";

        private readonly RendererRegistry registry;
        private readonly ResourceUploader uploader;
        private readonly List<GameObject> roots = new (64);
        private readonly List<MeshRenderer> meshRenderers = new (4096);
        private readonly List<Light> lights = new (64);
        private readonly List<Material> sharedMaterials = new (8);
        private readonly List<Light> sceneLights = new (64);

        public SceneContentScanner(RendererRegistry registry, ResourceUploader uploader)
        {
            this.registry = registry;
            this.uploader = uploader;
        }

        /// <summary>Point and spot lights found under the scene roots at the last scan.</summary>
        public IReadOnlyList<Light> SceneLights => sceneLights;

        /// <summary>
        ///     <see cref="RenderSettings.sun" /> when set, else the strongest enabled directional light anywhere; the
        ///     skybox owns the sun here and does not always publish it.
        /// </summary>
        public Light? Sun { get; private set; }

        public int ScannedRenderers { get; private set; }

        public ResourceUploader Uploader => uploader;

        /// <summary>
        ///     Walks the scene roots once: registers renderers not yet known, drops destroyed ones, refreshes lights.
        /// </summary>
        public void Scan()
        {
            registry.PruneDestroyed();
            sceneLights.Clear();
            Sun = RenderSettings.sun != null ? RenderSettings.sun : StrongestDirectionalLight();
            uploader.RefreshDynamicTextures();
            ScannedRenderers = 0;

            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid()) return;

            roots.Clear();
            active.GetRootGameObjects(roots);

            foreach (GameObject root in roots)
            {
                if (!root.name.EndsWith(CONTAINER_SUFFIX, StringComparison.Ordinal)) continue;

                meshRenderers.Clear();
                root.GetComponentsInChildren(true, meshRenderers);

                foreach (MeshRenderer renderer in meshRenderers)
                {
                    ScannedRenderers++;
                    if (registry.Contains(renderer)) continue;

                    Register(renderer);
                }

                lights.Clear();
                root.GetComponentsInChildren(true, lights);

                foreach (Light light in lights)
                    if (light.type is LightType.Point or LightType.Spot && light.enabled)
                        sceneLights.Add(light);
            }
        }

        private static Light? StrongestDirectionalLight()
        {
            Light? best = null;

            foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (light.type == LightType.Directional && light.enabled && (best == null || light.intensity > best.intensity))
                    best = light;

            return best;
        }

        private void Register(MeshRenderer renderer)
        {
            MeshFilter? filter = renderer.GetComponent<MeshFilter>();
            Mesh? mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return;

            uint meshHandle = uploader.Mesh(mesh);
            renderer.GetSharedMaterials(sharedMaterials);
            var materialHandles = new uint[Mathf.Max(sharedMaterials.Count, 1)];

            for (var i = 0; i < sharedMaterials.Count; i++)
            {
                Material? material = sharedMaterials[i];
                materialHandles[i] = material != null ? uploader.Material(material) : BridgeMaterials.PLACEHOLDER;
            }

            registry.Register(renderer, meshHandle, materialHandles);
        }
    }
}
