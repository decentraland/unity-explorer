using Arch.Core;
using DCL.SkyBox;
using Decentraland.Common;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Textures;
using System;

namespace DCL.SDKComponents.Skybox
{
    /// <summary>
    ///     State of the scene environment components on the scene root entity: the texture slot of the PBSkybox sky
    ///     texture, the look profile built from PBSkybox, PBSun and PBEnvironmentFog, the sun override of PBSun, and
    ///     which of those components the profile was built from.
    /// </summary>
    public struct SceneSkyboxComponent
    {
        /// <summary>
        ///     The environment components present on the root entity. Removing one of several sets no dirty flag on the
        ///     others, so a change of the set is what triggers the profile rebuild.
        /// </summary>
        [Flags]
        public enum EnvironmentSources : byte
        {
            None = 0,
            Skybox = 1,
            Sun = 1 << 1,
            Fog = 1 << 2,
        }

        public TextureSlot SkyboxTexture;
        public SceneEnvironmentProfile? Environment;
        public SceneSunOverride Sun;
        public EnvironmentSources Sources;

        /// <summary>
        ///     Copies of the PBSun look fields the profile was last built from. A PBSun update that leaves them as they
        ///     are only moves the sun, so the profile and its gradients are kept.
        /// </summary>
        public ColorGradient? SunColor;
        public bool? SunVisible;

        /// <summary>
        ///     One requested texture source, loaded through a promise. The intention stays after the promise resolves so
        ///     the same source is not requested again.
        /// </summary>
        public struct TextureSlot
        {
            public GetTextureIntention LoadingIntention;
            public AssetPromise<TextureData, GetTextureIntention>? LoadingPromise;
            public TextureData? TextureData;

            /// <summary>
            ///     Forgets the intention, cancels an unresolved promise and dereferences the loaded texture.
            /// </summary>
            public void CleanUp(World world)
            {
                LoadingIntention = default(GetTextureIntention);

                if (LoadingPromise != null)
                {
                    LoadingPromise.Value.ForgetLoading(world);
                    LoadingPromise = null;
                }

                TextureData?.Dereference();
                TextureData = null;
            }
        }
    }
}
