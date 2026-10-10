using Arch.SystemGroups;
using ECS;
using ECS.SceneLifeCycle.Realm.Systems;

namespace DCL.PluginSystem.Global
{
    public class RealmDataDirtyFlagPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly RealmData realmData;

        public RealmDataDirtyFlagPlugin(RealmData realmData)
        {
            this.realmData = realmData;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ResetRealmDataDirtyFlagSystem.InjectToWorld(ref builder, realmData);
        }
    }
}
