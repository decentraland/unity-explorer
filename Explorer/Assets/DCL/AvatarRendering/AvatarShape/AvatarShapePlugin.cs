using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class AvatarShapePlugin : IDCLGlobalPlugin<AvatarShapePlugin.AvatarShapeSettings>
{
    [Serializable]
    public class AvatarShapeSettings : IDCLPluginSettings
    {
        [field: SerializeField]
        public AssetReferenceGameObject avatarBase;
    }

    private readonly IAssetsProvisioner assetsProvisioner;
    private ProvidedAsset<GameObject> providedAvatarBase;

    public AvatarShapePlugin(IAssetsProvisioner assetsProvisioner)
    {
        this.assetsProvisioner = assetsProvisioner;
    }

    public async UniTask Initialize(AvatarShapeSettings settings, CancellationToken ct)
    {
        providedAvatarBase = await assetsProvisioner.ProvideMainAsset(settings.avatarBase, ct: ct);
    }

    public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
    {
        StartAvatarLoadSystem.InjectToWorld(ref builder);
        InstantiateAvatarSystem.InjectToWorld(ref builder, new NullBudgetProvider(), providedAvatarBase.Value);
    }

    public void Dispose() { }



}
