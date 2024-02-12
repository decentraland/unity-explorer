using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Prioritization.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Nametags
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class NametagPlacementSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<NametagView> nametagPoolRegistry;
        private readonly Dictionary<string, NametagView> nametagViews = new Dictionary<string, NametagView>();

        public NametagPlacementSystem(World world, IComponentPool<NametagView> nametagPoolRegistry) : base(world)
        {
            this.nametagPoolRegistry = nametagPoolRegistry;
        }

        protected override void Update(float t)
        {
            UpdateNametagPlacementQuery(World);
        }

        [Query]
        [All(typeof(CharacterTransform))]
        private void UpdateNametagPlacement(ref AvatarShapeComponent avatarShape, ref CharacterTransform characterTransform, ref PartitionComponent partitionComponent)
        {
            if (partitionComponent.IsBehind)
            {
                if(nametagViews.TryGetValue(avatarShape.ID, out var releasableNametagView))
                {
                    nametagPoolRegistry.Release(releasableNametagView);
                    nametagViews.Remove(avatarShape.ID);
                }

                return;
            }

            if(nametagViews.TryGetValue(avatarShape.ID, out var nametagView))
            {
                nametagView.transform.position = characterTransform.Transform.position; //Camera.WorldToScreenPoint(characterTransform.Transform.position + Vector3.up * 2);
            }
            else
            {
                nametagViews.Add(avatarShape.ID, nametagPoolRegistry.Get());
            }

        }
    }
}
