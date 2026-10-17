using CommunicationData.URLHelpers;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;

namespace ECS.StreamableLoading.GLTF
{
    /// <summary>
    ///     An external file a GLTF import fetched (texture or buffer) together with the content URL it
    ///     resolved to at import time. A cached import embeds those files in its materials and meshes,
    ///     so the import is only reusable while every recorded file still resolves to the same URL.
    /// </summary>
    public readonly struct GltfExternalDependency
    {
        public readonly string File;
        public readonly string Url;

        public GltfExternalDependency(string file, string url)
        {
            File = file;
            Url = url;
        }

        /// <summary>
        ///     True while every recorded file still resolves to the URL it was imported from under the
        ///     given content mapping. A mismatch means the file was republished under a new content hash
        ///     (e.g. a texture edited during local scene development), so the import holding the old
        ///     bytes is stale.
        /// </summary>
        public static bool AreUpToDate(IReadOnlyList<GltfExternalDependency>? dependencies, ISceneContent sceneContent)
        {
            if (dependencies == null)
                return true;

            for (var i = 0; i < dependencies.Count; i++)
            {
                GltfExternalDependency dependency = dependencies[i];

                if (!sceneContent.TryGetContentUrl(dependency.File, out URLAddress url)
                    || !string.Equals(url.Value, dependency.Url, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
    }
}
