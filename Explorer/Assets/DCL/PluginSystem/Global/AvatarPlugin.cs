using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.DemoScripts.Systems;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.DebugUtilities;
using DCL.Nametags;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using ECS;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using DCL.AvatarRendering;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.Multiplayer.Profiles.Entities;
using DCL.AvatarRendering.Loading.Assets;
using DCL.Friends.UserBlocking;
using DCL.Quality;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;
using StartAvatarMatricesCalculationSystem = DCL.AvatarRendering.AvatarShape.Systems.StartAvatarMatricesCalculationSystem;
#if UNITY_EDITOR
using DCL.AvatarAnimation;
#endif

namespace DCL.PluginSystem.Global
{
    public class AvatarPlugin : IDCLGlobalPlugin<AvatarPlugin.AvatarShapeSettings>
    {
        private static readonly int GLOBAL_AVATAR_BUFFER = Shader.PropertyToID("_GlobalAvatarBuffer");

        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IPerformanceBudget frameTimeCapBudget;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IRendererFeaturesCache rendererFeaturesCache;
        private readonly IRealmData realmData;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;

        private readonly AttachmentsAssetsCache attachmentsAssetsCache;

        // late init
        private IComponentPool<AvatarBase> avatarPoolRegistry = null!;
        private IAvatarMaterialPoolHandler avatarMaterialPoolHandler = null!;
        private IExtendedObjectPool<ComputeShader> computeShaderPool = null!;

        private readonly NametagsData nametagsData;

        private IComponentPool<Transform> transformPoolRegistry = null!;
        private Transform? poolParent = null;

        private IObjectPool<NametagView> nametagViewPool = null!;
        private TextureArrayContainer textureArrayContainer;

        private AvatarRandomizerAsset avatarRandomizerAsset;
        private ChatBubbleConfigurationSO chatBubbleConfiguration;

        private readonly TextureArrayContainerFactory textureArrayContainerFactory;
        private readonly IWearableStorage wearableStorage;
        private readonly AvatarTransformMatrixJobWrapper avatarTransformMatrixJobWrapper;

        private float startFadeDistanceDithering;
        private float endFadeDistanceDithering;

        private FacialFeaturesTextures[] facialFeaturesTextures;

        public AvatarPlugin(
            IComponentPoolsRegistry poolsRegistry,
            IAssetsProvisioner assetsProvisioner,
            IPerformanceBudget frameTimeCapBudget,
            IPerformanceBudget memoryBudget,
            IRendererFeaturesCache rendererFeaturesCache,
            IRealmData realmData,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            IDebugContainerBuilder debugContainerBuilder,
            CacheCleaner cacheCleaner,
            NametagsData nametagsData,
            TextureArrayContainerFactory textureArrayContainerFactory,
            IWearableStorage wearableStorage,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy
        )
        {
            this.assetsProvisioner = assetsProvisioner;
            this.frameTimeCapBudget = frameTimeCapBudget;
            this.realmData = realmData;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.debugContainerBuilder = debugContainerBuilder;
            this.cacheCleaner = cacheCleaner;
            this.memoryBudget = memoryBudget;
            this.rendererFeaturesCache = rendererFeaturesCache;
            this.nametagsData = nametagsData;
            this.textureArrayContainerFactory = textureArrayContainerFactory;
            this.wearableStorage = wearableStorage;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            componentPoolsRegistry = poolsRegistry;
            avatarTransformMatrixJobWrapper = new AvatarTransformMatrixJobWrapper();
            attachmentsAssetsCache = new AttachmentsAssetsCache(100, poolsRegistry);

            cacheCleaner.Register(attachmentsAssetsCache);
        }

        public void Dispose()
        {
            attachmentsAssetsCache.Dispose();
            avatarTransformMatrixJobWrapper.Dispose();
            UnityObjectUtils.SafeDestroyGameObject(poolParent);
        }

        public async UniTask InitializeAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            chatBubbleConfiguration = (await assetsProvisioner.ProvideMainAssetAsync(settings.ChatBubbleConfiguration, ct)).Value;
            startFadeDistanceDithering = settings.startFadeDistanceDithering;
            endFadeDistanceDithering = settings.endFadeDistanceDithering;

            await CreateAvatarBasePoolAsync(settings, ct);
            await CreateNametagPoolAsync(settings, ct);
            await CreateMaterialPoolPrewarmedAsync(settings, ct);
            await CreateComputeShaderPoolPrewarmedAsync(settings, ct);
            facialFeaturesTextures = await CreateDefaultFaceTexturesByBodyShapeAsync(settings, ct);

            transformPoolRegistry = componentPoolsRegistry.GetReferenceTypePool<Transform>().EnsureNotNull("ReferenceTypePool of type Transform not found in the registry");
            avatarRandomizerAsset = (await assetsProvisioner.ProvideMainAssetAsync(settings.AvatarRandomizerSettingsRef, ct)).Value;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var vertOutBuffer = new FixedComputeBufferHandler(5_000_000, Unsafe.SizeOf<CustomSkinningVertexInfo>());
            Shader.SetGlobalBuffer(GLOBAL_AVATAR_BUFFER, vertOutBuffer.Buffer);

            var skinningStrategy = new ComputeShaderSkinning();
            new NametagsDebugController(debugContainerBuilder, nametagsData);

            AvatarLoaderSystem.InjectToWorld(ref builder);

            cacheCleaner.Register(avatarPoolRegistry);
            cacheCleaner.Register(computeShaderPool);

            foreach (var extendedObjectPool in avatarMaterialPoolHandler.GetAllMaterialsPools())
                cacheCleaner.Register(extendedObjectPool.Pool);

            AvatarInstantiatorSystem.InjectToWorld(ref builder, frameTimeCapBudget, memoryBudget, avatarPoolRegistry, avatarMaterialPoolHandler,
                computeShaderPool, attachmentsAssetsCache, skinningStrategy, vertOutBuffer, mainPlayerAvatarBaseProxy,
                wearableStorage, avatarTransformMatrixJobWrapper, facialFeaturesTextures);

            MakeVertsOutBufferDefragmentationSystem.InjectToWorld(ref builder, vertOutBuffer, skinningStrategy);

            StartAvatarMatricesCalculationSystem.InjectToWorld(ref builder, avatarTransformMatrixJobWrapper);

            FinishAvatarMatricesCalculationSystem.InjectToWorld(ref builder, skinningStrategy,
                avatarTransformMatrixJobWrapper);

            AvatarShapeVisibilitySystem.InjectToWorld(ref builder, userBlockingCacheProxy, rendererFeaturesCache, startFadeDistanceDithering, endFadeDistanceDithering);

            AvatarCleanUpSystem.InjectToWorld(ref builder, frameTimeCapBudget, vertOutBuffer, avatarMaterialPoolHandler,
                avatarPoolRegistry, computeShaderPool, attachmentsAssetsCache, mainPlayerAvatarBaseProxy,
                avatarTransformMatrixJobWrapper);

            NametagPlacementSystem.InjectToWorld(ref builder, nametagViewPool, nametagsData, chatBubbleConfiguration);
            NameTagCleanUpSystem.InjectToWorld(ref builder, nametagsData, nametagViewPool);

            //Debug scripts
            InstantiateRandomAvatarsSystem.InjectToWorld(ref builder, debugContainerBuilder, realmData, transformPoolRegistry, avatarRandomizerAsset);
#if UNITY_EDITOR
            PlayableDirectorUpdatingSystem.InjectToWorld(ref builder);
#endif
        }

        private async UniTask CreateAvatarBasePoolAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            AvatarBase avatarBasePrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.AvatarBase, ct: ct)).Value.EnsureGetComponent<AvatarBase>();

            componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(avatarBasePrefab, Vector3.zero, Quaternion.identity));
            avatarPoolRegistry = componentPoolsRegistry.GetReferenceTypePool<AvatarBase>().EnsureNotNull("ReferenceTypePool of type AvatarBase not found in the registry");
        }

        private async UniTask CreateNametagPoolAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            NametagView nametagPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.NametagView, ct: ct)).Value.GetComponent<NametagView>();

            var poolRoot = componentPoolsRegistry.RootContainerTransform();
            poolParent = new GameObject("POOL_CONTAINER_NameTags").transform;
            poolParent.parent = poolRoot;

            nametagViewPool = new ObjectPool<NametagView>(
                () =>
                {
                    var nameTagView = Object.Instantiate(nametagPrefab, Vector3.zero, Quaternion.identity, poolParent);
                    nameTagView.gameObject.SetActive(false);
                    return nameTagView;
                },
                actionOnRelease: (nameTagView) => nameTagView.gameObject.SetActive(false),
                actionOnDestroy: UnityObjectUtils.SafeDestroy);
        }

        private async UniTask CreateMaterialPoolPrewarmedAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            Material toonMaterial = (await assetsProvisioner.ProvideMainAssetAsync(settings.CelShadingMaterial, ct: ct)).Value;
            Material faceFeatureMaterial = (await assetsProvisioner.ProvideMainAssetAsync(settings.FaceFeatureMaterial, ct: ct)).Value;

#if UNITY_EDITOR

            //Avoid generating noise in editor git by creating a copy of the material
            toonMaterial = new Material(toonMaterial);
            faceFeatureMaterial = new Material(faceFeatureMaterial);
#endif

            //Set initial dither properties obtained through settings
            toonMaterial.SetFloat(ComputeShaderConstants.SHADER_FADING_DISTANCE_START_PARAM_ID, startFadeDistanceDithering);
            faceFeatureMaterial.SetFloat(ComputeShaderConstants.SHADER_FADING_DISTANCE_START_PARAM_ID, startFadeDistanceDithering);

            toonMaterial.SetFloat(ComputeShaderConstants.SHADER_FADING_DISTANCE_END_PARAM_ID, endFadeDistanceDithering);
            faceFeatureMaterial.SetFloat(ComputeShaderConstants.SHADER_FADING_DISTANCE_PARAM_ID, startFadeDistanceDithering);

            //Default should be visible
            toonMaterial.SetFloat(ComputeShaderConstants.SHADER_FADING_DISTANCE_PARAM_ID, startFadeDistanceDithering);
            faceFeatureMaterial.SetFloat(ComputeShaderConstants.SHADER_FADING_DISTANCE_PARAM_ID, startFadeDistanceDithering);

            avatarMaterialPoolHandler = new AvatarMaterialPoolHandler(new List<Material>
            {
                toonMaterial, faceFeatureMaterial,
            }, settings.defaultMaterialCapacity, textureArrayContainerFactory);
        }

        private async UniTask CreateComputeShaderPoolPrewarmedAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            ProvidedAsset<ComputeShader> providedComputeShader = await assetsProvisioner.ProvideMainAssetAsync(settings.ComputeShader, ct: ct);
            computeShaderPool = new ExtendedObjectPool<ComputeShader>(() => Object.Instantiate(providedComputeShader.Value), actionOnDestroy: UnityObjectUtils.SafeDestroy, defaultCapacity: settings.defaultMaterialCapacity);

            for (var i = 0; i < PoolConstants.COMPUTE_SHADER_COUNT; i++)
            {
                ComputeShader prewarmedShader = computeShaderPool.Get()!;
                computeShaderPool.Release(prewarmedShader);
            }
        }

        private async UniTask<FacialFeaturesTextures[]> CreateDefaultFaceTexturesByBodyShapeAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            var maleMouthTexture = (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultMaleMouthTexture, ct: ct)).Value;
            var maleEyebrowsTexture = (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultMaleEyebrowsTexture, ct: ct)).Value;
            var maleEyesTexture = (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultMaleEyesTexture, ct: ct)).Value;
            var femaleMouthTexture = (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultFemaleMouthTexture, ct: ct)).Value;
            var femaleEyebrowsTexture = (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultFemaleEyebrowsTexture, ct: ct)).Value;
            var femaleEyesTexture = (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultFemaleEyesTexture, ct: ct)).Value;

            return new FacialFeaturesTextures[]
            {
                new (new Dictionary<string, Dictionary<int, Texture>>
                {
                    [WearablesConstants.Categories.EYES] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = maleEyesTexture },
                    [WearablesConstants.Categories.MOUTH] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = maleMouthTexture },
                    [WearablesConstants.Categories.EYEBROWS] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = maleEyebrowsTexture },
                }),
                new (new Dictionary<string, Dictionary<int, Texture>>
                {
                    [WearablesConstants.Categories.EYES] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = femaleEyesTexture },
                    [WearablesConstants.Categories.MOUTH] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = femaleMouthTexture },
                    [WearablesConstants.Categories.EYEBROWS] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = femaleEyebrowsTexture },
                }),
            };
        }

        [Serializable]
        public class AvatarShapeSettings : IDCLPluginSettings
        {
            [field: Header(nameof(AvatarPlugin) + "." + nameof(AvatarShapeSettings))]
            [field: Space]
            [field: SerializeField]
            private AssetReferenceGameObject? avatarBase;

            [field: Space]
            [field: SerializeField]
            private AssetReferenceGameObject? nametagView;

            [field: SerializeField]
            private AssetReferenceMaterial? celShadingMaterial;

            [field: SerializeField]
            private AssetReferenceMaterial? faceFeatureMaterial;

            [field: SerializeField]
            public float startFadeDistanceDithering = 2;

            [field: SerializeField]
            public float endFadeDistanceDithering = 0.8f;

            [field: SerializeField]
            public int defaultMaterialCapacity = 100;

            [field: SerializeField]
            public AssetReferenceT<ChatBubbleConfigurationSO> ChatBubbleConfiguration { get; private set; }

            [field: SerializeField]
            public AssetReferenceComputeShader computeShader;

            [field: SerializeField]
            public StaticSettings.AvatarRandomizerSettingsRef AvatarRandomizerSettingsRef { get; set; }

            public AssetReferenceGameObject AvatarBase => avatarBase.EnsureNotNull();

            public AssetReferenceGameObject NametagView => nametagView.EnsureNotNull();

            public AssetReferenceComputeShader ComputeShader => computeShader.EnsureNotNull();

            public AssetReferenceMaterial CelShadingMaterial => celShadingMaterial.EnsureNotNull();

            public AssetReferenceMaterial FaceFeatureMaterial => faceFeatureMaterial.EnsureNotNull();

            public AssetReferenceT<Texture> DefaultMaleMouthTexture;
            public AssetReferenceT<Texture> DefaultMaleEyesTexture;
            public AssetReferenceT<Texture> DefaultMaleEyebrowsTexture;
            public AssetReferenceT<Texture> DefaultFemaleMouthTexture;
            public AssetReferenceT<Texture> DefaultFemaleEyesTexture;
            public AssetReferenceT<Texture> DefaultFemaleEyebrowsTexture;

            [Serializable]
            public class NametagsDataRef : AssetReferenceT<NametagsData>
            {
                public NametagsDataRef(string guid) : base(guid) { }
            }
        }
    }
}
