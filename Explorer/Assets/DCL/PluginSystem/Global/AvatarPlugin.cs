using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.DemoScripts.Systems;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Chat;
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
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.Multiplayer.Profiles.Tables;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;
using StartAvatarMatricesCalculationSystem = DCL.AvatarRendering.AvatarShape.Systems.StartAvatarMatricesCalculationSystem;

namespace DCL.PluginSystem.Global
{
    public class AvatarPlugin : IDCLGlobalPlugin<AvatarPlugin.AvatarShapeSettings>
    {
        private static readonly int GLOBAL_AVATAR_BUFFER = Shader.PropertyToID("_GlobalAvatarBuffer");

        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IPerformanceBudget frameTimeCapBudget;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IRealmData realmData;

        private readonly WearableAssetsCache wearableAssetsCache = new (100);

        // late init
        private IComponentPool<AvatarBase> avatarPoolRegistry = null!;
        private IAvatarMaterialPoolHandler avatarMaterialPoolHandler  = null!;
        private IExtendedObjectPool<ComputeShader> computeShaderPool = null!;

        private NametagsData nametagsData;

        private IComponentPool<Transform> transformPoolRegistry = null!;

        private IObjectPool<NametagView> nametagViewPool = null!;
        private TextureArrayContainer textureArrayContainer;

        private AvatarRandomizerAsset avatarRandomizerAsset;
        private ChatBubbleConfigurationSO chatBubbleConfiguration;

        private readonly DefaultFaceFeaturesHandler defaultFaceFeaturesHandler;
        private readonly IEntityParticipantTable entityParticipantTable;
        private readonly TextureArrayContainerFactory textureArrayContainerFactory;

        public AvatarPlugin(
            IComponentPoolsRegistry poolsRegistry,
            IAssetsProvisioner assetsProvisioner,
            IPerformanceBudget frameTimeCapBudget,
            IPerformanceBudget memoryBudget,
            IRealmData realmData,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            IDebugContainerBuilder debugContainerBuilder,
            CacheCleaner cacheCleaner,
            ChatEntryConfigurationSO chatEntryConfiguration,
            DefaultFaceFeaturesHandler defaultFaceFeaturesHandler,
            IEntityParticipantTable entityParticipantTable,
            NametagsData nametagsData,
            TextureArrayContainerFactory textureArrayContainerFactory)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.frameTimeCapBudget = frameTimeCapBudget;
            this.realmData = realmData;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.debugContainerBuilder = debugContainerBuilder;
            this.cacheCleaner = cacheCleaner;
            this.chatEntryConfiguration = chatEntryConfiguration;
            this.defaultFaceFeaturesHandler = defaultFaceFeaturesHandler;
            this.entityParticipantTable = entityParticipantTable;
            this.memoryBudget = memoryBudget;
            this.nametagsData = nametagsData;
            this.textureArrayContainerFactory = textureArrayContainerFactory;
            componentPoolsRegistry = poolsRegistry;

            cacheCleaner.Register(wearableAssetsCache);
        }

        public void Dispose()
        {
            wearableAssetsCache.Dispose();
        }

        public async UniTask InitializeAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            chatBubbleConfiguration = (await assetsProvisioner.ProvideMainAssetAsync(settings.ChatBubbleConfiguration, ct)).Value;

            await CreateAvatarBasePoolAsync(settings, ct);
            await CreateNametagPoolAsync(settings, ct);
            await CreateMaterialPoolPrewarmedAsync(settings, ct);
            await CreateComputeShaderPoolPrewarmedAsync(settings, ct);

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
                computeShaderPool, wearableAssetsCache, skinningStrategy, vertOutBuffer, mainPlayerAvatarBaseProxy, defaultFaceFeaturesHandler);

            MakeVertsOutBufferDefragmentationSystem.InjectToWorld(ref builder, vertOutBuffer, skinningStrategy);

            StartAvatarMatricesCalculationSystem.InjectToWorld(ref builder);
            FinishAvatarMatricesCalculationSystem.InjectToWorld(ref builder, skinningStrategy);

            AvatarShapeVisibilitySystem.InjectToWorld(ref builder);
            AvatarCleanUpSystem.InjectToWorld(ref builder, frameTimeCapBudget, vertOutBuffer, avatarMaterialPoolHandler, avatarPoolRegistry, computeShaderPool, wearableAssetsCache, mainPlayerAvatarBaseProxy);
            TrackTransformMatrixSystem.InjectToWorld(ref builder);

            NametagPlacementSystem.InjectToWorld(ref builder, nametagViewPool, chatEntryConfiguration, nametagsData, chatBubbleConfiguration);

            //Debug scripts
            InstantiateRandomAvatarsSystem.InjectToWorld(ref builder, debugContainerBuilder, realmData, entityParticipantTable, transformPoolRegistry, avatarRandomizerAsset);
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

            nametagViewPool = new ObjectPool<NametagView>(
                () => Object.Instantiate(nametagPrefab, Vector3.zero, Quaternion.identity, null),
                actionOnGet: (nametagView) => nametagView.gameObject.SetActive(true),
                actionOnRelease: (nametagView) => nametagView.gameObject.SetActive(false),
                actionOnDestroy: UnityObjectUtils.SafeDestroy);
        }

        private async UniTask CreateMaterialPoolPrewarmedAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            ProvidedAsset<Material> toonMaterial = await assetsProvisioner.ProvideMainAssetAsync(settings.CelShadingMaterial, ct: ct);
            ProvidedAsset<Material> faceFeatureMaterial = await assetsProvisioner.ProvideMainAssetAsync(settings.FaceFeatureMaterial, ct: ct);

            avatarMaterialPoolHandler = new AvatarMaterialPoolHandler(new List<Material>
            {
                toonMaterial.Value, faceFeatureMaterial.Value
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

            [Serializable]
            public class NametagsDataRef : AssetReferenceT<NametagsData>
            {
                public NametagsDataRef(string guid) : base(guid) { }
            }
        }
    }
}
