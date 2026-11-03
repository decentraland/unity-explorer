using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character;
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
        private const float DEFAULT_MAX_DISTANCE = 40;
        private const float MIN_DISTANCE = 2;
        private const float MIN_DISTANCE_SQR = MIN_DISTANCE * MIN_DISTANCE;
        private const int MAX_VISIBLE_TAGS = 100;

        private readonly IObjectPool<NametagHolder> nametagHolderPool;
        private readonly NametagsData nametagsData;

        // Per-frame scratch: refilled at the start of every Update.
        private readonly NearestTagsCap nearestTagsCap = new (MAX_VISIBLE_TAGS);

        // When ghosts are enabled, nametags should appear immediately (alongside the ghost placeholder).
        // Otherwise, wait until the avatar has been fully instantiated before showing the nametag.
        private readonly bool includeGhosts;

        private SingleInstanceEntity playerCamera;
        private SingleInstanceEntity playerEntity;

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
            playerEntity = World.CachePlayer();
        }

        protected override void Update(float t)
        {
            bool showNameTags = nametagsData.showNameTags;
            float maxDistance = DEFAULT_MAX_DISTANCE;

            CameraComponent cameraComponent = playerCamera.GetCameraComponent(World);
            float3 playerPosition = World.Get<CharacterTransform>(playerEntity).Position;

            float fovScaleFactor = NametagMathHelper.CalculateFovScaleFactor(cameraComponent.Camera.fieldOfView, NAMETAG_SCALE_MULTIPLIER);
            NametagMathHelper.CalculateCameraForward(cameraComponent.Camera.transform.rotation, out float3 cameraForward);
            NametagMathHelper.CalculateCameraUp(cameraComponent.Camera.transform.rotation, out float3 cameraUp);

            nearestTagsCap.Clear();

            // The nametags toggle disables the name and the scene plate alike.
            if (showNameTags)
            {
                // Must run before any tag is added or kept: it sets the cap distance both rely on.
                CollectNearestTagsQuery(World, cameraComponent, maxDistance, playerPosition);

                AddTagForPlayerAvatarsQuery(World, cameraComponent, maxDistance, playerPosition);
                AddTagForNonPlayerAvatarsQuery(World, cameraComponent, maxDistance, playerPosition);

                // Holders needed by a scene avatar tag while the name itself is hidden or missing.
                // SceneAvatarTagComponent is in the archetype, so these iterate nothing until a scene tags someone.
                AddSceneTagForPlayerAvatarsQuery(World, cameraComponent, maxDistance, playerPosition);
                AddSceneTagForNonPlayerAvatarsQuery(World, cameraComponent, maxDistance, playerPosition);
            }

            UpdateOwnTagQuery(World);
            UpdateElementTagQuery(World, cameraComponent, fovScaleFactor, cameraForward, cameraUp, showNameTags, maxDistance, playerPosition);
            UpdateSceneTaggedElementTagQuery(World, cameraComponent, fovScaleFactor, cameraForward, cameraUp, showNameTags, maxDistance, playerPosition);
            ProcessChatBubbleComponentsQuery(World);
            ProcessSceneAvatarTagsQuery(World);
            RemoveOrphanSceneAvatarTagsQuery(World);
            UpdateNametagSpeakingStateQuery(World);
        }

        [Query]
        [None(typeof(NametagHolder), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void AddTagForPlayerAvatars([Data] in CameraComponent camera, [Data] in float maxDistance, [Data] in float3 playerPosition,
            Entity e, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in PartitionComponent partitionComponent, in Profile profile)
        {
            if (avatarShape.NameTagHiddenByModifierArea
                || !CanAddTag(in camera, maxDistance, in playerPosition, e, in avatarShape, in characterTransform, in partitionComponent))
                return;

            MarkVoiceChatBadgeDirty(e);
            MarkSceneAvatarTagDirty(e);
            AddNameTag(e, in avatarShape, nameVisible: true, profile);
        }

        [Query]
        [None(typeof(NametagHolder), typeof(Profile), typeof(DeleteEntityIntention))]
        [All(typeof(PBAvatarShape), typeof(AvatarBase))]
        private void AddTagForNonPlayerAvatars([Data] in CameraComponent camera, [Data] in float maxDistance, [Data] in float3 playerPosition,
            Entity e, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (avatarShape.NameTagHiddenByModifierArea
                || string.IsNullOrEmpty(avatarShape.Name)
                || !CanAddTag(in camera, maxDistance, in playerPosition, e, in avatarShape, in characterTransform, in partitionComponent))
                return;

            MarkVoiceChatBadgeDirty(e);
            MarkSceneAvatarTagDirty(e);
            AddNameTag(e, in avatarShape, nameVisible: true);
        }

        [Query]
        [None(typeof(NametagHolder), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void AddSceneTagForPlayerAvatars([Data] in CameraComponent camera, [Data] in float maxDistance, [Data] in float3 playerPosition,
            Entity e, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in PartitionComponent partitionComponent, in Profile profile, ref SceneAvatarTagComponent sceneTag)
        {
            if (sceneTag.IsRemoving || !CanAddTag(in camera, maxDistance, in playerPosition, e, in avatarShape, in characterTransform, in partitionComponent))
                return;

            MarkVoiceChatBadgeDirty(e);
            sceneTag.IsDirty = true;
            AddNameTag(e, in avatarShape, nameVisible: !avatarShape.NameTagHiddenByModifierArea, profile);
        }

        [Query]
        [None(typeof(NametagHolder), typeof(Profile), typeof(DeleteEntityIntention))]
        [All(typeof(PBAvatarShape), typeof(AvatarBase))]
        private void AddSceneTagForNonPlayerAvatars([Data] in CameraComponent camera, [Data] in float maxDistance, [Data] in float3 playerPosition,
            Entity e, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in PartitionComponent partitionComponent, ref SceneAvatarTagComponent sceneTag)
        {
            if (sceneTag.IsRemoving || !CanAddTag(in camera, maxDistance, in playerPosition, e, in avatarShape, in characterTransform, in partitionComponent))
                return;

            MarkVoiceChatBadgeDirty(e);
            sceneTag.IsDirty = true;
            AddNameTag(e, in avatarShape, nameVisible: !avatarShape.NameTagHiddenByModifierArea && !string.IsNullOrEmpty(avatarShape.Name));
        }

        // Mirrors the add and keep conditions of the tag queries, so only tags that can actually be shown take a cap slot.
        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void CollectNearestTags([Data] in CameraComponent camera, [Data] in float maxDistance, [Data] in float3 playerPosition,
            Entity e, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (!IsTagWanted(e, in avatarShape) || !CanShowTag(in camera, maxDistance, e, in avatarShape, in characterTransform, in partitionComponent))
                return;

            nearestTagsCap.Add(math.distancesq(playerPosition, characterTransform.Position));
        }

        private bool IsTagWanted(Entity e, in AvatarShapeComponent avatarShape)
        {
            if (!avatarShape.NameTagHiddenByModifierArea && !string.IsNullOrEmpty(avatarShape.Name))
                return true;

            ref SceneAvatarTagComponent sceneTag = ref World.TryGetRef<SceneAvatarTagComponent>(e, out bool exists);
            return exists && !sceneTag.IsRemoving;
        }

        private bool CanAddTag(in CameraComponent camera, float maxDistance, in float3 playerPosition, Entity e, in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent) =>
            CanShowTag(in camera, maxDistance, e, in avatarShape, in characterTransform, in partitionComponent)
            && !IsBeyondTagsCap(in playerPosition, in characterTransform);

        private bool CanShowTag(in CameraComponent camera, float maxDistance, Entity e, in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent) =>
            (includeGhosts || avatarShape.InstantiatedWearables.Count > 0)
            && !ShouldCullTag(in camera, maxDistance, e, in avatarShape, in characterTransform, in partitionComponent);

        // Measured from the player, not the camera: a far virtual camera keeps the tags nearest to the player.
        private bool IsBeyondTagsCap(in float3 playerPosition, in CharacterTransform characterTransform) =>
            math.distancesq(playerPosition, characterTransform.Position) > nearestTagsCap.MaxSqrDistance;

        // The pool resets transient visual state on Release, so a fresh holder always starts clean.
        // Re-dirty any existing voice chat badge so UpdateNametagSpeakingState re-applies the current state to the new holder,
        // otherwise IsDirty may already be false and the badge would stay off.
        private void MarkVoiceChatBadgeDirty(Entity e)
        {
            ref VoiceChatNametagComponent voiceChat = ref World.TryGetRef<VoiceChatNametagComponent>(e, out bool exists);
            if (exists)
                voiceChat.IsDirty = true;
        }

        // Same rationale as MarkVoiceChatBadgeDirty: a re-acquired holder starts with the plate hidden.
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

            nametagHolder.Nametag.SetSceneAvatarTag(sceneTag.Text, sceneTag.TextColor, sceneTag.BackgroundColor, sceneTag.BorderColor);
            sceneTag.IsDirty = false;
        }

        // ProcessSceneAvatarTags only sees entities that have a holder, so a tag flagged for removal on a
        // culled avatar would linger forever.
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
        private void UpdateElementTag([Data] in CameraComponent camera, [Data] in float fovScaleFactor, [Data] in float3 cameraForward, [Data] in float3 cameraUp, [Data] in bool showNameTags,
            [Data] in float maxDistance, [Data] in float3 playerPosition, Entity e,
            NametagHolder nametagHolder, in AvatarBase avatarBase, in CharacterTransform characterTransform,
            in PartitionComponent partitionComponent, in AvatarShapeComponent avatarShape)
        {
            // Without a scene avatar tag the name is the only reason for this holder to exist.
            if (!showNameTags
                || avatarShape.NameTagHiddenByModifierArea
                || ShouldCullTag(in camera, maxDistance, e, in avatarShape, in characterTransform, in partitionComponent)
                || IsBeyondTagsCap(in playerPosition, in characterTransform))
            {
                ReleaseTag(e, nametagHolder);
                return;
            }

            UpdateTagTransform(nametagHolder, e, in avatarBase, in characterTransform, in camera, fovScaleFactor, cameraForward, cameraUp, maxDistance);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateSceneTaggedElementTag([Data] in CameraComponent camera, [Data] in float fovScaleFactor, [Data] in float3 cameraForward, [Data] in float3 cameraUp, [Data] in bool showNameTags,
            [Data] in float maxDistance, [Data] in float3 playerPosition, Entity e,
            NametagHolder nametagHolder, in AvatarBase avatarBase, in CharacterTransform characterTransform,
            in PartitionComponent partitionComponent, in AvatarShapeComponent avatarShape, in SceneAvatarTagComponent sceneTag)
        {
            // A scene avatar may carry no name at all; the plate must not uncover an empty name box.
            bool nameVisible = !avatarShape.NameTagHiddenByModifierArea && !string.IsNullOrEmpty(avatarShape.Name);

            // The nametags toggle takes the plate down with the name; a modifier area hides the name only.
            if (!showNameTags
                || (!nameVisible && sceneTag.IsRemoving)
                || ShouldCullTag(in camera, maxDistance, e, in avatarShape, in characterTransform, in partitionComponent)
                || IsBeyondTagsCap(in playerPosition, in characterTransform))
            {
                ReleaseTag(e, nametagHolder);
                return;
            }

            nametagHolder.Nametag.NameVisible = nameVisible;
            UpdateTagTransform(nametagHolder, e, in avatarBase, in characterTransform, in camera, fovScaleFactor, cameraForward, cameraUp, maxDistance);
        }

        private bool ShouldCullTag(in CameraComponent camera, float maxDistance, Entity e, in AvatarShapeComponent avatarShape,
            in CharacterTransform characterTransform, in PartitionComponent partitionComponent) =>
            avatarShape.HiddenByModifierArea
            || partitionComponent.IsBehind
            || NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, maxDistance * maxDistance, MIN_DISTANCE_SQR)
            || (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e))
            || World.Has<HiddenPlayerComponent>(e);

        private void ReleaseTag(Entity e, NametagHolder nametagHolder)
        {
            nametagHolderPool.Release(nametagHolder);
            World.Remove<NametagHolder>(e);
        }

        private void UpdateTagTransform(NametagHolder nametagHolder, Entity e, in AvatarBase avatarBase, in CharacterTransform characterTransform,
            in CameraComponent camera, float fovScaleFactor, float3 cameraForward, float3 cameraUp, float maxDistance)
        {
            Vector3 nametagPosition = avatarBase.GetAdaptiveNametagPosition();

            if (World.Has<GliderPropEnabled>(e))
                nametagPosition.y += avatarBase.NametagGlideOffset;

            UpdateTagPositionAndRotation(nametagHolder.transform, nametagPosition, cameraForward, cameraUp);
            UpdateTagTransparencyAndScale(nametagHolder, camera.Camera.transform.position, characterTransform.Position, fovScaleFactor, maxDistance);
        }

        private static void UpdateTagPositionAndRotation(Transform view, float3 newPosition, float3 cameraForward, float3 cameraUp)
        {
            view.position = newPosition;
            view.LookAt(newPosition + cameraForward, cameraUp);
        }

        private static void UpdateTagTransparencyAndScale(NametagHolder nametagHolder, float3 cameraPosition, float3 characterPosition, float fovScaleFactor, float maxDistance)
        {
            NametagElement nametag = nametagHolder.Nametag;

            if (!NametagMathHelper.HasDistanceChanged(cameraPosition, characterPosition, nametag.LastSqrDistance)
                && !NametagMathHelper.HasChanged(fovScaleFactor, nametag.LastFovScaleFactor)
                && !NametagMathHelper.HasChanged(maxDistance, nametag.LastMaxDistance))
                return;

            NametagMathHelper.CalculateDistance(cameraPosition, characterPosition, out float distance, out float sqrDistance);
            nametag.LastSqrDistance = sqrDistance;
            nametag.LastFovScaleFactor = fovScaleFactor;
            nametag.LastMaxDistance = maxDistance;

            NametagMathHelper.CalculateTagScale(distance, fovScaleFactor, out float3 scale);
            nametagHolder.gameObject.transform.localScale = scale;
            nametag.style.opacity = NametagMathHelper.CalculateOpacity(distance, maxDistance);
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

            string? userId = profile?.UserId;
            bool isOfficial = !string.IsNullOrEmpty(userId) && OfficialWalletsHelper.Instance.IsOfficialWallet(userId);

            nametagHolder.Nametag.SetData(avatarShape.Name, usernameColor, walletId, profile?.HasClaimedName ?? false, isOfficial);
        }
    }
}
