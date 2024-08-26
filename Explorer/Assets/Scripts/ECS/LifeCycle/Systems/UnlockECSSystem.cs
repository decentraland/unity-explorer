﻿using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.Groups;
using Utility.Multithreading;

namespace ECS.LifeCycle.Systems
{
    /// <summary>
    ///     Unlocks ECS when the whole cycle of the player loop has processed
    /// </summary>
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [UpdateAfter(typeof(SyncedPreRenderingSystemGroup))] // After all other systems
    public partial class UnlockECSSystem : BaseUnityLoopSystem
    {
        private readonly MultithreadSync multithreadSync;

        internal UnlockECSSystem(World world, MultithreadSync multithreadSync) : base(world)
        {
            this.multithreadSync = multithreadSync;
        }

        protected override void Update(float t)
        {
            // We could skip the first frame of LockECSSystem
            if (multithreadSync.Acquired)
                multithreadSync.Release();
        }
    }
}
