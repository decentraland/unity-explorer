using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Chat;
using DCL.Diagnostics;
using DCL.Profiles;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.ECSComponents;
using DCL.Profiles.Helpers;
using DCL.VoiceChat;
using System;
using UnityEngine;
using UnityEngine.Pool;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace DCL.Nametags
{
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class NametagPlacementSystem : BaseUnityLoopSystem
    {
        private const string NAMETAG_DEFAULT_WALLET_ID = "0000";
        private const float MAX_DISTANCE = 40;
        private const float MAX_DISTANCE_SQR = MAX_DISTANCE * MAX_DISTANCE;

        private readonly IObjectPool<NametagElement> nametagElementPool;
        private readonly NametagsData nametagsData;
        private readonly VisualElement nametagRoot;

        private SingleInstanceEntity playerCamera;
        private CameraComponent cameraComponent;
        private bool cameraInitialized;

        private float timer;

        public NametagPlacementSystem(
            World world,
            IObjectPool<NametagElement> nametagElementPool,
            NametagsData nametagsData,
            VisualElement nametagRoot
        ) : base(world)
        {
            this.nametagElementPool = nametagElementPool;
            this.nametagsData = nametagsData;
            this.nametagRoot = nametagRoot;
        }

        public override void Initialize()
        {
            playerCamera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            if (!nametagsData.showNameTags)
                return;

            if (!cameraInitialized)
            {
                cameraComponent = playerCamera.GetCameraComponent(World);
                cameraInitialized = true;
            }

            UpdateElementTagQuery(World, cameraComponent);
            AddTagForPlayerAvatarsQuery(World, cameraComponent);
            AddTagForNonPlayerAvatarsQuery(World, cameraComponent);
            UpdateOwnTagQuery(World);
            ProcessChatBubbleComponentsQuery(World);
            UpdateNametagSpeakingStateQuery(World);

            timer += t;

            // Sort nametags with 1 iteration of bubble sort per frame. Back to basics :)
            int childCount = nametagRoot.hierarchy.childCount;

            for (int i = 0; i < childCount - 1; i++)
            {
                var left = (NametagElement)nametagRoot.hierarchy.ElementAt(i);
                var right = (NametagElement)nametagRoot.hierarchy.ElementAt(i + 1);

                if (left.LastSqrDistance < right.LastSqrDistance)
                    left.PlaceInFront(right);
            }
        }

        [Query]
        [None(typeof(NametagElement), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void AddTagForPlayerAvatars([Data] in CameraComponent camera, Entity e, in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent, in Profile profile)
        {
            if (partitionComponent.IsBehind ||
                (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e)) ||
                NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, MAX_DISTANCE_SQR))
                return;

            NametagElement nametagElement = CreateNameTagElement(in avatarShape, profile);
            UpdateTagPositionAndRotation(nametagElement, characterTransform.Position, camera.Camera);
            World.Add(e, nametagElement);
        }

        [Query]
        [None(typeof(NametagElement), typeof(Profile), typeof(DeleteEntityIntention))]
        [All(typeof(PBAvatarShape))]
        private void AddTagForNonPlayerAvatars([Data] in CameraComponent camera, Entity e, in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (avatarShape.HiddenByModifierArea ||
                partitionComponent.IsBehind ||
                NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, MAX_DISTANCE_SQR) ||
                string.IsNullOrEmpty(avatarShape.Name))
                return;

            NametagElement nametagElement = CreateNameTagElement(in avatarShape);
            UpdateTagPositionAndRotation(nametagElement, characterTransform.Position, camera.Camera);
            World.Add(e, nametagElement);
        }

        [Query]
        [None(typeof(PBAvatarShape))]
        private void UpdateOwnTag(in AvatarShapeComponent avatarShape, in Profile profile, in NametagElement nametagElement) =>
            TryRefreshNametagElement(nametagElement, in avatarShape, profile);

        [Query]
        [All(typeof(ChatBubbleComponent))]
        private void ProcessChatBubbleComponents(in NametagElement nametagElement, ref ChatBubbleComponent chatBubbleComponent)
        {
            if (!chatBubbleComponent.IsDirty)
                return;

            nametagElement.DisplayMessage(chatBubbleComponent.ChatMessage, chatBubbleComponent.IsMention, chatBubbleComponent.IsPrivateMessage, chatBubbleComponent.IsOwnMessage, chatBubbleComponent.RecipientValidatedName, chatBubbleComponent.RecipientWalletId, chatBubbleComponent.RecipientNameColor, chatBubbleComponent.IsCommunityMessage, chatBubbleComponent.CommunityName);

            chatBubbleComponent.IsDirty = false;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateNametagSpeakingState(Entity e, in NametagElement nametagElement, ref VoiceChatNametagComponent voiceChatComponent)
        {
            if (!voiceChatComponent.IsDirty)
                return;

            nametagElement.VoiceChat = voiceChatComponent.IsSpeaking;

            if (voiceChatComponent.IsRemoving)
            {
                World.Remove<VoiceChatNametagComponent>(e);
                return;
            }

            voiceChatComponent.IsDirty = false;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateElementTag([Data] in CameraComponent camera, Entity e, NametagElement nametagElement, in AvatarBase avatarBase, in CharacterTransform characterTransform,
            in PartitionComponent partitionComponent, in AvatarShapeComponent avatarShape)
        {
            if (avatarShape.HiddenByModifierArea ||
                partitionComponent.IsBehind
                || NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, MAX_DISTANCE_SQR)
                || (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e))
                || World.Has<BlockedPlayerComponent>(e))
            {
                nametagElementPool.Release(nametagElement);
                World.Remove<NametagElement>(e);
                return;
            }

            UpdateTagPositionAndRotation(nametagElement, avatarBase.GetAdaptiveNametagPosition(), camera.Camera);
            UpdateTagTransparency(nametagElement, camera.Camera.transform.position, characterTransform.Position);
        }

        private static void UpdateTagPositionAndRotation(NametagElement element, Vector3 newPosition, Camera camera)
        {
            var panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(element.panel, newPosition, camera);
            panelPosition.x -= element.resolvedStyle.width / 2f;
            panelPosition.y -= element.resolvedStyle.height;

            element.transform.position = panelPosition;
        }

        private void UpdateTagTransparency(NametagElement view, float3 cameraPosition, float3 characterPosition)
        {
            if (!NametagMathHelper.HasDistanceChanged(cameraPosition, characterPosition, view.LastSqrDistance))
                return;

            NametagMathHelper.CalculateDistance(cameraPosition, characterPosition, out float distance, out float sqrDistance);
            view.LastSqrDistance = sqrDistance;

            // TODO: Maybe optimize?
            float normalizedDistance = (distance - NametagViewConstants.DEFAULT_OPACITY_MAX_DISTANCE) / (MAX_DISTANCE - NametagViewConstants.DEFAULT_OPACITY_MAX_DISTANCE);
            float opacity = 1f - normalizedDistance;

            view.style.opacity = opacity;
        }

        private NametagElement CreateNameTagElement(in AvatarShapeComponent avatarShape, Profile? profile = null)
        {
            NametagElement nametagElement = nametagElementPool.Get();

            TryRefreshNametagElement(nametagElement, in avatarShape, profile);

            return nametagElement;
        }

        private void TryRefreshNametagElement(NametagElement nametagElement, in AvatarShapeComponent avatarShape, Profile? profile)
        {
            if (nametagElement.ProfileID == avatarShape.ID && nametagElement.ProfileVersion == profile?.Version)
                return;

            nametagElement.name = avatarShape.ID;
            nametagElement.ProfileID = avatarShape.ID;
            nametagElement.ProfileVersion = profile?.Version ?? 0;

            Color usernameColor;

            if (profile != null)
                usernameColor = profile.UserNameColor != Color.white ? profile.UserNameColor : ProfileNameColorHelper.GetNameColor(profile.DisplayName);
            else
                usernameColor = ProfileNameColorHelper.GetNameColor(avatarShape.Name);

            string walletId = profile?.WalletId ?? (avatarShape.ID.Length >= 4
                ? avatarShape.ID.AsSpan(avatarShape.ID.Length - 4).ToString()
                : NAMETAG_DEFAULT_WALLET_ID);

            nametagElement.SetData(avatarShape.Name, usernameColor, walletId, profile?.HasClaimedName ?? false, false);
        }
    }
}
