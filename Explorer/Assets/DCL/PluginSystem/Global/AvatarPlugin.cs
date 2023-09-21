using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Systems;
using ECS.ComponentsPooling;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class AvatarPlugin : IDCLGlobalPlugin<AvatarPlugin.AvatarShapeSettings>
    {
        [Serializable]
        public class AvatarShapeSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceGameObject avatarBase;
        }

        private readonly IAssetsProvisioner assetsProvisioner;
        private ProvidedAsset<GameObject> providedAvatarBase;
        private readonly IConcurrentBudgetProvider frameTimeCapBudgetProvider;
        private readonly IComponentPool<AvatarBase> avatarPoolRegistry;

        public async UniTask Initialize(AvatarShapeSettings settings, CancellationToken ct)
        {
            providedAvatarBase = await assetsProvisioner.ProvideMainAsset(settings.avatarBase, ct: ct);
            AvatarPoolUtils.AvatarBasePrefab = providedAvatarBase.Value.GetComponent<AvatarBase>();
        }

        public AvatarPlugin(IAssetsProvisioner assetsProvisioner, IConcurrentBudgetProvider frameTimeCapBudgetProvider, IComponentPool<AvatarBase> avatarPoolRegistry)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.frameTimeCapBudgetProvider = frameTimeCapBudgetProvider;
            this.avatarPoolRegistry = avatarPoolRegistry;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            AvatarSystem.InjectToWorld(ref builder, frameTimeCapBudgetProvider, avatarPoolRegistry);
            InstantiateRandomAvatarsSystem.InjectToWorld(ref builder);
        }

        public void Dispose() { }

    }

}
