using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Chat;
using DCL.Diagnostics;
using DCL.Multiplayer.Profiles.Systems;
using DCL.Profiles;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Nametags
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MultiplayerProfilesSystem))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class NametagPlacementSystem : BaseUnityLoopSystem
    {
        private const float NAMETAG_HEIGHT_MULTIPLIER = 2.1f;
        private const float NAMETAG_SCALE_MULTIPLIER = 0.15f;

        private readonly IObjectPool<NametagView> nametagViewPool;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
        private readonly NametagsData nametagsData;
        private readonly ChatBubbleConfigurationSO chatBubbleConfigurationSo;

        private SingleInstanceEntity playerCamera;
        private float distanceFromCamera;

        public NametagPlacementSystem(
            World world,
            IObjectPool<NametagView> nametagViewPool,
            ChatEntryConfigurationSO chatEntryConfiguration,
            NametagsData nametagsData,
            ChatBubbleConfigurationSO chatBubbleConfigurationSo
        ) : base(world)
        {
            this.nametagViewPool = nametagViewPool;
            this.chatEntryConfiguration = chatEntryConfiguration;
            this.nametagsData = nametagsData;
            this.chatBubbleConfigurationSo = chatBubbleConfigurationSo;
        }

        public override void Initialize()
        {
            playerCamera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            if (!nametagsData.showNameTags)
            {
                RemoveAllTagsQuery(World);
                return;
            }

            RemoveTagQuery(World);

            CameraComponent camera = playerCamera.GetCameraComponent(World);

            UpdateTagQuery(World, camera);
            AddTagQuery(World, camera);
            ProcessChatBubbleComponentsQuery(World);
        }

        [Query]
        [None(typeof(NametagView))]
        private void AddTag([Data] in CameraComponent camera, Entity e, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in PartitionComponent partitionComponent, in Profile profile)
        {
            if (partitionComponent.IsBehind || IsOutOfRenderRange(camera, characterTransform) || (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e))) return;

            NametagView nametagView = nametagViewPool.Get();
            nametagView.Id = avatarShape.ID;
            nametagView.Username.color = chatEntryConfiguration.GetNameColor(avatarShape.Name);
            nametagView.InjectConfiguration(chatBubbleConfigurationSo);
            nametagView.SetUsername(avatarShape.Name, avatarShape.ID.Substring(avatarShape.ID.Length - 4), profile.HasClaimedName);
            nametagView.gameObject.name = avatarShape.ID;

            UpdateTagPosition(nametagView, camera.Camera, characterTransform.Position);

            World.Add(e, nametagView);
        }

        [Query]
        [All(typeof(ChatBubbleComponent))]
        private void ProcessChatBubbleComponents(Entity e, in ChatBubbleComponent chatBubbleComponent, in NametagView nametagView)
        {
            if (nametagsData.showChatBubbles)
                nametagView.SetChatMessage(chatBubbleComponent.ChatMessage);

            World.Remove<ChatBubbleComponent>(e);
        }

        [Query]
        private void RemoveTag(NametagView nametagView, in DeleteEntityIntention deleteEntityIntention)
        {
            if (deleteEntityIntention.DeferDeletion == false)
                nametagViewPool.Release(nametagView);
        }

        [Query]
        [All(typeof(NametagView))]
        private void RemoveAllTags(Entity e, NametagView nametagView)
        {
            nametagViewPool.Release(nametagView);
            World.Remove<NametagView>(e);
        }

        [Query]
        private void UpdateTag([Data] in CameraComponent camera, Entity e, NametagView nametagView, in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (partitionComponent.IsBehind || IsOutOfRenderRange(camera, characterTransform) || (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e)))
            {
                nametagViewPool.Release(nametagView);
                World.Remove<NametagView>(e);
                return;
            }

            UpdateTagPosition(nametagView, camera.Camera, characterTransform.Position);
            UpdateTagTransparencyAndScale(nametagView, camera.Camera, characterTransform.Position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTagPosition(NametagView view, Camera camera, Vector3 characterPosition)
        {
            view.transform.position = characterPosition + (Vector3.up * NAMETAG_HEIGHT_MULTIPLIER);
            view.transform.LookAt(view.transform.position + camera.transform.rotation * Vector3.forward, camera.transform.rotation * Vector3.up);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTagTransparencyAndScale(NametagView view, Camera camera, Vector3 characterPosition)
        {
            distanceFromCamera = Vector3.Distance(camera.transform.position, characterPosition);
            view.gameObject.transform.localScale
                = Vector3.one * (Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distanceFromCamera * NAMETAG_SCALE_MULTIPLIER);
            view.SetTransparency(distanceFromCamera, chatBubbleConfigurationSo.maxDistance);
        }

        private bool IsOutOfRenderRange(CameraComponent camera, CharacterTransform characterTransform) =>
            Vector3.Distance(camera.Camera.transform.position, characterTransform.Position) > chatBubbleConfigurationSo.maxDistance;
    }
}
