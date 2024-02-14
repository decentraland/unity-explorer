using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Chat;
using DCL.Diagnostics;
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
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
        private readonly Dictionary<string, NametagView> nametagViews = new Dictionary<string, NametagView>();
        private SingleInstanceEntity playerCamera;
        private NametagView nametagView;

        public NametagPlacementSystem(World world, IObjectPool<NametagView> nametagViewPool, ChatEntryConfigurationSO chatEntryConfiguration) : base(world)
        {
            this.nametagViewPool = nametagViewPool;
            this.chatEntryConfiguration = chatEntryConfiguration;
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
        [None(typeof(PlayerComponent))]
        private void UpdateNametagPlacement([Data] in CameraComponent camera, ref AvatarShapeComponent avatarShape, ref CharacterTransform characterTransform, ref PartitionComponent partitionComponent)
        {
            if (partitionComponent.IsBehind)
            {
                if(nametagViews.TryGetValue(avatarShape.ID, out nametagView))
                {
                    nametagViewPool.Release(nametagView);
                    nametagViews.Remove(avatarShape.ID);
                }

                return;
            }

            if(nametagViews.TryGetValue(avatarShape.ID, out nametagView))
            {
                nametagView.transform.position = camera.Camera.WorldToScreenPoint(characterTransform.Transform.position + (Vector3.up * 2.2f));
            }
            else
            {
                nametagView = nametagViewPool.Get();
                nametagView.Username.color = chatEntryConfiguration.GetNameColor(avatarShape.Name);
                nametagView.Username.text = avatarShape.Name;
                nametagView.WalletId.text = avatarShape.ID;
                nametagViews.Add(avatarShape.ID, nametagView);
            }

        }
    }
}
