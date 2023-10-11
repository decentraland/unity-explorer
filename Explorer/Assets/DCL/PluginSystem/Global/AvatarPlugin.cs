using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.DemoScripts.UI;
using DCL.AvatarRendering.AvatarShape.GPUSkinning;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.ECSComponents;
using ECS;
using ECS.ComponentsPooling;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Utility;
using Utility.Pool;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class AvatarPlugin : IDCLGlobalPlugin<AvatarPlugin.AvatarShapeSettings>
    {
        private static readonly QueryDescription AVATARS_QUERY = new QueryDescription().WithAll<PBAvatarShape>().WithNone<PlayerComponent>();

        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IConcurrentBudgetProvider frameTimeCapBudgetProvider;
        private readonly IRealmData realmData;
        private readonly TextureArrayContainer textureArrayContainer;

        private readonly WearableAssetsCache wearableAssetsCache = new (3, 100);

        private ProvidedInstance<AvatarInstantiatorView> avatarInstantiatorView;
        private IComponentPool<AvatarBase> avatarPoolRegistry;

        private IObjectPool<Material> celShadingMaterialPool;
        private IObjectPool<ComputeShader> computeShaderPool;

        public AvatarPlugin(IComponentPoolsRegistry poolsRegistry, IAssetsProvisioner assetsProvisioner, IConcurrentBudgetProvider frameTimeCapBudgetProvider, IRealmData realmData)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.frameTimeCapBudgetProvider = frameTimeCapBudgetProvider;
            this.realmData = realmData;
            componentPoolsRegistry = poolsRegistry;
            textureArrayContainer = new TextureArrayContainer();
        }

        public async UniTask Initialize(AvatarShapeSettings settings, CancellationToken ct)
        {
            AvatarBase avatarBasePrefab = (await assetsProvisioner.ProvideMainAsset(settings.avatarBase, ct: ct)).Value.GetComponent<AvatarBase>();
            componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(avatarBasePrefab, Vector3.zero, Quaternion.identity));

            //TODO: Does it make sense to prewarm using a for?
            ProvidedAsset<Material> providedMaterial = await assetsProvisioner.ProvideMainAsset(settings.celShadingMaterial, ct: ct);
            celShadingMaterialPool = new ObjectPool<Material>(() => new Material(providedMaterial.Value), actionOnDestroy: UnityObjectUtils.SafeDestroy, defaultCapacity: settings.defaultMaterialCapacity);

            for (var i = 0; i < settings.defaultMaterialCapacity; i++)
            {
                Material prewarmedMaterial = celShadingMaterialPool.Get();
                celShadingMaterialPool.Release(prewarmedMaterial);
            }

            ProvidedAsset<ComputeShader> providedComputeShader = await assetsProvisioner.ProvideMainAsset(settings.computeShader, ct: ct);
            computeShaderPool = new ObjectPool<ComputeShader>(() => Object.Instantiate(providedComputeShader.Value), actionOnDestroy: UnityObjectUtils.SafeDestroy, defaultCapacity: settings.defaultMaterialCapacity);

            for (var i = 0; i < PoolConstants.COMPUTE_SHADER_COUNT; i++)
            {
                ComputeShader prewarmedShader = computeShaderPool.Get();
                computeShaderPool.Release(prewarmedShader);
            }

            avatarInstantiatorView = await assetsProvisioner.ProvideInstance(settings.avatarInstantiatorViewRef, ct: ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var vertOutBuffer = new FixedComputeBufferHandler(5_000_000, Unsafe.SizeOf<CustomSkinningVertexInfo>());
            Shader.SetGlobalBuffer("_GlobalAvatarBuffer", vertOutBuffer.Buffer);

            var skinningStrategy = new ComputeShaderSkinning();

            AvatarLoaderSystem.InjectToWorld(ref builder);

            AvatarInstantiatorSystem.InjectToWorld(ref builder, frameTimeCapBudgetProvider, componentPoolsRegistry.GetReferenceTypePool<AvatarBase>(), celShadingMaterialPool,
                computeShaderPool, textureArrayContainer, wearableAssetsCache, skinningStrategy, vertOutBuffer);

            MakeVertsOutBufferDefragmentationSystem.InjectToWorld(ref builder, vertOutBuffer, skinningStrategy);

            StartAvatarMatricesCalculationSystem.InjectToWorld(ref builder);
            FinishAvatarMatricesCalculationSystem.InjectToWorld(ref builder, skinningStrategy);

            //Debug scripts
            InstantiateRandomAvatarsSystem.InjectToWorld(ref builder, avatarInstantiatorView.Value, realmData, AVATARS_QUERY);
        }

        public void Dispose()
        {
            wearableAssetsCache.Dispose();
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

            [field: Header(nameof(AvatarPlugin) + ".Debug")]
            [field: Space]
            [field: SerializeField]
            public AvatarInstantiatorViewRef avatarInstantiatorViewRef { get; private set; }

            [Serializable]
            public class AvatarInstantiatorViewRef : ComponentReference<AvatarInstantiatorView>
            {
                public AvatarInstantiatorViewRef(string guid) : base(guid) { }
            }
        }
    }
}
