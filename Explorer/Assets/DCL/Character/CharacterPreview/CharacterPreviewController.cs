using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.CharacterPreview.Components;
using DCL.Optimization.Pools;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;

namespace DCL.CharacterPreview
{
    public readonly struct CharacterPreviewController : IDisposable
    {
        private readonly CharacterPreviewCameraController cameraController;
        private readonly CharacterPreviewAvatarContainer characterPreviewAvatarContainer;
        private readonly IComponentPool<CharacterPreviewAvatarContainer> characterPreviewContainerPool;
        private readonly Entity characterPreviewEntity;
        private readonly World globalWorld;

        public CharacterPreviewController(World world, CharacterPreviewAvatarContainer avatarContainer,
            CharacterPreviewInputEventBus inputEventBus, IComponentPool<CharacterPreviewAvatarContainer> characterPreviewContainerPool,
            CharacterPreviewCameraSettings cameraSettings, IComponentPool<Transform> transformPool)
        {
            globalWorld = world;
            characterPreviewAvatarContainer = avatarContainer;
            cameraController = new CharacterPreviewCameraController(inputEventBus, characterPreviewAvatarContainer, cameraSettings);
            this.characterPreviewContainerPool = characterPreviewContainerPool;

            var parent = transformPool.Get();
            parent.SetParent(avatarContainer.avatarParent, false);
            parent.gameObject.layer = avatarContainer.avatarParent.gameObject.layer;
            parent.name = "CharacterPreview";
            parent.ResetLocalTRS();

            characterPreviewEntity = world.Create(
                new CharacterTransform(parent),
                new AvatarShapeComponent("CharacterPreview", "CharacterPreview"),
                new CharacterPreviewComponent(),
                new CharacterEmoteComponent());
        }

        public void Dispose()
        {
            // World can be already destroyed but for some reason `IsAlive` returns true
            if (globalWorld.Capacity > 0)
            {
                ref AvatarShapeComponent avatarShape = ref globalWorld.Get<AvatarShapeComponent>(characterPreviewEntity);
                if (!avatarShape.WearablePromise.IsConsumed) avatarShape.WearablePromise.ForgetLoading(globalWorld);
                globalWorld.Add(characterPreviewEntity, new DeleteEntityIntention());
            }

            characterPreviewContainerPool.Release(characterPreviewAvatarContainer);
            cameraController.Dispose();
        }

        public UniTask UpdateAvatar(CharacterPreviewAvatarModel avatarModel, CancellationToken ct)
        {
            ref AvatarShapeComponent avatarShape = ref globalWorld.Get<AvatarShapeComponent>(characterPreviewEntity);

            avatarShape.SkinColor = avatarModel.SkinColor;
            avatarShape.HairColor = avatarModel.HairColor;
            avatarShape.EyesColor = avatarModel.EyesColor;
            avatarShape.BodyShape = BodyShape.FromStringSafe(avatarModel.BodyShape);

            avatarShape.WearablePromise.ForgetLoading(globalWorld);

            avatarShape.WearablePromise = AssetPromise<WearablesResolution, GetWearablesByPointersIntention>.Create(
                globalWorld,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(avatarShape.BodyShape,
                    avatarModel.Wearables ?? (IReadOnlyCollection<URN>)Array.Empty<URN>(), avatarModel.ForceRenderCategories),
                PartitionComponent.TOP_PRIORITY
            );

            avatarShape.EmotePromise = EmotePromise.Create(globalWorld,
                EmoteComponentsUtils.CreateGetEmotesByPointersIntention(avatarShape.BodyShape,
                    avatarModel.Emotes ?? (IReadOnlyCollection<URN>)Array.Empty<URN>()),
                PartitionComponent.TOP_PRIORITY);

            avatarShape.IsDirty = true;

            return WaitForAvatarInstantiatedAsync(ct);
        }

        private async UniTask WaitForAvatarInstantiatedAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && globalWorld.Get<AvatarShapeComponent>(characterPreviewEntity).IsDirty)
                await UniTask.Yield(ct);
        }

        public void PlayEmote(string emoteId)
        {
            globalWorld.Add(characterPreviewEntity, new CharacterEmoteIntent { EmoteId = emoteId });
        }
    }
}
