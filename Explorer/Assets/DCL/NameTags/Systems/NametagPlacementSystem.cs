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
using UnityEngine;
using UnityEngine.Pool;

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
        private float distanceFromCamera;

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
        }

        public override void Initialize()
        {
            playerCamera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            if (!nametagsData.showNameTags)
                return;

            EnableTagQuery(World);

            CameraComponent camera = playerCamera.GetCameraComponent(World);

            UpdateTagQuery(World, camera);
            AddTagForPlayerAvatarsQuery(World, camera);
            AddTagForNonPlayerAvatarsQuery(World, camera);
            ProcessChatBubbleComponentsQuery(World);
            UpdateOwnTagQuery(World, camera);
            RemoveUnusedChatBubbleComponentsQuery(World);
        }

        [Query]
        [None(typeof(NametagView), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void AddTagForPlayerAvatars([Data] in CameraComponent camera, Entity e, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in PartitionComponent partitionComponent, in Profile profile)
        {
            if (partitionComponent.IsBehind || IsOutOfRenderRange(camera, characterTransform) || (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e))) return;

            NametagView nametagView = CreateNameTagView(in avatarShape, profile.HasClaimedName, true, profile);
            UpdateTagPosition(nametagView, camera.Camera, characterTransform.Position);

            World.Add(e, nametagView);
        }

        [Query]
        [None(typeof(NametagView), typeof(Profile), typeof(DeleteEntityIntention))]
        [All(typeof(PBAvatarShape))]
        private void AddTagForNonPlayerAvatars([Data] in CameraComponent camera, Entity e, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (partitionComponent.IsBehind || IsOutOfRenderRange(camera, characterTransform) || string.IsNullOrEmpty(avatarShape.Name)) return;

            NametagView nametagView = CreateNameTagView(in avatarShape, true, false);
            UpdateTagPosition(nametagView, camera.Camera, characterTransform.Position);

            World.Add(e, nametagView);
        }

        [Query]
        [All(typeof(AvatarBase), typeof(NametagView))]
        [None(typeof(DeleteEntityIntention))]
        private void EnableTag(in NametagView nametagView)
        {
            if (nametagView.isActiveAndEnabled)
                return;

            nametagView.gameObject.SetActive(true);
        }

        [Query]
        [None(typeof(PBAvatarShape))]
        private void UpdateOwnTag([Data] in CameraComponent camera, in AvatarShapeComponent avatarShape, in CharacterTransform characterTransform, in Profile profile, in NametagView nametagView)
        {
            if (nametagView.Id == avatarShape.ID)
                return;

            nametagView.Id = avatarShape.ID;
            nametagView.Username.color = profile.UserNameColor;
            nametagView.SetUsername(profile.ValidatedName, profile.WalletId, profile.HasClaimedName, true);
            nametagView.gameObject.name = avatarShape.ID;
            UpdateTagTransparencyAndScale(nametagView, camera.Camera, characterTransform.Position);

            UpdateTagPosition(nametagView, camera.Camera, characterTransform.Position);
        }

        [Query]
        [All(typeof(ChatBubbleComponent))]
        private void ProcessChatBubbleComponents(Entity e, in ChatBubbleComponent chatBubbleComponent, in NametagView nametagView)
        {
            if (nametagsData.showChatBubbles)
                nametagView.SetChatMessage(chatBubbleComponent.ChatMessage, chatBubbleComponent.IsMention);

            World.Remove<ChatBubbleComponent>(e);
        }

        [Query]
        [All(typeof(ChatBubbleComponent))]
        //This query is used to remove the ChatBubbleComponent from the entity if the chat bubble has not been displayed
        private void RemoveUnusedChatBubbleComponents(Entity e)
        {
            World.Remove<ChatBubbleComponent>(e);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateTag([Data] in CameraComponent camera, Entity e, NametagView nametagView, in AvatarCustomSkinningComponent avatarSkinningComponent, in CharacterTransform characterTransform, in PartitionComponent partitionComponent)
        {
            if (partitionComponent.IsBehind || IsOutOfRenderRange(camera, characterTransform) || (camera.Mode == CameraMode.FirstPerson && World.Has<PlayerComponent>(e)))
            {
                nametagViewPool.Release(nametagView);
                World.Remove<NametagView>(e);
                return;
            }

            // To test bounds:
//#if UNITY_EDITOR
//            Bounds avatarBounds = avatarSkinningComponent.LocalBounds;
//            avatarBounds.center += characterTransform.Position;
//            avatarBounds.DrawInEditor(Color.red);
//#endif

            UpdateTagPosition(nametagView, camera.Camera, characterTransform.Position + new Vector3(0.0f, Mathf.Min(avatarSkinningComponent.LocalBounds.max.y, NAMETAG_MAX_HEIGHT), 0.0f));
            UpdateTagTransparencyAndScale(nametagView, camera.Camera, characterTransform.Position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTagPosition(NametagView view, Camera camera, Vector3 newPosition)
        {
            view.transform.position = newPosition;
            view.transform.LookAt(view.transform.position + (camera.transform.rotation * Vector3.forward), camera.transform.rotation * Vector3.up);
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

        private NametagView CreateNameTagView(in AvatarShapeComponent avatarShape, bool hasClaimedName, bool useVerifiedIcon, Profile? profile = null)
        {
            NametagView nametagView = nametagViewPool.Get();
            nametagView.gameObject.name = avatarShape.ID;
            nametagView.Id = avatarShape.ID;

            if (profile != null)
                nametagView.Username.color = profile.UserNameColor != Color.white? profile.UserNameColor : ProfileNameColorHelper.GetNameColor(avatarShape.Name);
            else
                nametagView.Username.color = ProfileNameColorHelper.GetNameColor(avatarShape.Name);

            nametagView.InjectConfiguration(chatBubbleConfigurationSo);

            int walletIdLastDigitsIndex = avatarShape.ID.Length - 4;
            string walletId = walletIdLastDigitsIndex >= 0 ? avatarShape.ID.Substring(walletIdLastDigitsIndex) : NAMETAG_DEFAULT_WALLET_ID;
            nametagView.SetUsername(avatarShape.Name, walletId, hasClaimedName, useVerifiedIcon);

            return nametagView;
        }
    }
}
