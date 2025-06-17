using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterPreview.Components;
using DCL.Optimization.Pools;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using Global.AppArgs;
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
        private const string CHARACTER_PREVIEW_NAME = "CharacterPreview";

        private readonly CharacterPreviewCameraController cameraController;
        private readonly CharacterPreviewAvatarContainer characterPreviewAvatarContainer;
        private readonly IComponentPool<CharacterPreviewAvatarContainer> characterPreviewContainerPool;
        private readonly Entity characterPreviewEntity;
        private readonly World globalWorld;
        private readonly bool builderEmotesPreview;

        public CharacterPreviewController(World world, RectTransform renderImage, CharacterPreviewAvatarContainer avatarContainer,
            CharacterPreviewInputEventBus inputEventBus, IComponentPool<CharacterPreviewAvatarContainer> characterPreviewContainerPool,
            CharacterPreviewCameraSettings cameraSettings, IComponentPool<Transform> transformPool, IAppArgs appArgs)
        {
            globalWorld = world;
            characterPreviewAvatarContainer = avatarContainer;
            cameraController = new CharacterPreviewCameraController(inputEventBus, characterPreviewAvatarContainer, cameraSettings);
            builderEmotesPreview = appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS);
            this.characterPreviewContainerPool = characterPreviewContainerPool;

            Transform? parent = transformPool.Get();
            parent.SetParent(avatarContainer.avatarParent, false);
            parent.gameObject.layer = avatarContainer.avatarParent.gameObject.layer;
            parent.name = CHARACTER_PREVIEW_NAME;
            parent.ResetLocalTRS();

            characterPreviewEntity = world.Create(
                new CharacterTransform(parent),
                new AvatarShapeComponent(CHARACTER_PREVIEW_NAME, CHARACTER_PREVIEW_NAME),
                new CharacterPreviewComponent { Camera = avatarContainer.camera, RenderImageRect = renderImage, Settings = avatarContainer.headIKSettings},
                new HeadIKComponent(),
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

            StopEmotes();

            if (globalWorld.TryGet(characterPreviewEntity, out AvatarBase avatarBase) && avatarBase != null)
                avatarBase.HeadIKRig.weight = 0;

            characterPreviewContainerPool.Release(characterPreviewAvatarContainer);
            cameraController.Dispose();
        }

        public UniTask UpdateAvatarAsync(CharacterPreviewAvatarModel avatarModel, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

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

            Entity emotePromiseEntity = builderEmotesPreview ? Entity.Null
                : globalWorld.Create(EmotePromise.Create(globalWorld,
                EmoteComponentsUtils.CreateGetEmotesByPointersIntention(avatarShape.BodyShape,
                    avatarModel.Emotes ?? (IReadOnlyCollection<URN>)Array.Empty<URN>()),
                PartitionComponent.TOP_PRIORITY));

            avatarShape.IsDirty = true;

            return WaitForAvatarInstantiatedAsync(emotePromiseEntity, ct);
        }

        private async UniTask WaitForAvatarInstantiatedAsync(Entity emotePromiseEntity, CancellationToken ct)
        {
            World world = globalWorld;
            Entity avatarEntity = characterPreviewEntity;

            while (!IsAvatarLoaded() || !IsEmoteLoaded())
                await UniTask.Yield(ct);

            ct.ThrowIfCancellationRequested();

            if (world.TryGet(avatarEntity, out AvatarBase avatarBase) && avatarBase != null  && !avatarBase.RigBuilder.enabled)
            {
                avatarBase.RigBuilder.enabled = true;
                avatarBase.HeadIKRig.weight = 1f;
            }
            return;

            bool IsAvatarLoaded()
            {
                return !world.Get<AvatarShapeComponent>(avatarEntity).IsDirty;
            }

            bool IsEmoteLoaded() =>
                emotePromiseEntity == Entity.Null
                || !world.IsAlive(emotePromiseEntity)
                || world.Get<EmotePromise>(emotePromiseEntity).IsConsumed;
        }

        public void PlayEmote(string emoteId)
        {
            var intent = new CharacterEmoteIntent { EmoteId = emoteId, TriggerSource = TriggerSource.PREVIEW };

            if (globalWorld.Has<CharacterEmoteIntent>(characterPreviewEntity))
                globalWorld.Set(characterPreviewEntity, intent);
            else
                globalWorld.Add(characterPreviewEntity, intent);
        }

        public bool IsPlayingEmote() =>
            globalWorld.TryGet(characterPreviewEntity, out CharacterEmoteComponent emoteComponent) && emoteComponent.IsPlayingEmote;

        public bool IsPlayingEmote(out CharacterEmoteComponent emoteComponent) =>
            globalWorld.TryGet(characterPreviewEntity, out emoteComponent) && emoteComponent.IsPlayingEmote;

        public void StopEmotes()
        {
            ref CharacterEmoteComponent emoteComponent = ref globalWorld.Get<CharacterEmoteComponent>(characterPreviewEntity);
            emoteComponent.StopEmote = true;
        }

        public void SetPreviewPlatformActive(bool isActive) =>
            characterPreviewAvatarContainer.SetPreviewPlatformActive(isActive);

        public void SetCharacterPreviewAvatarContainerActive(bool isActive) =>
            characterPreviewAvatarContainer.gameObject.SetActive(isActive);
    }
}
