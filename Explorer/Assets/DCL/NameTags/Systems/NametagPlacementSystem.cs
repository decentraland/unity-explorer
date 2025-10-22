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
using DCL.FeatureFlags;
using DCL.Utilities;
using DCL.VoiceChat;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Unity.Mathematics;

namespace DCL.Nametags
{
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class NametagPlacementSystem : BaseUnityLoopSystem
    {
        private const float NAMETAG_SCALE_MULTIPLIER = 0.15f;

        private const string NAMETAG_DEFAULT_WALLET_ID = "0000";
        private const float MAX_DISTANCE = 40;
        private const float MIN_DISTANCE = 2;
        private const float MAX_DISTANCE_SQR = MAX_DISTANCE * MAX_DISTANCE;
        private const float MIN_DISTANCE_SQR = MIN_DISTANCE * MIN_DISTANCE;

        private readonly IObjectPool<NametagHolder> nametagHolderPool;
        private readonly NametagsData nametagsData;

        private SingleInstanceEntity playerCamera;

        public NametagPlacementSystem(
            World world,
            IObjectPool<NametagHolder> nametagHolderPool,
            NametagsData nametagsData
        ) : base(world)
        {
            this.nametagHolderPool = nametagHolderPool;
            this.nametagsData = nametagsData;
        }

        public override void Initialize()
        {
            playerCamera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            if (!nametagsData.showNameTags)
                return;

            CameraComponent cameraComponent = playerCamera.GetCameraComponent(World);

            float fovScaleFactor = NametagMathHelper.CalculateFovScaleFactor(cameraComponent.Camera.fieldOfView, NAMETAG_SCALE_MULTIPLIER);
            NametagMathHelper.CalculateCameraForward(cameraComponent.Camera.transform.rotation, out float3 cameraForward);
            NametagMathHelper.CalculateCameraUp(cameraComponent.Camera.transform.rotation, out float3 cameraUp);

            AddTagForPlayerAvatarsQuery(World, cameraComponent, fovScaleFactor, cameraForward, cameraUp);
            AddTagForNonPlayerAvatarsQuery(World, cameraComponent, fovScaleFactor, cameraForward, cameraUp);
            UpdateOwnTagQuery(World);
            UpdateElementTagQuery(World, cameraComponent, fovScaleFactor, cameraForward, cameraUp);
            ProcessChatBubbleComponentsQuery(World);
            UpdateNametagSpeakingStateQuery(World);
        }

        [Query]
        [None(typeof(NametagHolder), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void AddTagForPlayerAvatars([Data] in CameraComponent camera, [Data] in float fovScaleFactor, [Data] in float3 cameraForward, [Data] in float3 cameraUp, Entity e,
            in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent, in Profile profile)
        {
            if (partitionComponent.IsBehind ||
                (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e)) ||
                NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, MAX_DISTANCE_SQR, MIN_DISTANCE_SQR))
                return;

            NametagHolder nametagHolder = CreateNameTag(in avatarShape, profile);
            World.Add(e, nametagHolder);
        }

        [Query]
        [None(typeof(NametagHolder), typeof(Profile), typeof(DeleteEntityIntention))]
        [All(typeof(PBAvatarShape), typeof(AvatarBase))]
        private void AddTagForNonPlayerAvatars([Data] in CameraComponent camera, [Data] in float fovScaleFactor, [Data] in float3 cameraForward, [Data] in float3 cameraUp, Entity e,
            in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (avatarShape.HiddenByModifierArea ||
                partitionComponent.IsBehind ||
                NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, MAX_DISTANCE_SQR, MIN_DISTANCE_SQR) ||
                string.IsNullOrEmpty(avatarShape.Name))
                return;

            NametagHolder nametagHolder = CreateNameTag(in avatarShape);
            World.Add(e, nametagHolder);
        }

        [Query]
        [None(typeof(PBAvatarShape))]
        private void UpdateOwnTag(in AvatarShapeComponent avatarShape, in Profile profile, in NametagHolder nametagHolder) =>
            TryRefreshNametag(nametagHolder, in avatarShape, profile);

        [Query]
        [All(typeof(ChatBubbleComponent))]
        private void ProcessChatBubbleComponents(in NametagHolder nametagHolder, ref ChatBubbleComponent chatBubbleComponent)
        {
            if (!chatBubbleComponent.IsDirty)
                return;

            nametagHolder.Nametag.DisplayMessage(chatBubbleComponent.ChatMessage, chatBubbleComponent.IsMention, chatBubbleComponent.IsPrivateMessage, chatBubbleComponent.IsOwnMessage, chatBubbleComponent.RecipientValidatedName, chatBubbleComponent.RecipientWalletId, chatBubbleComponent.RecipientNameColor, chatBubbleComponent.IsCommunityMessage, chatBubbleComponent.CommunityName);

            chatBubbleComponent.IsDirty = false;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateNametagSpeakingState(Entity e, in NametagHolder nametagHolder, ref VoiceChatNametagComponent voiceChatComponent)
        {
            if (!voiceChatComponent.IsDirty)
                return;

            nametagHolder.Nametag.VoiceChat = voiceChatComponent.IsSpeaking;

            if (voiceChatComponent.IsRemoving)
            {
                World.Remove<VoiceChatNametagComponent>(e);
                return;
            }

            voiceChatComponent.IsDirty = false;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateElementTag([Data] in CameraComponent camera, [Data] in float fovScaleFactor, [Data] in float3 cameraForward, [Data] in float3 cameraUp, Entity e,
            NametagHolder nametagHolder, in AvatarBase avatarBase, in CharacterTransform characterTransform,
            in PartitionComponent partitionComponent, in AvatarShapeComponent avatarShape)
        {
            if (avatarShape.HiddenByModifierArea 
                || partitionComponent.IsBehind
                || NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, MAX_DISTANCE_SQR, MIN_DISTANCE_SQR)
                || (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e))
                || World.Has<HiddenPlayerComponent>(e))
            {
                nametagHolderPool.Release(nametagHolder);
                World.Remove<NametagHolder>(e);
                return;
            }

            UpdateTagPositionAndRotation(nametagHolder.transform, avatarBase.GetAdaptiveNametagPosition(), cameraForward, cameraUp);
            UpdateTagTransparencyAndScale(nametagHolder, camera.Camera.transform.position, characterTransform.Position, fovScaleFactor);
        }

        private static void UpdateTagPositionAndRotation(Transform view, float3 newPosition, float3 cameraForward, float3 cameraUp)
        {
            view.position = newPosition;
            view.LookAt(newPosition + cameraForward, cameraUp);
        }

        private void UpdateTagTransparencyAndScale(NametagHolder nametagHolder, float3 cameraPosition, float3 characterPosition, float fovScaleFactor)
        {
            if (!NametagMathHelper.HasDistanceChanged(cameraPosition, characterPosition, nametagHolder.Nametag.LastSqrDistance))
                return;

            NametagMathHelper.CalculateDistance(cameraPosition, characterPosition, out float distance, out float sqrDistance);
            nametagHolder.Nametag.LastSqrDistance = sqrDistance;
            NametagMathHelper.CalculateTagScale(distance, fovScaleFactor, out float3 scale);
            nametagHolder.gameObject.transform.localScale = scale;

            // TODO: Maybe optimize?
            float normalizedDistance = (distance - NametagViewConstants.DEFAULT_OPACITY_MAX_DISTANCE) / (MAX_DISTANCE - NametagViewConstants.DEFAULT_OPACITY_MAX_DISTANCE);
            float opacity = Mathf.Clamp01(1f - normalizedDistance);

            nametagHolder.Nametag.style.opacity = opacity;
        }

        private NametagHolder CreateNameTag(in AvatarShapeComponent avatarShape, Profile? profile = null)
        {
            NametagHolder nametagHolder = nametagHolderPool.Get();

            TryRefreshNametag(nametagHolder, in avatarShape, profile);

            return nametagHolder;
        }

        private void TryRefreshNametag(NametagHolder nametagHolder, in AvatarShapeComponent avatarShape, Profile? profile)
        {
            if (nametagHolder.Nametag.ProfileID == avatarShape.ID && nametagHolder.Nametag.ProfileVersion == profile?.Version)
                return;

            nametagHolder.name = avatarShape.ID;
            nametagHolder.Nametag.ProfileID = avatarShape.ID;
            nametagHolder.Nametag.ProfileVersion = profile?.Version ?? 0;

            Color usernameColor;

            if (profile != null)
                usernameColor = profile.UserNameColor != Color.white ? profile.UserNameColor : NameColorHelper.GetNameColor(profile.DisplayName);
            else
                usernameColor = NameColorHelper.GetNameColor(avatarShape.Name);

            string walletId = profile?.WalletId ?? (avatarShape.ID.Length >= 4
                ? avatarShape.ID.AsSpan(avatarShape.ID.Length - 4).ToString()
                : NAMETAG_DEFAULT_WALLET_ID);

            bool isOfficial = !string.IsNullOrEmpty(profile?.UserId) && OfficialWalletsHelper.Instance.IsOfficialWallet(profile.UserId);

            nametagHolder.Nametag.SetData(avatarShape.Name, usernameColor, walletId, profile?.HasClaimedName ?? false, isOfficial);
        }
    }
}
