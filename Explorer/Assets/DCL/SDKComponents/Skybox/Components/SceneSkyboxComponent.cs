using Arch.Core;
using CRDT;
using DCL.SDKComponents.MediaStream;
using DCL.SkyBox;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Textures;
using System;

namespace DCL.SDKComponents.Skybox
{
    /// <summary>
    ///     State of the scene environment components on the scene root entity: the texture slot of the PBSkybox sky
    ///     texture, the environment profile built from PBSkybox, PBSun and PBEnvironmentFog, and which of those
    ///     components the profile was built from.
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
        public EnvironmentSources Sources;

        /// <summary>
        ///     One requested texture source: a file texture loads through a promise, a video texture resolves through the
        ///     media factory as a screen-space consumer of its video player entity.
        /// </summary>
        public struct TextureSlot
        {
            public GetTextureIntention LoadingIntention;
            public AssetPromise<TextureData, GetTextureIntention>? LoadingPromise;
            public bool IsVideoTexture;
            public CRDTEntity VideoPlayerEntity;
            public TextureData? TextureData;

            /// <summary>
            ///     Forgets the intention, cancels an unresolved promise, removes a resolved video consumer and dereferences the loaded texture.
            /// </summary>
            public void CleanUp(World world, IMediaFactory mediaFactory)
            {
                LoadingIntention = default(GetTextureIntention);

                if (LoadingPromise != null)
                {
                    LoadingPromise.Value.ForgetLoading(world);
                    LoadingPromise = null;
                }

                if (IsVideoTexture && TextureData != null)
                    mediaFactory.RemoveScreenSpaceConsumer(VideoPlayerEntity);

                IsVideoTexture = false;
                VideoPlayerEntity = default(CRDTEntity);

                TextureData?.Dereference();
                TextureData = null;
            }
        }
    }
}
