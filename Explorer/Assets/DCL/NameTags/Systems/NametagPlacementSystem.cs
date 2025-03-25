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
using System.Runtime.CompilerServices;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.ECSComponents;
using DCL.UI.Profiles.Helpers;
using ECS.Unity.Transforms.Components;
using System;
using UnityEngine;
using UnityEngine.Pool;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;

// #if UNITY_EDITOR
// using Utility.Editor;
// #endif

namespace DCL.Nametags
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ChangeCharacterPositionGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    [BurstCompile]
    public partial class NametagPlacementSystem : BaseUnityLoopSystem
    {
        private const float NAMETAG_SCALE_MULTIPLIER = 0.15f;
        private const string NAMETAG_DEFAULT_WALLET_ID = "0000";
        private const float NAMETAG_MAX_HEIGHT = 4f;
        private const float FOV_HALF_RAD = 0.5f * Mathf.Deg2Rad;

        // Cache vectors to avoid allocations
        private static readonly Vector3 Forward = Vector3.forward;
        private static readonly Vector3 Up = Vector3.up;
        private static readonly Vector3 HeightOffset = new(0f, NAMETAG_MAX_HEIGHT, 0f);

        private readonly IObjectPool<NametagView> nametagViewPool;
        private readonly NametagsData nametagsData;
        private readonly ChatBubbleConfigurationSO chatBubbleConfigurationSo;

        private SingleInstanceEntity playerCamera;
        private float distanceFromCamera;
        private float maxDistance;
        private float maxDistanceSqr;

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

            var camera = playerCamera.GetCameraComponent(World);

            float fovScaleFactor = Mathf.Tan(camera.Camera.fieldOfView * FOV_HALF_RAD) * NAMETAG_SCALE_MULTIPLIER;
            var cameraForward = camera.Camera.transform.rotation * Forward;
            var cameraUp = camera.Camera.transform.rotation * Up;

            EnableTagQuery(World);
            UpdateTagQuery(World, camera, fovScaleFactor, cameraForward, cameraUp);
            AddTagForPlayerAvatarsQuery(World, camera, cameraForward, cameraUp);
            AddTagForNonPlayerAvatarsQuery(World, camera, cameraForward, cameraUp);
            ProcessChatBubbleComponentsQuery(World);
            UpdateOwnTagQuery(World, camera, fovScaleFactor, cameraForward, cameraUp);
        }

        [Query]
        [None(typeof(NametagView), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void AddTagForPlayerAvatars([Data] in CameraComponent camera, [Data] in Vector3 cameraForward, [Data] in Vector3 cameraUp, Entity e, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in PartitionComponent partitionComponent, in Profile profile)
        {
            if (partitionComponent.IsBehind ||
                (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e)) ||
                IsOutOfRenderRange(camera, characterTransform))
                return;

            var nametagView = CreateNameTagView(in avatarShape, profile.HasClaimedName, true, profile);
            UpdateTagPosition(nametagView, camera.Camera, characterTransform.Position, cameraForward, cameraUp);
            World.Add(e, nametagView);
        }

        [Query]
        [None(typeof(NametagView), typeof(Profile), typeof(DeleteEntityIntention))]
        [All(typeof(PBAvatarShape))]
        private void AddTagForNonPlayerAvatars([Data] in CameraComponent camera, [Data] in Vector3 cameraForward, [Data] in Vector3 cameraUp, Entity e, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (partitionComponent.IsBehind ||
                IsOutOfRenderRange(camera, characterTransform) ||
                string.IsNullOrEmpty(avatarShape.Name))
                return;

            var nametagView = CreateNameTagView(in avatarShape, true, false);
            UpdateTagPosition(nametagView, camera.Camera, characterTransform.Position, cameraForward, cameraUp);
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
        private void UpdateOwnTag([Data] in CameraComponent camera, [Data] in float fovScaleFactor, [Data] in Vector3 cameraForward, [Data] in Vector3 cameraUp, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in Profile profile, in NametagView nametagView)
        {
            if (nametagView.Id == avatarShape.ID)
                return;

            nametagView.Id = avatarShape.ID;
            nametagView.SetUsername(profile.ValidatedName, profile.WalletId, profile.HasClaimedName, true, profile.UserNameColor);
            nametagView.gameObject.name = avatarShape.ID;

            UpdateTagTransparencyAndScale(nametagView, camera.Camera, characterTransform.Position, fovScaleFactor);
            UpdateTagPosition(nametagView, camera.Camera, characterTransform.Position, cameraForward, cameraUp);
        }

        [Query]
        [All(typeof(ChatBubbleComponent))]
        private void ProcessChatBubbleComponents(Entity e, in NametagView nametagView, ref ChatBubbleComponent chatBubbleComponent)
        {
            if (!chatBubbleComponent.IsDirty)
                return;

            if (nametagsData.showChatBubbles)
                nametagView.SetChatMessage(chatBubbleComponent.ChatMessage, chatBubbleComponent.IsMention);

            chatBubbleComponent.IsDirty = false;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateTag([Data] in CameraComponent camera, [Data] in float fovScaleFactor, [Data] in Vector3 cameraForward, [Data] in Vector3 cameraUp, Entity e, NametagView nametagView, in AvatarCustomSkinningComponent avatarSkinningComponent, in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (partitionComponent.IsBehind ||
                IsOutOfRenderRange(camera, characterTransform) ||
                (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e)))
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

            var position = characterTransform.Position;
            position.y += Mathf.Min(avatarSkinningComponent.LocalBounds.max.y, NAMETAG_MAX_HEIGHT);

            UpdateTagPosition(nametagView, camera.Camera, position, cameraForward, cameraUp);
            UpdateTagTransparencyAndScale(nametagView, camera.Camera, characterTransform.Position, fovScaleFactor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        private void UpdateTagPosition(NametagView view, Camera camera, Vector3 newPosition, Vector3 cameraForward, Vector3 cameraUp)
        {
            view.transform.position = newPosition;
            view.transform.LookAt(view.transform.position + cameraForward, cameraUp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        private void UpdateTagTransparencyAndScale(NametagView view, Camera camera, Vector3 characterPosition, float fovScaleFactor)
        {
            distanceFromCamera = Vector3.Distance(camera.transform.position, characterPosition);
            view.gameObject.transform.localScale = Vector3.one * (fovScaleFactor * distanceFromCamera);
            view.SetTransparency(distanceFromCamera, maxDistance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        private bool IsOutOfRenderRange(CameraComponent camera, CharacterTransform characterTransform)
        {
            float sqrDistance = (camera.Camera.transform.position - characterTransform.Position).sqrMagnitude;
            return sqrDistance > maxDistanceSqr;
        }

        private NametagView CreateNameTagView(in AvatarShapeComponent avatarShape, bool hasClaimedName, bool useVerifiedIcon, Profile? profile = null)
        {
            var nametagView = nametagViewPool.Get();
            nametagView.gameObject.name = avatarShape.ID;
            nametagView.Id = avatarShape.ID;

            var usernameColor = profile?.UserNameColor != Color.white
                ? profile.UserNameColor
                : ProfileNameColorHelper.GetNameColor(profile?.DisplayName ?? avatarShape.Name);

            var walletId = profile?.WalletId ?? (avatarShape.ID.Length >= 4
                ? avatarShape.ID.AsSpan(avatarShape.ID.Length - 4).ToString()
                : NAMETAG_DEFAULT_WALLET_ID);

            nametagView.InjectConfiguration(chatBubbleConfigurationSo);
            nametagView.SetUsername(avatarShape.Name, walletId, hasClaimedName, useVerifiedIcon, usernameColor);

            return nametagView;
        }
    }
}
