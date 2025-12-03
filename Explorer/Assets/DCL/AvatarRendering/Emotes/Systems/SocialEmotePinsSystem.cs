using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Nametags;
using DCL.Web3.Identities;
using ECS.Abstract;
using ECS.Prioritization.Components;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SocialEmotes.UI
{
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [UpdateAfter(typeof(NametagPlacementSystem))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class SocialEmotePinsSystem : BaseUnityLoopSystem
    {
        // TODO: Put this in a settings file
        private const float MAX_DISTANCE = 40;
        private const float MAX_DISTANCE_SQR = MAX_DISTANCE * MAX_DISTANCE;

        private readonly IObjectPool<SocialEmotePin> pinsPool;
        private readonly IWeb3IdentityCache identityCache;
        private SingleInstanceEntity playerCamera;
        private CameraComponent mainCameraComponent;
        private bool isCameraSet;

        public SocialEmotePinsSystem(World world, IObjectPool<SocialEmotePin> pinsPool, IWeb3IdentityCache identityCache) : base(world)
        {
            this.pinsPool = pinsPool;
            this.identityCache = identityCache;
        }

        public override void Initialize()
        {
            base.Initialize();
            playerCamera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            if (!isCameraSet)
            {
                mainCameraComponent = playerCamera.GetCameraComponent(World);
                isCameraSet = true;
            }

            if(identityCache.Identity == null)
                return;

            CreatePinQuery(World, identityCache.Identity.Address.OriginalFormat, mainCameraComponent);
            UpdatePinPositionQuery(World);
            AddNametagHeightToPinPositionQuery(World);
            MakePinFaceCameraQuery(World, mainCameraComponent);
            RemovePinQuery(World, mainCameraComponent);
        }

        [Query]
        [None(typeof(SocialEmotePin), typeof(PlayerComponent))]
        private void CreatePin([Data] string playerWalletAddress, [Data] CameraComponent camera, in Entity entity, in CharacterEmoteComponent emoteComponent,
            in CharacterTransform characterTransform, in AvatarShapeComponent avatarShape, in PartitionComponent partitionComponent)
        {
            if (avatarShape.HiddenByModifierArea ||
                partitionComponent.IsBehind ||
                NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, MAX_DISTANCE_SQR, 0.0f))
                return;

            if(emoteComponent.IsPlayingEmote &&
               emoteComponent.Metadata != null &&
               emoteComponent.Metadata.IsSocialEmote &&
               !emoteComponent.IsPlayingSocialEmoteOutcome &&
               emoteComponent.TargetAvatarWalletAddress == playerWalletAddress)
                World.Add(entity, pinsPool.Get());
        }

        [Query]
        private void UpdatePinPosition(in SocialEmotePin emotePin, AvatarBase avatarBase)
        {
            emotePin.transform.position = avatarBase.GetAdaptiveNametagPosition();
        }

        [Query]
        private void AddNametagHeightToPinPosition(in SocialEmotePin emotePin, NametagHolder nametag)
        {
            emotePin.transform.position += new Vector3(0.0f, nametag.Nametag.worldBound.height * nametag.transform.lossyScale.y, 0.0f);
        }

        [Query]
        private void MakePinFaceCamera([Data] CameraComponent camera, in SocialEmotePin emotePin)
        {
            emotePin.transform.LookAt(emotePin.transform.position + camera.Camera.transform.forward, camera.Camera.transform.up);
        }

        [Query]
        private void RemovePin([Data] CameraComponent camera, in Entity entity, in SocialEmotePin pin, ref CharacterEmoteComponent emoteComponent,
            in CharacterTransform characterTransform, in AvatarShapeComponent avatarShape, in PartitionComponent partitionComponent)
        {
            if (avatarShape.HiddenByModifierArea ||
                partitionComponent.IsBehind ||
                NametagMathHelper.IsOutOfRenderRange(camera.Camera.transform.position, characterTransform.Position, MAX_DISTANCE_SQR, 0.0f) ||
                !emoteComponent.IsPlayingEmote ||
                emoteComponent.IsPlayingSocialEmoteOutcome)
            {
                pinsPool.Release(pin);
                World.Remove<SocialEmotePin>(entity);
            }
        }
    }
}
