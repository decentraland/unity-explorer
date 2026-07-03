using DCL.SceneRunner.Scene;
using System;
using System.Collections.Generic;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    /// <summary>
    ///     Server-side JSON DTO for the descriptor file at the LOD manifest bucket. Deserialized by
    ///     <see cref="LoadISSDescriptorSystem"/>; not used outside the loader.
    /// </summary>
    [Serializable]
    public struct ISSDescriptorMetadata
    {
        public List<ISSDescriptorAsset> assets;
    }
}
