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
            includeGhosts = FeaturesRegistry.Instance.IsEnabled(FeatureId.AvatarGhosts);
        }

        public override void Initialize()
        {
            playerCamera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            bool showNameTags = nametagsData.showNameTags;

            CameraComponent cameraComponent = playerCamera.GetCameraComponent(World);

            float fovScaleFactor = NametagMathHelper.CalculateFovScaleFactor(cameraComponent.Camera.fieldOfView, NAMETAG_SCALE_MULTIPLIER);
            NametagMathHelper.CalculateCameraForward(cameraComponent.Camera.transform.rotation, out float3 cameraForward);
            NametagMathHelper.CalculateCameraUp(cameraComponent.Camera.transform.rotation, out float3 cameraUp);

            // Nothing can gain a name while they are globally disabled, so skip the avatar-wide queries entirely.
            if (showNameTags)
            {
                AddTagForPlayerAvatarsQuery(World, cameraComponent);
                AddTagForNonPlayerAvatarsQuery(World, cameraComponent);
            }

            // Holders needed by a scene avatar tag rather than by a name. SceneAvatarTagComponent is part of the
            // archetype, so these iterate nothing at all while no scene has tagged anyone.
            AddSceneTagForPlayerAvatarsQuery(World, cameraComponent, showNameTags);
            AddSceneTagForNonPlayerAvatarsQuery(World, cameraComponent, showNameTags);

            UpdateOwnTagQuery(World);
            UpdateElementTagQuery(World, cameraComponent, fovScaleFactor, cameraForward, cameraUp, showNameTags);
            UpdateSceneTaggedElementTagQuery(World, cameraComponent, fovScaleFactor, cameraForward, cameraUp, showNameTags);
            ProcessChatBubbleComponentsQuery(World);
            ProcessSceneAvatarTagsQuery(World);
            RemoveOrphanSceneAvatarTagsQuery(World);
            UpdateNametagSpeakingStateQuery(World);
        }

        [Query]
        [None(typeof(NametagHolder), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void AddTagForPlayerAvatars([Data] in CameraComponent camera, Entity e, in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent, in Profile profile)
        {
            if (avatarShape.NameTagHiddenByModifierArea
                || !CanAddTag(in camera, e, in avatarShape, in characterTransform, in partitionComponent))
                return;

            MarkVoiceChatBadgeDirty(e);
            MarkSceneAvatarTagDirty(e);
            AddNameTag(e, in avatarShape, nameVisible: true, profile);
        }

        [Query]
        [None(typeof(NametagHolder), typeof(Profile), typeof(DeleteEntityIntention))]
        [All(typeof(PBAvatarShape), typeof(AvatarBase))]
        private void AddTagForNonPlayerAvatars([Data] in CameraComponent camera, Entity e, in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (avatarShape.NameTagHiddenByModifierArea
                || string.IsNullOrEmpty(avatarShape.Name)
                || !CanAddTag(in camera, e, in avatarShape, in characterTransform, in partitionComponent))
                return;

            MarkVoiceChatBadgeDirty(e);
            MarkSceneAvatarTagDirty(e);
            AddNameTag(e, in avatarShape, nameVisible: true);
        }

        [Query]
        [None(typeof(NametagHolder), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void AddSceneTagForPlayerAvatars([Data] in CameraComponent camera, [Data] in bool showNameTags, Entity e, in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent, in Profile profile, ref SceneAvatarTagComponent sceneTag)
        {
            if (sceneTag.IsRemoving || !CanAddTag(in camera, e, in avatarShape, in characterTransform, in partitionComponent))
                return;

            MarkVoiceChatBadgeDirty(e);
            sceneTag.IsDirty = true;
            AddNameTag(e, in avatarShape, showNameTags && !avatarShape.NameTagHiddenByModifierArea, profile);
        }

        [Query]
        [None(typeof(NametagHolder), typeof(Profile), typeof(DeleteEntityIntention))]
        [All(typeof(PBAvatarShape), typeof(AvatarBase))]
        private void AddSceneTagForNonPlayerAvatars([Data] in CameraComponent camera, [Data] in bool showNameTags, Entity e, in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent, ref SceneAvatarTagComponent sceneTag)
        {
            if (sceneTag.IsRemoving || !CanAddTag(in camera, e, in avatarShape, in characterTransform, in partitionComponent))
                return;

            MarkVoiceChatBadgeDirty(e);
            sceneTag.IsDirty = true;
            AddNameTag(e, in avatarShape, showNameTags && !avatarShape.NameTagHiddenByModifierArea && !string.IsNullOrEmpty(avatarShape.Name));
        }

        private bool CanAddTag(in CameraComponent camera, Entity e, in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent) =>
            (includeGhosts || avatarShape.InstantiatedWearables.Count > 0)
            && !ShouldCullTag(in camera, e, in avatarShape, in characterTransform, in partitionComponent);

        // The pool resets transient visual state on Release, so a fresh holder always starts clean.
        // Re-dirty any existing voice chat badge so UpdateNametagSpeakingState re-applies the current state to the new holder,
        // otherwise IsDirty may already be false and the badge would stay off.
        private void MarkVoiceChatBadgeDirty(Entity e)
        {
            ref VoiceChatNametagComponent voiceChat = ref World.TryGetRef<VoiceChatNametagComponent>(e, out bool exists);
            if (exists)
                voiceChat.IsDirty = true;
        }

        // Same rationale as MarkVoiceChatBadgeDirty: a re-acquired holder starts with the plate hidden,
        // so ProcessSceneAvatarTags must re-apply the current tag to it.
        private void MarkSceneAvatarTagDirty(Entity e)
        {
            ref SceneAvatarTagComponent sceneTag = ref World.TryGetRef<SceneAvatarTagComponent>(e, out bool exists);
            if (exists)
                sceneTag.IsDirty = true;
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
        private void ProcessSceneAvatarTags(Entity e, in NametagHolder nametagHolder, ref SceneAvatarTagComponent sceneTag)
        {
            if (sceneTag.IsRemoving)
            {
                nametagHolder.Nametag.HideSceneAvatarTag();
                World.Remove<SceneAvatarTagComponent>(e);
                return;
            }

            if (!sceneTag.IsDirty)
                return;

            nametagHolder.Nametag.SetSceneAvatarTag(sceneTag.Text, sceneTag.TextColor, sceneTag.BackgroundColor);
            sceneTag.IsDirty = false;
        }

        // A tag flagged for removal on an entity that currently has no holder (culled by distance, behind camera)
        // would otherwise linger forever, as ProcessSceneAvatarTags only sees entities that do have one.
        [Query]
        [None(typeof(NametagHolder), typeof(DeleteEntityIntention))]
        private void RemoveOrphanSceneAvatarTags(Entity e, in SceneAvatarTagComponent sceneTag)
        {
            if (sceneTag.IsRemoving)
                World.Remove<SceneAvatarTagComponent>(e);
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

            nametagHolder.Nametag.VoiceChat = voiceChatComponent.Type == VoiceChatType.Nearby || voiceChatComponent.IsSpeaking;

            nametagHolder.Nametag.Speaking = voiceChatComponent.IsSpeaking;
            nametagHolder.Nametag.Hushed = voiceChatComponent.IsHushed; // hushed is cleared to false when changing room

            voiceChatComponent.IsDirty = false;
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(SceneAvatarTagComponent))]
        private void UpdateElementTag([Data] in CameraComponent camera, [Data] in float fovScaleFactor, [Data] in float3 cameraForward, [Data] in float3 cameraUp, [Data] in bool showNameTags, Entity e,
            NametagHolder nametagHolder, in AvatarBase avatarBase, in CharacterTransform characterTransform,
            in PartitionComponent partitionComponent, in AvatarShapeComponent avatarShape)
        {
            // Without a scene avatar tag the name is the only reason for this holder to exist.
            if (!showNameTags
                || avatarShape.NameTagHiddenByModifierArea
                || ShouldCullTag(in camera, e, in avatarShape, in characterTransform, in partitionComponent))
            {
                ReleaseTag(e, nametagHolder);
                return;
            }

            nametagHolder.Nametag.NameVisible = true;
            UpdateTagTransform(nametagHolder, e, in avatarBase, in characterTransform, in camera, fovScaleFactor, cameraForward, cameraUp);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateSceneTaggedElementTag([Data] in CameraComponent camera, [Data] in float fovScaleFactor, [Data] in float3 cameraForward, [Data] in float3 cameraUp, [Data] in bool showNameTags, Entity e,
            NametagHolder nametagHolder, in AvatarBase avatarBase, in CharacterTransform characterTransform,
            in PartitionComponent partitionComponent, in AvatarShapeComponent avatarShape, in SceneAvatarTagComponent sceneTag)
        {
            bool nameVisible = showNameTags && !avatarShape.NameTagHiddenByModifierArea;

            if ((!nameVisible && sceneTag.IsRemoving)
                || ShouldCullTag(in camera, e, in avatarShape, in characterTransform, in partitionComponent))
            {
                ReleaseTag(e, nametagHolder);
                return;
            }

            nametagHolder.Nametag.NameVisible = nameVisible;
            UpdateTagTransform(nametagHolder, e, in avatarBase, in characterTransform, in camera, fovScaleFactor, cameraForward, cameraUp);
        }

        private bool ShouldCullTag(in CameraComponent camera, Entity e, in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent) =>
            avatarShape.HiddenByModifierArea
            || partitionComponent.IsBehind
            || NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, MAX_DISTANCE_SQR, MIN_DISTANCE_SQR)
            || (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e))
            || World.Has<HiddenPlayerComponent>(e);

        private void ReleaseTag(Entity e, NametagHolder nametagHolder)
        {
            nametagHolderPool.Release(nametagHolder);
            World.Remove<NametagHolder>(e);
        }

        private void UpdateTagTransform(NametagHolder nametagHolder, Entity e, in AvatarBase avatarBase, in CharacterTransform characterTransform,
            in CameraComponent camera, float fovScaleFactor, float3 cameraForward, float3 cameraUp)
        {
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

        private void AddNameTag(Entity e, in AvatarShapeComponent avatarShape, bool nameVisible, Profile? profile = null)
        {
            NametagHolder nametagHolder = nametagHolderPool.Get();

            TryRefreshNametag(nametagHolder, in avatarShape, profile);
            nametagHolder.Nametag.NameVisible = nameVisible;

            World.Add(e, nametagHolder);
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
