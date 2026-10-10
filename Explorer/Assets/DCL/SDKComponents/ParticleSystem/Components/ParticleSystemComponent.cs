using Arch.Core;
using CrdtEcsBridge.Components.Conversion;
using DCL.ECSComponents;
using Decentraland.Common;
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
        public TextureData? SourceTextureData;
        public Material ParticleMaterial;

        // Cached objects to avoid allocations on dirty updates
        public Gradient? CachedGradient;
        private GradientColorKey[]? cachedColorKeys;
        private GradientAlphaKey[]? cachedAlphaKeys;
        public AnimationCurve? CachedCurve;
        public UnityEngine.ParticleSystem.Burst[]? CachedBursts;

        // Blend mode tracking to skip redundant material operations
        public PBParticleSystem.Types.BlendMode LastAppliedBlendMode;
        public bool BlendModeInitialized;

        public ParticleSystemComponent(UnityEngine.ParticleSystem instance, GameObject hostGameObject) : this()
        {
            ParticleSystemInstance = instance;
            Renderer = instance.GetComponent<ParticleSystemRenderer>();
            HostGameObject = hostGameObject;
        }

        public void UpdateColorOverLifetimeCache(ColorRange colorOverLifetime)
        {
            CachedGradient ??= new Gradient();
            cachedColorKeys ??= new GradientColorKey[2];
            cachedAlphaKeys ??= new GradientAlphaKey[2];

            cachedColorKeys[0] = new GradientColorKey(colorOverLifetime.Start.ToUnityColor(), 0f);
            cachedColorKeys[1] = new GradientColorKey(colorOverLifetime.End.ToUnityColor(), 1f);
            cachedAlphaKeys[0] = new GradientAlphaKey(colorOverLifetime.Start.A, 0f);
            cachedAlphaKeys[1] = new GradientAlphaKey(colorOverLifetime.End.A, 1f);

            CachedGradient.SetKeys(cachedColorKeys, cachedAlphaKeys);
        }

        public void CleanUpTexture(in World world)
        {
            LoadingTextureIntention = default(GetTextureIntention);

            if (TexturePromise != null)
            {
                TexturePromise.Value.ForgetLoading(world);
                TexturePromise = null;
            }

            SourceTextureData?.Dereference();
            SourceTextureData = null;

            if (ParticleMaterial != null)
                ParticleMaterial.mainTexture = null;
        }
    }
}
