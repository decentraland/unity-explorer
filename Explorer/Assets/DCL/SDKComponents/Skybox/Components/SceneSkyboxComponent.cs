using Arch.Core;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Textures;

namespace DCL.SDKComponents.Skybox
{
    /// <summary>
    ///     Texture loading state of the scene skybox component on the scene root entity, one slot per PBSkybox field.
    /// </summary>
    public struct SceneSkyboxComponent
    {
        public TextureSlot ReflectionMap;
        public TextureSlot SkyboxTexture;

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
