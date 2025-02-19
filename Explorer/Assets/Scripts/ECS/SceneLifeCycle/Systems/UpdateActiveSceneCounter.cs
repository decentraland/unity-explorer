using Arch.Core;
using Arch.SystemGroups;
using ECS.Abstract;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Updates the active scene counter
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class UpdateActiveSceneCounter : BaseUnityLoopSystem
    {

        private IScenesCache scenesCache;
        public UpdateActiveSceneCounter(World world, IScenesCache scenesCache) : base(world)
        {
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
        }
    }
}