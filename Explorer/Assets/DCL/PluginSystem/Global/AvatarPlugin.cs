using Arch.Core;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.GPUSkinning;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.PluginSystem.Global;
using ECS.ComponentsPooling;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;

public class AvatarPlugin : IDCLGlobalPluginWithoutSettings
{
    //TODO: Keep it as setting or load it through the pool?
    /*[Serializable]
    public class AvatarShapeSettings : IDCLPluginSettings
    {
        [field: SerializeField]
        public AssetReferenceGameObject avatarBase;
    }
        private readonly IAssetsProvisioner assetsProvisioner;
    private ProvidedAsset<GameObject> providedAvatarBase;
    public async UniTask Initialize(AvatarShapeSettings settings, CancellationToken ct)
    {
        providedAvatarBase = await assetsProvisioner.ProvideMainAsset(settings.avatarBase, ct: ct);
    }

    */

    private readonly IConcurrentBudgetProvider frameTimeCapBudgetProvider;
    private readonly IComponentPool<AvatarBase> avatarPoolRegistry;

    public AvatarPlugin(IConcurrentBudgetProvider frameTimeCapBudgetProvider, IComponentPool<AvatarBase> avatarPoolRegistry)
    {
        this.frameTimeCapBudgetProvider = frameTimeCapBudgetProvider;
        this.avatarPoolRegistry = avatarPoolRegistry;
    }

    public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
    {
        AvatarSystem.InjectToWorld(ref builder, frameTimeCapBudgetProvider, avatarPoolRegistry);
        InstantiateRandomAvatarsSystem.InjectToWorld(ref builder);
        StartAvatarMatricesCalculationSystem.InjectToWorld(ref builder);
    }
}
