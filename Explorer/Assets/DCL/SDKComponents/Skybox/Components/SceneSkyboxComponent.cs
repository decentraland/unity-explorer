using Arch.Core;
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
        public SceneEnvironmentProfile? Environment;

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
