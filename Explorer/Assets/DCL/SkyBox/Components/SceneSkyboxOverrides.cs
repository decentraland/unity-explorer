using DCL.Diagnostics;
using UnityEngine;

namespace DCL.SkyBox.Components
{
    /// <summary>
    ///     Environment overrides requested by the current scene, held on the skybox entity of the global world.
    ///     Scene worlds write the requested sky texture, the look profile, the sun override and the owner; the apply system tracks what it last pushed.
    /// </summary>
    public struct SceneSkyboxOverrides
    {
        public Texture? SkyboxTexture;
        public SceneEnvironmentProfile? Environment;
        public SceneSunOverride Sun;
        // Full scene identity: base parcels are not unique, portable experiences usually share (0,0) with world scenes
        public SceneShortInfo? Owner;

        public bool AppliedSceneControlled;
        public Texture? AppliedSkyboxTexture;
        public Texture? AppliedReflectionSource;
        public SceneEnvironmentProfile? AppliedEnvironment;
        public SceneSunOverride AppliedSun;

        /// <summary>
        ///     A scene owns the skybox while it is current and has a PBSkybox, PBSun or PBEnvironmentFog, whatever fields it sets.
        /// </summary>
        public bool SceneControlled => Owner != null;

        /// <summary>
        ///     Reflections are derived from the scene skybox.
        /// </summary>
        public Texture? ReflectionSource => SkyboxTexture;
    }
}
