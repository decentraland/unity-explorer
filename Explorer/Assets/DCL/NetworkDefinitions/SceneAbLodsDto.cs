using System;

// ReSharper disable InconsistentNaming
namespace DCL.Ipfs
{
    // Written by abgen's LOD lane: https://github.com/decentraland/abgen/blob/main/crate/src/lods.rs (lods_manifest_block)
    /// <summary>Content-addressed names of a scene's LOD objects: the usual CDN directories, with the build's digest in the file name.</summary>
    [Serializable]
    public class SceneAbLodsDto
    {
        public string digest = null!;

        /// <summary>Descriptor file name under <c>lods-unity/manifests/</c>.</summary>
        public string? descriptor;

        public SceneAbLodLevelDto[]? levels;
    }

    [Serializable]
    public class SceneAbLodLevelDto
    {
        public int level;

        /// <summary>Bundle name under <c>LOD/{level}/</c>, without the platform suffix.</summary>
        public string? file;
    }
}
