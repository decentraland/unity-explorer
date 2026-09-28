using UnityEngine;

namespace DCL.SkyBox.Components
{
    /// <summary>
    ///     Environment overrides requested by the current scene, held on the skybox entity of the global world.
    ///     Scene worlds write the requested textures and the owner; the apply system tracks what it last pushed.
    /// </summary>
    public struct SceneSkyboxOverrides
    {
        public Texture2D? ReflectionMap;
        public Texture2D? SkyboxTexture;
        public Vector2Int? OwnerBaseParcel;

        public Texture2D? AppliedSkyboxTexture;
        public Texture2D? AppliedReflectionSource;

        /// <summary>
        ///     An explicit reflection map wins, otherwise reflections are derived from the scene skybox.
        /// </summary>
        public Texture2D? ReflectionSource => ReferenceEquals(ReflectionMap, null) ? SkyboxTexture : ReflectionMap;
    }
}
