using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Systems;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using UnityEngine.Pool;

namespace DCL.Nametags
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [UpdateAfter(typeof(AvatarCleanUpSystem))] // Avatar CleanUp System decides whether to defer deletion, this system is dependent on it
    public partial class NameTagCleanUpSystem : BaseUnityLoopSystem
    {
        private readonly NametagsData nametagsData;
        private readonly IObjectPool<NametagElement> nametagElementPool;

        public NameTagCleanUpSystem(World world, NametagsData nametagsData, IObjectPool<NametagElement> nametagElementPool) : base(world)
        {
            this.nametagsData = nametagsData;
            this.nametagElementPool = nametagElementPool;
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
        private void RemoveTag(NametagElement nametagView, in DeleteEntityIntention deleteEntityIntention)
        {
            if (!deleteEntityIntention.DeferDeletion)
                nametagElementPool.Release(nametagView);
        }

        [Query]
        private void RemoveAllTags(Entity e, NametagElement nametagView)
        {
            nametagElementPool.Release(nametagView);
            World.Remove<NametagElement>(e);
        }
    }
}
