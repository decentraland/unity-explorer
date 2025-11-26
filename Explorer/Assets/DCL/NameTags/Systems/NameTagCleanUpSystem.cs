using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using UnityEngine.Pool;
using AvatarCleanUpSystem = DCL.AvatarRendering.AvatarShape.AvatarCleanUpSystem;

namespace DCL.Nametags
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [UpdateAfter(typeof(AvatarCleanUpSystem))] // Avatar CleanUp System decides whether to defer deletion, this system is dependent on it
    public partial class NameTagCleanUpSystem : BaseUnityLoopSystem
    {
        private readonly NametagsData nametagsData;
        private readonly IObjectPool<NametagHolder> nametagHolderPool;

        public NameTagCleanUpSystem(World world, NametagsData nametagsData, IObjectPool<NametagHolder> nametagHolderPool) : base(world)
        {
            this.nametagsData = nametagsData;
            this.nametagHolderPool = nametagHolderPool;
        }

        protected override void Update(float t)
        {
            if (!nametagsData.showNameTags)
            {
                RemoveAllTagsQuery(World);
                return;
            }

            RemoveTagQuery(World);
        }

        [Query]
        private void RemoveTag(NametagHolder nametagHolder, in DeleteEntityIntention deleteEntityIntention)
        {
            if (!deleteEntityIntention.DeferDeletion)
                nametagHolderPool.Release(nametagHolder);
        }

        [Query]
        private void RemoveAllTags(Entity e, NametagHolder nametagHolder)
        {
            nametagHolderPool.Release(nametagHolder);
            World.Remove<NametagHolder>(e);
        }
    }
}
