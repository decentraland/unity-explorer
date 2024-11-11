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
        private readonly IObjectPool<NametagView> nametagViewPool;

        public NameTagCleanUpSystem(World world, NametagsData nametagsData, IObjectPool<NametagView> nametagViewPool) : base(world)
        {
            this.nametagsData = nametagsData;
            this.nametagViewPool = nametagViewPool;
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
        private void RemoveTag(NametagView nametagView, in DeleteEntityIntention deleteEntityIntention)
        {
            if (deleteEntityIntention.DeferDeletion == false)
                nametagViewPool.Release(nametagView);
        }

        [Query]
        private void RemoveAllTags(Entity e, NametagView nametagView)
        {
            nametagViewPool.Release(nametagView);
            World.Remove<NametagView>(e);
        }
    }
}
