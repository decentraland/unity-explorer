using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Time.Components;
using ECS.Abstract;

namespace DCL.Time.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class UpdateTimeSystem : BaseUnityLoopSystem
    {
        public UpdateTimeSystem(World world) : base(world)
        {
            World.Create<TimeComponent>();
        }

        protected override void Update(float t)
        {
            UpdateTickQuery(World, UnityEngine.Time.time);
        }

        [Query]
        private void UpdateTick(
            [Data] float time,
            ref TimeComponent tickComponent)
        {
            tickComponent.Time = time;
        }
    }
}
