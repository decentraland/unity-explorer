using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using ECS.Abstract;
using Realm;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    //TODO: System that will resolve lod state (Manifest and ABs)
    public partial class ResolveLODContentSystem : BaseUnityLoopSystem
    {
        public ResolveLODContentSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
        }
    }
}