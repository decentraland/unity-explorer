using Arch.Core;
using CRDT;
using DCL.SDKComponents.MediaStream;
using DCL.SkyBox;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Textures;

namespace DCL.SDKComponents.Skybox
{
    /// <summary>
    ///     State of the scene skybox component on the scene root entity: one texture slot per PBSkybox texture field
    ///     and the environment profile built from its color and float groups.
    /// </summary>
    public struct SceneSkyboxComponent
    {
        public TextureSlot ReflectionMap;
        public TextureSlot SkyboxTexture;
        public TextureSlot CloudsTexture;
        public SceneEnvironmentProfile? Environment;

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
