using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Shows the ghost renderer on AvatarBase while the avatar is loading. When wearables are resolved,
    ///     <see cref="AvatarInstantiatorSystem" /> reuses this AvatarBase and turns the ghost off.
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarLoaderSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarGhostSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<AvatarBase> avatarPoolRegistry;

        internal AvatarGhostSystem(World world, IComponentPool<AvatarBase> avatarPoolRegistry) : base(world)
        {
            this.avatarPoolRegistry = avatarPoolRegistry;
        }

        protected override void Update(float t)
        {
            EnsureGhostAvatarQuery(World);
        }

        [Query]
        [None(typeof(AvatarBase), typeof(DeleteEntityIntention))]
        private void EnsureGhostAvatar(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref CharacterTransform transformComponent)
        {
            AvatarBase avatarBase = avatarPoolRegistry.Get();
            avatarBase.gameObject.name = $"Avatar Ghost {avatarShapeComponent.ID}";

            Transform avatarTransform = avatarBase.transform;

            if (transformComponent.Transform != null)
            {
                avatarTransform.SetParent(transformComponent.Transform, false);

                using PoolExtensions.Scope<List<Transform>> children = avatarTransform.gameObject.GetComponentsInChildrenIntoPooledList<Transform>(true);

                for (var index = 0; index < children.Value.Count; index++)
                {
                    Transform child = children.Value[index];

                    if (child != null)
                        child.gameObject.layer = transformComponent.Transform.gameObject.layer;
                }
            }

            avatarTransform.ResetLocalTRS();

            if (avatarBase.GhostRenderer != null)
                avatarBase.GhostRenderer.SetActive(true);

            avatarBase.gameObject.SetActive(true);

            World.Add(entity, avatarBase, (IAvatarView)avatarBase, new AvatarGhostComponent(avatarBase.gameObject));
        }
    }
}
