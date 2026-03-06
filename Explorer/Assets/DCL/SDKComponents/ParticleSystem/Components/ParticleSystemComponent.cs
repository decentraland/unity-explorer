using Arch.Core;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Textures;
using UnityEngine;

namespace DCL.SDKComponents.ParticleSystem
{
    public struct ParticleSystemComponent
    {
        public readonly UnityEngine.ParticleSystem ParticleSystemInstance;
        public readonly GameObject HostGameObject;

        public GetTextureIntention LoadingTextureIntention;
        public AssetPromise<TextureData, GetTextureIntention>? TexturePromise;
        public Material ParticleMaterial;

        public uint LastRestartCount;

        public ParticleSystemComponent(UnityEngine.ParticleSystem instance, GameObject hostGameObject) : this()
        {
            ParticleSystemInstance = instance;
            HostGameObject = hostGameObject;
        }

        public void CleanUpTexture(in World world)
        {
            LoadingTextureIntention = default(GetTextureIntention);

            if (TexturePromise != null)
            {
                TexturePromise.Value.ForgetLoading(world);
                TexturePromise = null;
            }
        }
    }
}
