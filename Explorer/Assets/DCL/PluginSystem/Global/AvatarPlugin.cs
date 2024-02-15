using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.GPUSkinning;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.DemoScripts.Systems;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.Chat;
using DCL.DebugUtilities;
using DCL.ECSComponents;
using DCL.Nametags;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using ECS;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class AvatarPlugin : IDCLGlobalPlugin<AvatarPlugin.AvatarShapeSettings>
    {
        private static readonly int GLOBAL_AVATAR_BUFFER = Shader.PropertyToID("_GlobalAvatarBuffer");
        private static readonly QueryDescription AVATARS_QUERY = new QueryDescription().WithAll<PBAvatarShape>().WithNone<PlayerComponent>();

        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IPerformanceBudget frameTimeCapBudget;
        private readonly MainPlayerAvatarBase mainPlayerAvatarBase;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IRealmData realmData;
        private readonly IDebugContainerBuilder debugContainerBuilder;

        private readonly WearableAssetsCache wearableAssetsCache = new (100);
        private readonly CacheCleaner cacheCleaner;
        private readonly IPerformanceBudget memoryBudget;

        private TextureArrayContainer textureArrayContainer;
        private IComponentPool<Transform> transformPoolRegistry;

        private IComponentPool<AvatarBase> avatarPoolRegistry;
        private IExtendedObjectPool<Material> celShadingMaterialPool;
        private IExtendedObjectPool<ComputeShader> computeShaderPool;

        private IComponentPool<Transform> transformPoolRegistry;

        private IObjectPool<NametagView> nametagViewPool;

        public AvatarPlugin(
            IComponentPoolsRegistry poolsRegistry,
            IAssetsProvisioner assetsProvisioner,
            IPerformanceBudget frameTimeCapBudget,
            IPerformanceBudget memoryBudget,
            IRealmData realmData,
            MainPlayerAvatarBase mainPlayerAvatarBase,
            IDebugContainerBuilder debugContainerBuilder,
            CacheCleaner cacheCleaner,
            ChatEntryConfigurationSO chatEntryConfiguration)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.frameTimeCapBudget = frameTimeCapBudget;
            this.realmData = realmData;
            this.mainPlayerAvatarBase = mainPlayerAvatarBase;
            this.debugContainerBuilder = debugContainerBuilder;
            this.cacheCleaner = cacheCleaner;
            this.chatEntryConfiguration = chatEntryConfiguration;
            this.memoryBudget = memoryBudget;
            componentPoolsRegistry = poolsRegistry;

            cacheCleaner.Register(wearableAssetsCache);
        }

        public void Dispose()
        {
            wearableAssetsCache.Dispose();
        }

        public async UniTask InitializeAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            await CreateAvatarBasePoolAsync(settings, ct);
            await CreateNametagPoolAsync(settings, ct);
            await CreateMaterialPoolPrewarmedAsync(settings, ct);
            await CreateComputeShaderPoolPrewarmedAsync(settings, ct);

            transformPoolRegistry = componentPoolsRegistry.GetReferenceTypePool<Transform>();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var vertOutBuffer = new FixedComputeBufferHandler(5_000_000, Unsafe.SizeOf<CustomSkinningVertexInfo>());
            Shader.SetGlobalBuffer(GLOBAL_AVATAR_BUFFER, vertOutBuffer.Buffer);

            var skinningStrategy = new ComputeShaderSkinning();

            AvatarLoaderSystem.InjectToWorld(ref builder);

            cacheCleaner.Register(avatarPoolRegistry);
            cacheCleaner.Register(celShadingMaterialPool);
            cacheCleaner.Register(computeShaderPool);

            AvatarInstantiatorSystem.InjectToWorld(ref builder, frameTimeCapBudget, memoryBudget, avatarPoolRegistry, celShadingMaterialPool,
                computeShaderPool, textureArrayContainer, wearableAssetsCache, skinningStrategy, vertOutBuffer, mainPlayerAvatarBase);

            MakeVertsOutBufferDefragmentationSystem.InjectToWorld(ref builder, vertOutBuffer, skinningStrategy);

            StartAvatarMatricesCalculationSystem.InjectToWorld(ref builder);
            FinishAvatarMatricesCalculationSystem.InjectToWorld(ref builder, skinningStrategy);

            AvatarShapeVisibilitySystem.InjectToWorld(ref builder);

            //Debug scripts
            InstantiateRandomAvatarsSystem.InjectToWorld(ref builder, debugContainerBuilder, realmData, AVATARS_QUERY, transformPoolRegistry);
            NametagPlacementSystem.InjectToWorld(ref builder, nametagViewPool, chatEntryConfiguration);
        }

        private async UniTask CreateAvatarBasePoolAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            AvatarBase avatarBasePrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.avatarBase, ct: ct)).Value.GetComponent<AvatarBase>();

            componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(avatarBasePrefab, Vector3.zero, Quaternion.identity));
            avatarPoolRegistry = componentPoolsRegistry.GetReferenceTypePool<AvatarBase>();
        }

        private async UniTask CreateNametagPoolAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            NametagView nametagPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.nametagView, ct: ct)).Value.GetComponent<NametagView>();
            GameObject nametagPrefabParent = (await assetsProvisioner.ProvideMainAssetAsync(settings.nametagParent, ct: ct)).Value;

            GameObject nametagParent = Object.Instantiate(nametagPrefabParent, Vector3.zero, Quaternion.identity);

            nametagViewPool = new ObjectPool<NametagView>(
                () => Object.Instantiate(nametagPrefab, Vector3.zero, Quaternion.identity, nametagParent.transform),
                actionOnGet: (nametagView) => nametagView.gameObject.SetActive(true),
                actionOnRelease: (nametagView) => nametagView.gameObject.SetActive(false),
                actionOnDestroy: UnityObjectUtils.SafeDestroy);
        }

        private async UniTask CreateMaterialPoolPrewarmedAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            ProvidedAsset<Material> providedMaterial = await assetsProvisioner.ProvideMainAssetAsync(settings.celShadingMaterial, ct: ct);

            textureArrayContainer = TextureArrayContainerFactory.GetCached(providedMaterial.Value.shader);

            celShadingMaterialPool = new ExtendedObjectPool<Material>(
                () =>
                {
                    var mat = new Material(providedMaterial.Value);
                    return mat;
                },
                actionOnRelease: mat =>
                {
                    // reset material so it does not contain any old properties
                    mat.CopyPropertiesFromMaterial(providedMaterial.Value);
                },
                actionOnDestroy: UnityObjectUtils.SafeDestroy,
                defaultCapacity: settings.defaultMaterialCapacity);

            for (var i = 0; i < settings.defaultMaterialCapacity; i++)
            {
                Material prewarmedMaterial = celShadingMaterialPool.Get();
                celShadingMaterialPool.Release(prewarmedMaterial);
            }
        }

        private async UniTask CreateComputeShaderPoolPrewarmedAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            ProvidedAsset<ComputeShader> providedComputeShader = await assetsProvisioner.ProvideMainAssetAsync(settings.computeShader, ct: ct);
            computeShaderPool = new ExtendedObjectPool<ComputeShader>(() => Object.Instantiate(providedComputeShader.Value), actionOnDestroy: UnityObjectUtils.SafeDestroy, defaultCapacity: settings.defaultMaterialCapacity);

            for (var i = 0; i < PoolConstants.COMPUTE_SHADER_COUNT; i++)
            {
                ComputeShader prewarmedShader = computeShaderPool.Get();
                computeShaderPool.Release(prewarmedShader);
            }
        }

        [Serializable]
        public class AvatarShapeSettings : IDCLPluginSettings
        {
            [field: Header(nameof(AvatarPlugin) + "." + nameof(AvatarShapeSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject avatarBase;

            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject nametagView;

            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject nametagParent;

            [field: SerializeField]
            public AssetReferenceMaterial celShadingMaterial;

            [field: SerializeField]
            public int defaultMaterialCapacity = 100;

            [field: SerializeField]
            public AssetReferenceComputeShader computeShader;
        }
    }
}
