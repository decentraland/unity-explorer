using Arch.Core;
using Arch.SystemGroups;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape;
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
    private readonly string CATALYST_URL;
    private readonly string ENTITIES_ACTIVE;

    public AvatarPlugin(IAssetsProvisioner assetsProvisioner, IConcurrentBudgetProvider frameTimeCapBudgetProvider, IComponentPool<AvatarBase> avatarPoolRegistry, string catalystURL, string entitiesActiveURL)
    {
        this.frameTimeCapBudgetProvider = frameTimeCapBudgetProvider;
        this.avatarPoolRegistry = avatarPoolRegistry;
        CATALYST_URL = catalystURL;
        ENTITIES_ACTIVE = entitiesActiveURL;
    }
    public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
    {
        PrepareAvatarSystem.InjectToWorld(ref builder, $"{CATALYST_URL}{ENTITIES_ACTIVE}");
        InstantiateAvatarSystem.InjectToWorld(ref builder, frameTimeCapBudgetProvider, avatarPoolRegistry);
        InstantiateRandomAvatarsSystem.InjectToWorld(ref builder);
    }




}
