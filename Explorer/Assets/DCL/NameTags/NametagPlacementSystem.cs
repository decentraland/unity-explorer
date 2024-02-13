using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.CharacterCamera;
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
        private readonly IObjectPool<NametagView> nametagViewPool;
        private readonly Dictionary<string, NametagView> nametagViews = new Dictionary<string, NametagView>();
        private SingleInstanceEntity playerCamera;

        public NametagPlacementSystem(World world, IObjectPool<NametagView> nametagViewPool) : base(world)
        {
            this.nametagViewPool = nametagViewPool;
        }

        public override void Initialize()
        {
            playerCamera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            UpdateNametagPlacementQuery(World, playerCamera.GetCameraComponent(World));
        }

        [Query]
        [All(typeof(CharacterTransform))]
        private void UpdateNametagPlacement([Data] in CameraComponent camera, ref AvatarShapeComponent avatarShape, ref CharacterTransform characterTransform, ref PartitionComponent partitionComponent)
        {
            if (partitionComponent.IsBehind)
            {
                if(nametagViews.TryGetValue(avatarShape.ID, out var releasableNametagView))
                {
                    nametagViewPool.Release(releasableNametagView);
                    nametagViews.Remove(avatarShape.ID);
                }

                return;
            }

            if(nametagViews.TryGetValue(avatarShape.ID, out var nametagView))
            {
                nametagView.transform.position = camera.Camera.WorldToScreenPoint(characterTransform.Transform.position + Vector3.up * 2);
            }
            else
            {
                nametagViews.Add(avatarShape.ID, nametagViewPool.Get());
            }

        }
    }
}
