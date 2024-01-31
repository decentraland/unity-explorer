using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.GPUSkinning;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.DemoScripts.Systems;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.DebugUtilities;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using ECS;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class AvatarPlugin : IDCLGlobalPlugin<AvatarPlugin.AvatarShapeSettings>
    {
        private static readonly int GLOBAL_AVATAR_BUFFER = Shader.PropertyToID("_GlobalAvatarBuffer");
        private static readonly QueryDescription AVATARS_QUERY = new QueryDescription().WithAll<PBAvatarShape>().WithNone<PlayerComponent>();

        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IPerformanceBudget frameTimeCapBudget;
        private readonly IRealmData realmData;
        private readonly TextureArrayContainer textureArrayContainer;
        private readonly IDebugContainerBuilder debugContainerBuilder;

        private readonly WearableAssetsCache wearableAssetsCache = new (100);
        private readonly CacheCleaner cacheCleaner;
        private readonly IPerformanceBudget memoryBudget;

        private IComponentPool<Transform> transformPoolRegistry;

        private IComponentPool<AvatarBase> avatarPoolRegistry;
        private IExtendedObjectPool<Material> celShadingMaterialPool;
        private IExtendedObjectPool<ComputeShader> computeShaderPool;

        public AvatarPlugin(IComponentPoolsRegistry poolsRegistry, IAssetsProvisioner assetsProvisioner,
            IPerformanceBudget frameTimeCapBudget, IPerformanceBudget memoryBudget,
            IRealmData realmData, IDebugContainerBuilder debugContainerBuilder, CacheCleaner cacheCleaner)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.frameTimeCapBudget = frameTimeCapBudget;
            this.realmData = realmData;
            this.debugContainerBuilder = debugContainerBuilder;
            this.cacheCleaner = cacheCleaner;
            this.memoryBudget = memoryBudget;
            componentPoolsRegistry = poolsRegistry;
            textureArrayContainer = new TextureArrayContainer();

            cacheCleaner.Register(wearableAssetsCache);
        }

        public void Dispose()
        {
            wearableAssetsCache.Dispose();
        }

        public async UniTask InitializeAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            await CreateAvatarBasePoolAsync(settings, ct);
            await CreateMaterialPoolPrewarmedAsync(settings, ct);
            await CreateComputeShaderPoolPrewarmedAsync(settings, ct);

            transformPoolRegistry = componentPoolsRegistry.GetReferenceTypePool<Transform>();
        }

        private async UniTask CreateAvatarBasePoolAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            AvatarBase avatarBasePrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.avatarBase, ct: ct)).Value.GetComponent<AvatarBase>();

            componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(avatarBasePrefab, Vector3.zero, Quaternion.identity));
            avatarPoolRegistry = componentPoolsRegistry.GetReferenceTypePool<AvatarBase>();
        }

        private async UniTask CreateMaterialPoolPrewarmedAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            ProvidedAsset<Material> providedMaterial = await assetsProvisioner.ProvideMainAssetAsync(settings.celShadingMaterial, ct: ct);

            celShadingMaterialPool = new ExtendedObjectPool<Material>(
                () => new Material(providedMaterial.Value),
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
                computeShaderPool, textureArrayContainer, wearableAssetsCache, skinningStrategy, vertOutBuffer);

            MakeVertsOutBufferDefragmentationSystem.InjectToWorld(ref builder, vertOutBuffer, skinningStrategy);

            StartAvatarMatricesCalculationSystem.InjectToWorld(ref builder);
            FinishAvatarMatricesCalculationSystem.InjectToWorld(ref builder, skinningStrategy);

            AvatarShapeVisibilitySystem.InjectToWorld(ref builder);

            //Debug scripts
            InstantiateRandomAvatarsSystem.InjectToWorld(ref builder, debugContainerBuilder, realmData, AVATARS_QUERY, transformPoolRegistry);
        }

        [Serializable]
        public class AvatarShapeSettings : IDCLPluginSettings
        {
            [field: Header(nameof(AvatarPlugin) + "." + nameof(AvatarShapeSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject avatarBase;

            [field: SerializeField]
            public AssetReferenceMaterial celShadingMaterial;

            [field: SerializeField]
            public int defaultMaterialCapacity = 100;

            [field: SerializeField]
            public AssetReferenceComputeShader computeShader;
        }
    }
}
