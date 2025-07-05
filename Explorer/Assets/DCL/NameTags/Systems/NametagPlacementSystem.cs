using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Systems;
using DCL.Chat;
using DCL.Diagnostics;
using DCL.Profiles;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.ECSComponents;
using DCL.Profiles.Helpers;
using System;
using UnityEngine;
using UnityEngine.Pool;
using Unity.Mathematics;

// #if UNITY_EDITOR
// using Utility.Editor;
// #endif

namespace DCL.Nametags
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ChangeCharacterPositionGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class NametagPlacementSystem : BaseUnityLoopSystem
    {
        private const float NAMETAG_SCALE_MULTIPLIER = 0.15f;
        private const string NAMETAG_DEFAULT_WALLET_ID = "0000";
        private const float NAMETAG_MAX_HEIGHT = 4f;

        private readonly IObjectPool<NametagView> nametagViewPool;
        private readonly NametagsData nametagsData;
        private readonly ChatBubbleConfigurationSO chatBubbleConfigurationSo;

        private SingleInstanceEntity playerCamera;
        private float maxDistance;
        private float maxDistanceSqr;
        private CameraComponent cameraComponent;
        private bool cameraInitialized;

        public NametagPlacementSystem(
            World world,
            IObjectPool<NametagView> nametagViewPool,
            NametagsData nametagsData,
            ChatBubbleConfigurationSO chatBubbleConfigurationSo
        ) : base(world)
        {
            this.nametagViewPool = nametagViewPool;
            this.nametagsData = nametagsData;
            this.chatBubbleConfigurationSo = chatBubbleConfigurationSo;
            this.maxDistance = chatBubbleConfigurationSo.maxDistance;
            this.maxDistanceSqr = maxDistance * maxDistance;
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

            float fovScaleFactor = NametagMathHelper.CalculateFovScaleFactor(cameraComponent.Camera.fieldOfView, NAMETAG_SCALE_MULTIPLIER);
            NametagMathHelper.CalculateCameraForward(cameraComponent.Camera.transform.rotation, out float3 cameraForward);
            NametagMathHelper.CalculateCameraUp(cameraComponent.Camera.transform.rotation, out float3 cameraUp);

            EnableTagQuery(World);
            UpdateTagQuery(World, cameraComponent, fovScaleFactor, cameraForward, cameraUp);
            AddTagForPlayerAvatarsQuery(World, cameraComponent, cameraForward, cameraUp);
            AddTagForNonPlayerAvatarsQuery(World, cameraComponent, cameraForward, cameraUp);
            UpdateOwnTagQuery(World, cameraComponent, fovScaleFactor, cameraForward, cameraUp);
            ProcessChatBubbleComponentsQuery(World);
        }

        [Query]
        [None(typeof(NametagView), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void AddTagForPlayerAvatars([Data] in CameraComponent camera, [Data] in float3 cameraForward, [Data] in float3 cameraUp, Entity e, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in PartitionComponent partitionComponent, in Profile profile)
        {
            if (partitionComponent.IsBehind ||
                (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e)) ||
                NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, maxDistanceSqr))
                return;

            NametagView nametagView = CreateNameTagView(in avatarShape, profile.HasClaimedName, profile.HasClaimedName, profile);
            UpdateTagPosition(nametagView, characterTransform.Position, cameraForward, cameraUp);
            World.Add(e, nametagView);
        }

        [Query]
        [None(typeof(NametagView), typeof(Profile), typeof(DeleteEntityIntention))]
        [All(typeof(PBAvatarShape))]
        private void AddTagForNonPlayerAvatars([Data] in CameraComponent camera, [Data] in float3 cameraForward, [Data] in float3 cameraUp, Entity e, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (avatarShape.HiddenByModifierArea ||
                partitionComponent.IsBehind ||
                NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, maxDistanceSqr) ||
                string.IsNullOrEmpty(avatarShape.Name))
                return;

            var nametagView = CreateNameTagView(in avatarShape, true, false);
            UpdateTagPosition(nametagView, characterTransform.Position, cameraForward, cameraUp);
            World.Add(e, nametagView);
        }

        [Query]
        [All(typeof(AvatarBase), typeof(NametagView))]
        [None(typeof(DeleteEntityIntention))]
        private void EnableTag(in NametagView nametagView)
        {
            if (!nametagView.isActiveAndEnabled)
                nametagView.gameObject.SetActive(true);
        }

        [Query]
        [None(typeof(PBAvatarShape))]
        private void UpdateOwnTag([Data] in CameraComponent camera, [Data] in float fovScaleFactor, [Data] in float3 cameraForward, [Data] in float3 cameraUp, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in Profile profile, in NametagView nametagView)
        {
            if (nametagView.Id == avatarShape.ID
                && nametagView.ProfileVersion == profile.Version)
                return;

            nametagView.ProfileVersion = profile.Version;

            // If version is different, it might be because some part of the profile was updated, but not necessarily the name, so we also check
            // if the name is different in this case, otherwise we dont re-setup the own tag.
            if (nametagView.IsSameName(profile.ValidatedName, profile.HasClaimedName)) return;

            nametagView.Id = avatarShape.ID;
            nametagView.SetUsername(profile.ValidatedName, profile.WalletId, profile.HasClaimedName, profile.HasClaimedName, profile.UserNameColor);
            nametagView.gameObject.name = avatarShape.ID;

            UpdateTagTransparencyAndScale(nametagView, camera.Camera.transform.position, characterTransform.Position, fovScaleFactor);
            UpdateTagPosition(nametagView, characterTransform.Position, cameraForward, cameraUp);
        }

        [Query]
        [All(typeof(ChatBubbleComponent))]
        private void ProcessChatBubbleComponents(Entity e, in NametagView nametagView, ref ChatBubbleComponent chatBubbleComponent)
        {
            if (!chatBubbleComponent.IsDirty)
                return;

            nametagView.SetChatMessage(chatBubbleComponent.ChatMessage, chatBubbleComponent.IsMention, chatBubbleComponent.IsPrivateMessage, chatBubbleComponent.IsOwnMessage, chatBubbleComponent.RecipientValidatedName, chatBubbleComponent.RecipientWalletId, chatBubbleComponent.RecipientNameColor, chatBubbleComponent.IsCommunityMessage, chatBubbleComponent.CommunityName);

            chatBubbleComponent.IsDirty = false;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateTag([Data] in CameraComponent camera, [Data] in float fovScaleFactor, [Data] in float3 cameraForward, [Data] in float3 cameraUp, Entity e, NametagView nametagView, in AvatarCustomSkinningComponent avatarSkinningComponent, in CharacterTransform characterTransform, in PartitionComponent partitionComponent, in AvatarShapeComponent avatarShape)
        {
            if (avatarShape.HiddenByModifierArea ||
                partitionComponent.IsBehind
                || NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, maxDistanceSqr)
                || (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e))
                || World.Has<BlockedPlayerComponent>(e))
            {
                nametagViewPool.Release(nametagView);
                World.Remove<NametagView>(e);
                return;
            }
// To view and test bounds:
//#if UNITY_EDITOR
//            Bounds avatarBounds = avatarSkinningComponent.LocalBounds;
//            avatarBounds.center += characterTransform.Position;
//            avatarBounds.DrawInEditor(Color.red);
//#endif

            NametagMathHelper.CalculateTagPosition(characterTransform.Position, NAMETAG_MAX_HEIGHT, avatarSkinningComponent.LocalBounds.max.y, out float3 position);

            UpdateTagPosition(nametagView, position, cameraForward, cameraUp);
            UpdateTagTransparencyAndScale(nametagView, camera.Camera.transform.position, characterTransform.Position, fovScaleFactor);
        }

        private void UpdateTagPosition(NametagView view, float3 newPosition, float3 cameraForward, float3 cameraUp)
        {
            var transform = view.transform;
            transform.position = newPosition;
            transform.LookAt(newPosition + cameraForward, cameraUp);
        }

        private void UpdateTagTransparencyAndScale(NametagView view, float3 cameraPosition, float3 characterPosition, float fovScaleFactor)
        {
            if (!NametagMathHelper.HasDistanceChanged(cameraPosition, characterPosition, view.LastSqrDistance))
                return;

            NametagMathHelper.CalculateDistance(cameraPosition, characterPosition, out float distance, out float sqrDistance);
            view.LastSqrDistance = sqrDistance;
            NametagMathHelper.CalculateTagScale(distance, fovScaleFactor, out float3 scale);
            view.gameObject.transform.localScale = scale;
            view.SetTransparency(distance, maxDistance);
        }

        private NametagView CreateNameTagView(in AvatarShapeComponent avatarShape, bool hasClaimedName, bool useVerifiedIcon, Profile? profile = null)
        {
            var nametagView = nametagViewPool.Get();
            nametagView.gameObject.name = avatarShape.ID;
            nametagView.Id = avatarShape.ID;

            Color usernameColor;
            if (profile != null)
                usernameColor = profile.UserNameColor != Color.white ?
                    profile.UserNameColor :
                    ProfileNameColorHelper.GetNameColor(profile.DisplayName);
            else
                usernameColor = ProfileNameColorHelper.GetNameColor(avatarShape.Name);

            var walletId = profile?.WalletId ?? (avatarShape.ID.Length >= 4
                ? avatarShape.ID.AsSpan(avatarShape.ID.Length - 4).ToString()
                : NAMETAG_DEFAULT_WALLET_ID);

            nametagView.InjectConfiguration(chatBubbleConfigurationSo);
            nametagView.SetUsername(avatarShape.Name, walletId, hasClaimedName, useVerifiedIcon, usernameColor);

            return nametagView;
        }
    }
}
