using DCL.Diagnostics;
using UnityEngine;

namespace DCL.SkyBox.Components
{
    /// <summary>
    ///     Environment overrides requested by the current scene, held on the skybox entity of the global world.
    ///     Scene worlds write the requested textures, the environment profile and the owner; the apply system tracks what it last pushed.
    ///     Textures are either loaded files or the live render texture of a video player.
    /// </summary>
    public struct SceneSkyboxOverrides
    {
        public Texture? ReflectionMap;
        public Texture? SkyboxTexture;
        public Texture? CloudsTexture;
        public SceneEnvironmentProfile? Environment;
        // Full scene identity: base parcels are not unique, portable experiences usually share (0,0) with world scenes
        public SceneShortInfo? Owner;

        public bool AppliedSceneControlled;
        public Texture? AppliedSkyboxTexture;
        public Texture? AppliedReflectionSource;
        public Texture? AppliedCloudsTexture;
        public SceneEnvironmentProfile? AppliedEnvironment;

        /// <summary>
        ///     A scene owns the skybox while it is current and has a PBSkybox, whatever fields it sets.
        /// </summary>
        public bool SceneControlled => Owner != null;

        /// <summary>
        ///     An explicit reflection map wins, otherwise reflections are derived from the scene skybox.
        /// </summary>
        public Texture? ReflectionSource => ReferenceEquals(ReflectionMap, null) ? SkyboxTexture : ReflectionMap;
    }
}
