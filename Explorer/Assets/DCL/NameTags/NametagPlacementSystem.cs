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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Nametags
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class NametagPlacementSystem : BaseUnityLoopSystem
    {
        private const float NAMETAG_HEIGHT_MULTIPLIER = 2.1f;

        private readonly IObjectPool<NametagView> nametagViewPool;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
        private readonly NametagsData nametagsData;
        private readonly ChatBubbleConfigurationSO chatBubbleConfigurationSo;
        private SingleInstanceEntity playerCamera;
        private float distanceFromCamera;
        private Dictionary<string, NametagView> activeNametags = new ();

        public NametagPlacementSystem(
            World world,
            IObjectPool<NametagView> nametagViewPool,
            ChatEntryConfigurationSO chatEntryConfiguration,
            NametagsData nametagsData,
            ChatBubbleConfigurationSO chatBubbleConfigurationSo) : base(world)
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
        [None(typeof(NametagView), typeof(PlayerComponent))]
        private void AddTag([Data] in CameraComponent camera, Entity e, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (partitionComponent.IsBehind || IsOutOfRenderRange(camera, characterTransform)) return;

            NametagView nametagView = nametagViewPool.Get();
            activeNametags.Add(avatarShape.ID, nametagView);
            nametagView.Id = avatarShape.ID;
            nametagView.Username.color = chatEntryConfiguration.GetNameColor(avatarShape.Name);
            nametagView.InjectConfiguration(chatBubbleConfigurationSo);
            nametagView.SetUsername($"{avatarShape.Name}<color=#76717E>#{avatarShape.ID.Substring(0, 4)}</color>");
            nametagView.gameObject.name = avatarShape.ID;

            UpdateTagPosition(nametagView, camera.Camera, characterTransform.Position);

            World.Add(e, nametagView);
        }

        [Query]
        [All(typeof(ChatBubbleComponent))]
        private void ProcessChatBubbleComponents(Entity e, in ChatBubbleComponent chatBubbleComponent)
        {
            if (nametagsData.showChatBubbles)
                if(activeNametags.TryGetValue(chatBubbleComponent.SenderId, out NametagView nametagView))
                    nametagView.SetChatMessage(chatBubbleComponent.ChatMessage);

            World.Remove<ChatBubbleComponent>(e);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void RemoveTag(NametagView nametagView)
        {
            activeNametags.Remove(nametagView.Id);
            nametagViewPool.Release(nametagView);
        }

        [Query]
        [All(typeof(NametagView))]
        private void RemoveAllTags(Entity e, NametagView nametagView)
        {
            nametagViewPool.Release(nametagView);
            activeNametags.Remove(nametagView.Id);
            World.Remove<NametagView>(e);
        }

        [Query]
        private void UpdateTag([Data] in CameraComponent camera, Entity e, NametagView nametagView, in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (partitionComponent.IsBehind || IsOutOfRenderRange(camera, characterTransform))
            {
                activeNametags.Remove(nametagView.Id);
                nametagViewPool.Release(nametagView);
                World.Remove<NametagView>(e);
                return;
            }

            UpdateTagPosition(nametagView, camera.Camera, characterTransform.Position);
            UpdateTagTransparency(nametagView, camera.Camera, characterTransform.Position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTagPosition(NametagView view, Camera camera, Vector3 characterPosition)
        {
            view.transform.position = characterPosition + (Vector3.up * NAMETAG_HEIGHT_MULTIPLIER);
            view.transform.LookAt(view.transform.position + camera.transform.rotation * Vector3.forward, camera.transform.rotation * Vector3.up);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTagTransparency(NametagView view, Camera camera, Vector3 characterPosition)
        {
            distanceFromCamera = Vector3.Distance(camera.transform.position, characterPosition);
            view.SetTransparency(distanceFromCamera, nametagsData.maxDistance);
        }

        private bool IsOutOfRenderRange(CameraComponent camera, CharacterTransform characterTransform) =>
            Vector3.Distance(camera.Camera.transform.position, characterTransform.Position) > nametagsData.maxDistance;
    }
}
