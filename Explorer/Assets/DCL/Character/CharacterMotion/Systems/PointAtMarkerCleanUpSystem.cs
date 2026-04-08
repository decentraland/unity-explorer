using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Assets;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using UnityEngine.Pool;

namespace DCL.Character.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class PointAtMarkerCleanUpSystem : BaseUnityLoopSystem
    {
        private readonly IObjectPool<PointAtMarkerHolder> markerPool;

        internal PointAtMarkerCleanUpSystem(
            World world,
            IObjectPool<PointAtMarkerHolder> markerPool
        ) : base(world)
        {
            this.markerPool = markerPool;
        }

        protected override void Update(float t) =>
            CleanUpQuery(World);

        [Query]
        private void CleanUp(PointAtMarkerHolder marker, in DeleteEntityIntention del)
        {
            if (!del.DeferDeletion)
                markerPool.Release(marker);
        }
    }
}
