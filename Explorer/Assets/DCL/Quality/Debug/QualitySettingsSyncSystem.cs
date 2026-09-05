using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using System;
using System.Collections.Generic;

namespace DCL.Quality.Debug
{
    /// <summary>
    ///     Synchronizes the quality settings between the runtime and the debug system
    /// </summary>
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class QualitySettingsSyncSystem : BaseUnityLoopSystem
    {
        private readonly IReadOnlyList<Action> onUpdate;

        internal QualitySettingsSyncSystem(World world, IReadOnlyList<Action> onUpdate) : base(world)
        {
            this.onUpdate = onUpdate;
        }

        protected override void Update(float t)
        {
            for (var i = 0; i < onUpdate.Count; i++)
                onUpdate[i]();
        }
    }
}
