using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
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

        // When ghosts are enabled, nametags should appear immediately (alongside the ghost placeholder).
        // Otherwise, wait until the avatar has been fully instantiated before showing the nametag.
        private readonly bool includeGhosts;

        private SingleInstanceEntity playerCamera;

        public NametagPlacementSystem(
            World world,
            IObjectPool<NametagHolder> nametagHolderPool,
            NametagsData nametagsData
        ) : base(world)
        {
            this.nametagHolderPool = nametagHolderPool;
            this.nametagsData = nametagsData;
            includeGhosts = FeaturesRegistry.Instance.IsEnabled(FeatureId.AVATAR_GHOSTS);
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

            AddTagForPlayerAvatarsQuery(World, cameraComponent);
            AddTagForNonPlayerAvatarsQuery(World, cameraComponent);
            UpdateOwnTagQuery(World);
            UpdateElementTagQuery(World, cameraComponent, fovScaleFactor, cameraForward, cameraUp);
            ProcessChatBubbleComponentsQuery(World);
            UpdateNametagSpeakingStateQuery(World);
        }

        [Query]
        [None(typeof(NametagHolder), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void AddTagForPlayerAvatars([Data] in CameraComponent camera, Entity e, in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent, in Profile profile)
        {
            if (!includeGhosts && avatarShape.InstantiatedWearables.Count == 0)
                return;

            if (avatarShape.NameTagHiddenByModifierArea ||
                partitionComponent.IsBehind ||
                (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e)) ||
                NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, MAX_DISTANCE_SQR, MIN_DISTANCE_SQR))
                return;

            MarkVoiceChatBadgeDirty(e);
            NametagHolder nametagHolder = CreateNameTag(in avatarShape, profile);
            World.Add(e, nametagHolder);
        }

        [Query]
        [None(typeof(NametagHolder), typeof(Profile), typeof(DeleteEntityIntention))]
        [All(typeof(PBAvatarShape), typeof(AvatarBase))]
        private void AddTagForNonPlayerAvatars([Data] in CameraComponent camera, Entity e, in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (!includeGhosts && avatarShape.InstantiatedWearables.Count == 0)
                return;

            if (avatarShape.HiddenByModifierArea ||
                avatarShape.NameTagHiddenByModifierArea ||
                partitionComponent.IsBehind ||
                NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, MAX_DISTANCE_SQR, MIN_DISTANCE_SQR) ||
                string.IsNullOrEmpty(avatarShape.Name))
                return;

            MarkVoiceChatBadgeDirty(e);
            NametagHolder nametagHolder = CreateNameTag(in avatarShape);
            World.Add(e, nametagHolder);
        }

        // The pool resets transient visual state on Release, so a fresh holder always starts clean.
        // Re-dirty any existing voice chat badge so UpdateNametagSpeakingState re-applies the current state to the new holder,
        // otherwise IsDirty may already be false and the badge would stay off.
        private void MarkVoiceChatBadgeDirty(Entity e)
        {
            ref VoiceChatNametagComponent voiceChat = ref World.TryGetRef<VoiceChatNametagComponent>(e, out bool exists);
            if (exists)
                voiceChat.IsDirty = true;
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

            if (voiceChatComponent.IsRemoving)
            {
                nametagHolder.Nametag.VoiceChat = nametagHolder.Nametag.Speaking = nametagHolder.Nametag.Hushed = false;
                World.Remove<VoiceChatNametagComponent>(e);
                return;
            }

            nametagHolder.Nametag.VoiceChat = voiceChatComponent.Type == VoiceChatType.NEARBY || voiceChatComponent.IsSpeaking;

            nametagHolder.Nametag.Speaking = voiceChatComponent.IsSpeaking;
            nametagHolder.Nametag.Hushed = voiceChatComponent.IsHushed; // hushed is cleared to false when changing room

            voiceChatComponent.IsDirty = false;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateElementTag([Data] in CameraComponent camera, [Data] in float fovScaleFactor, [Data] in float3 cameraForward, [Data] in float3 cameraUp, Entity e,
            NametagHolder nametagHolder, in AvatarBase avatarBase, in CharacterTransform characterTransform,
            in PartitionComponent partitionComponent, in AvatarShapeComponent avatarShape)
        {
            if (avatarShape.HiddenByModifierArea
                || avatarShape.NameTagHiddenByModifierArea
                || partitionComponent.IsBehind
                || NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, MAX_DISTANCE_SQR, MIN_DISTANCE_SQR)
                || (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e))
                || World.Has<HiddenPlayerComponent>(e))
            {
                nametagHolderPool.Release(nametagHolder);
                World.Remove<NametagHolder>(e);
                return;
            }

            Vector3 nametagPosition = avatarBase.GetAdaptiveNametagPosition();

            if (World.Has<GliderPropEnabled>(e))
                nametagPosition.y += avatarBase.NametagGlideOffset;

            UpdateTagPositionAndRotation(nametagHolder.transform, nametagPosition, cameraForward, cameraUp);
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

            Color usernameColor = profile?.UserNameColor ?? NameColorHelper.GetNameColor(avatarShape.Name);

            string walletId = profile?.WalletId ?? (avatarShape.ID.Length >= 4
                ? avatarShape.ID.AsSpan(avatarShape.ID.Length - 4).ToString()
                : NAMETAG_DEFAULT_WALLET_ID);

            bool isOfficial = !string.IsNullOrEmpty(profile?.UserId) && OfficialWalletsHelper.Instance.IsOfficialWallet(profile.UserId);

            nametagHolder.Nametag.SetData(avatarShape.Name, usernameColor, walletId, profile?.HasClaimedName ?? false, isOfficial);
        }
    }
}
