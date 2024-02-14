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
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using System.Runtime.CompilerServices;
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
        private SingleInstanceEntity playerCamera;

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
            RemoveTagQuery(World);

            CameraComponent camera = playerCamera.GetCameraComponent(World);

            UpdateTagQuery(World, camera);
            AddTagQuery(World, camera);
        }

        [Query]
        [None(typeof(NametagView))]
        private void AddTag([Data] in CameraComponent camera, Entity e, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (partitionComponent.IsBehind) return;

            NametagView nametagView = nametagViewPool.Get();
            nametagView.Username.color = chatEntryConfiguration.GetNameColor(avatarShape.Name);
            nametagView.Username.text = avatarShape.Name;
            nametagView.WalletId.text = avatarShape.ID;
            nametagView.gameObject.name = avatarShape.ID;

            UpdateTagPosition(nametagView, camera.Camera, characterTransform.Position);

            World.Add(e, nametagView);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void RemoveTag(NametagView nametagView)
        {
            nametagViewPool.Release(nametagView);
        }

        [Query]
        private void UpdateTag([Data] in CameraComponent camera, Entity e, NametagView nametagView, in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (partitionComponent.IsBehind)
            {
                nametagViewPool.Release(nametagView);
                World.Remove<NametagView>(e);
                return;
            }

            UpdateTagPosition(nametagView, camera.Camera, characterTransform.Position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTagPosition(NametagView view, Camera camera, Vector3 characterPosition)
        {
            view.transform.position = camera.WorldToScreenPoint(characterPosition + (Vector3.up * 2.2f));
        }
    }
}
