using Arch.Core;
using DCL.ECSComponents;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Textures;
using UnityEngine;

namespace DCL.SDKComponents.ParticleSystem
{
    public struct ParticleSystemComponent
    {
        public readonly UnityEngine.ParticleSystem ParticleSystemInstance;
        public readonly ParticleSystemRenderer Renderer;
        public readonly GameObject HostGameObject;

        public GetTextureIntention LoadingTextureIntention;
        public AssetPromise<TextureData, GetTextureIntention>? TexturePromise;
        public Material ParticleMaterial;

        // Cached objects to avoid allocations on dirty updates
        public Gradient CachedGradient;
        public GradientColorKey[] CachedColorKeys;
        public GradientAlphaKey[] CachedAlphaKeys;
        public AnimationCurve CachedCurve;
        public UnityEngine.ParticleSystem.Burst[] CachedBursts;

        // Blend mode tracking to skip redundant material operations
        public PBParticleSystem.Types.BlendMode LastAppliedBlendMode;
        public bool BlendModeInitialized;

        public ParticleSystemComponent(UnityEngine.ParticleSystem instance, GameObject hostGameObject) : this()
        {
            ParticleSystemInstance = instance;
            Renderer = instance.GetComponent<ParticleSystemRenderer>();
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
