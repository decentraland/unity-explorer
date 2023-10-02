using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.DemoScripts.UI;
using DCL.AvatarRendering.AvatarShape.GPUSkinning;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using DCL.AvatarRendering.AvatarShape.Systems;
using ECS;
using ECS.ComponentsPooling;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using System;
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

            [Serializable]
            public class AvatarInstantiatorViewRef : ComponentReference<AvatarInstantiatorView>
            {
                public AvatarInstantiatorViewRef(string guid) : base(guid) { }
            }

            [field: Header(nameof(AvatarPlugin) + ".Debug")]
            [field: Space]
            [field: SerializeField]
            public AvatarInstantiatorViewRef avatarInstantiatorViewRef { get; private set; }

        }

        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IConcurrentBudgetProvider frameTimeCapBudgetProvider;
        private readonly IComponentPool<AvatarBase> avatarPoolRegistry;

        private IObjectPool<Material> celShadingMaterialPool;
        private IObjectPool<ComputeShader> computeShaderPool;
        private readonly TextureArrayContainer textureArrayContainer;

        private ProvidedInstance<AvatarInstantiatorView> avatarInstantiatorView;
        private readonly IRealmData realmData;


        public async UniTask Initialize(AvatarShapeSettings settings, CancellationToken ct)
        {
            //TODO: Check this static reference assignation
            ProvidedAsset<GameObject> providedAvatarBase = await assetsProvisioner.ProvideMainAsset(settings.avatarBase, ct: ct);
            AvatarPoolUtils.AvatarBasePrefab = providedAvatarBase.Value.GetComponent<AvatarBase>();

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

        public AvatarPlugin(IAssetsProvisioner assetsProvisioner, IConcurrentBudgetProvider frameTimeCapBudgetProvider, IComponentPool<AvatarBase> avatarPoolRegistry, IRealmData realmData)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.frameTimeCapBudgetProvider = frameTimeCapBudgetProvider;
            this.avatarPoolRegistry = avatarPoolRegistry;
            this.realmData = realmData;
            textureArrayContainer = new TextureArrayContainer();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            AvatarSystem.InjectToWorld(ref builder, frameTimeCapBudgetProvider, avatarPoolRegistry, celShadingMaterialPool, computeShaderPool, textureArrayContainer);
            StartAvatarMatricesCalculationSystem.InjectToWorld(ref builder);

            //Debug scripts
            InstantiateRandomAvatarsSystem.InjectToWorld(ref builder, avatarInstantiatorView.Value, realmData);
        }

        public void Dispose() { }


    }
}
