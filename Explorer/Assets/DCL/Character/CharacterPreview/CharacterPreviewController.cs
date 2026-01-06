using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Export;
using DCL.AvatarRendering.Loading.Assets;
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
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UniGLTF;
using UnityEngine;
using Utility;
using VRM;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
using Object = UnityEngine.Object;

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
                new AvatarShapeComponent(CHARACTER_PREVIEW_NAME, CHARACTER_PREVIEW_NAME) { IsPreview = true },
                new CharacterPreviewComponent { Camera = avatarContainer.camera, RenderImageRect = renderImage, Settings = avatarContainer.headIKSettings },
                new CharacterEmoteComponent(),
                new HeadIKComponent { IsEnabled = false });
        }

        public void EnableHeadIK()
        {
            ref HeadIKComponent headIK = ref globalWorld.TryGetRef<HeadIKComponent>(characterPreviewEntity, out bool exists);

            if (exists)
                headIK.IsEnabled = true;
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

            characterPreviewAvatarContainer.DeInitialize();
            characterPreviewContainerPool.Release(characterPreviewAvatarContainer);
            cameraController.Dispose();
        }

        // TODO: Use the event bus instead.
        public async UniTask ExportAvatarAsync(CharacterPreviewAvatarModel avatarModel)
        {
            if (avatarModel.Wearables == null)
                throw new Exception("Tried to export an empty avatar");

            var wearablesPromise = AssetPromise<WearablesResolution, GetWearablesByPointersIntention>.Create(
                globalWorld,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(
                    BodyShape.FromStringSafe(avatarModel.BodyShape),
                    avatarModel.Wearables, avatarModel.ForceRenderCategories),
                PartitionComponent.TOP_PRIORITY
            );

            StreamableLoadingResult<WearablesResolution> wearablesResult;

            while (!wearablesPromise.TryConsume(globalWorld, out wearablesResult))
                await UniTask.Yield();

            if (!wearablesResult.Succeeded)
                throw new Exception("Wearables promise failed");

            if (FacialFeaturesTexturesByBodyShape == null)
                throw new Exception($"{nameof(FacialFeaturesTexturesByBodyShape)} is null");

            var wearableCache = new AttachmentAssetsDontCache();
            var usedCategories = new HashSet<string>();

            var facialFeaturesTextures = new FacialFeaturesTextures(
                new Dictionary<string, Dictionary<int, Texture>>());

            var avatarShape = new AvatarShapeComponent("", "")
            {
                BodyShape = BodyShape.FromStringSafe(avatarModel.BodyShape)
            };

            FacialFeaturesTexturesByBodyShape[avatarShape.BodyShape]
               .CopyInto(ref facialFeaturesTextures);

            GameObject parentObject = new GameObject("ExportAvatar");
            Transform parent = parentObject.transform;
            parent.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            parent.SetLocalPositionAndRotation(
                new Vector3(exportAvatarCount++ * 2f, -512f, 0f),
                Quaternion.Euler(90f, 0f, 0f));

            var wearableInfos = new List<WearableExportInfo>();
            GameObject? bodyShape = null;

            foreach (var wearable in wearablesResult.Asset.Wearables)
            {
                GameObject? instance = wearable.AppendToAvatar(wearableCache,
                    usedCategories, ref facialFeaturesTextures,
                    ref avatarShape, parent);

                wearableInfos.Add(
                    AvatarExportUtilities.CreateWearableInfo(wearable));

                if (wearable.Type == WearableType.BodyShape)
                    bodyShape = instance;
            }

            WearableComponentsUtils.HideBodyShape(bodyShape,
                wearablesResult.Asset.HiddenCategories, usedCategories);

            wearablesPromise.LoadingIntention.Dispose();
            wearablesResult.Asset.Dispose();

            // At this point, we should have a tree of game objects parented to
            // the ExportAvatar game object and a dictionary of face textures
            // ready to be massaged into a format acceptable to UniVRM.
            // AvatarBase is not needed because bodyShape has the same
            // structure already.

            if (bodyShape == null)
                throw new Exception("Avatar has no body shape");

            var bodyRenderers = bodyShape.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Transform[] bodyBones = bodyRenderers[0].bones;
            Transform bodyRoot = bodyRenderers[0].rootBone;

            var mainTextures = new Dictionary<string, Texture>();
            var maskTextures = new Dictionary<string, Texture>();

            foreach (var item in facialFeaturesTextures.Value)
            {
                if (item.Value.TryGetValue(TextureArrayConstants.MAINTEX_ORIGINAL_TEXTURE, out var mainTex) && mainTex != null)
                    mainTextures[item.Key] = mainTex;

                if (item.Value.TryGetValue(TextureArrayConstants.MASK_ORIGINAL_TEXTURE_ID, out var maskTex) && maskTex != null)
                    maskTextures[item.Key] = maskTex;
            }

            using var materialConverter = new VrmMaterialConverter(
                avatarModel.SkinColor,
                avatarModel.HairColor,
                avatarModel.EyesColor,
                mainTextures,
                maskTextures);

            foreach (var renderer in bodyRenderers)
            {
                if (renderer.gameObject.activeSelf)
                    renderer.sharedMaterials = materialConverter
                       .ConvertMaterials(renderer.sharedMaterials, renderer.name);
                else
                    Object.Destroy(renderer.gameObject);
            }

            for (var i = 1; i < parent.childCount; i++)
            {
                Transform wearable = parent.GetChild(i);

                foreach (var renderer in wearable.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    foreach (Transform bone in renderer.bones)
                        if (bone != null)
                            Object.Destroy(bone.gameObject);

                    if (renderer.gameObject.activeSelf)
                    {
                        renderer.bones = bodyBones;
                        renderer.rootBone = bodyRoot;

                        renderer.sharedMaterials = materialConverter
                           .ConvertMaterials(renderer.sharedMaterials, renderer.name);
                    }
                    else
                        Object.Destroy(renderer.gameObject);
                }
            }

            // A frame must pass for Object.Destroy to fully take effect, else
            // the animator avatar creation will complain about bones we have
            // supposedly already destroyed.
            await UniTask.Yield();

            var humanBones = new Dictionary<HumanBodyBones, Transform>();
            ExportSkeletonBuilder.MapBonesRecursive(humanBones, bodyShape.transform);

            var avatar = AvatarExportUtilities.CreateHumanoidAvatar(
                parentObject, humanBones);

            if (avatar == null || !avatar.isValid)
                throw new Exception("Could not create human avatar");

            parentObject.AddComponent<VRMMeta>().Meta = AvatarExportUtilities
               .CreateVrmMetaObject("Anonymous", wearableInfos);

            var data = new ExportingGltfData();
            var settings = new GltfExportSettings();

            using (var exporter = new VRMExporter(data, settings,
                       materialExporter: new UrpGltfMaterialExporter()))
            {
                exporter.Prepare(parentObject);
                exporter.Export();
            }

            await File.WriteAllBytesAsync("/Users/ansis/Desktop/test.glb",
                data.ToGlbBytes());
        }

        // TODO: Ask Misha how to do this correctly.
        public static FacialFeaturesTextures[]? FacialFeaturesTexturesByBodyShape;

        private static int exportAvatarCount = 0;

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

            Entity emotePromiseEntity = builderEmotesPreview
                ? Entity.Null
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

            if (world.TryGet(avatarEntity, out AvatarBase avatarBase) && avatarBase != null && !avatarBase.RigBuilder.enabled)
            {
                avatarBase.RigBuilder.enabled = true;
                avatarBase.HeadIKRig.weight = 1f;
            }

            return;

            bool IsEmoteLoaded() =>
                emotePromiseEntity == Entity.Null
                || !world.IsAlive(emotePromiseEntity)
                || world.Get<EmotePromise>(emotePromiseEntity).IsConsumed;
        }

        public bool IsAvatarLoaded() =>
            !globalWorld.Get<AvatarShapeComponent>(characterPreviewEntity).IsDirty;

        public void PlayEmote(string emoteId)
        {
            var intent = new CharacterEmoteIntent { EmoteId = emoteId, TriggerSource = TriggerSource.PREVIEW };

            if (globalWorld.Has<CharacterEmoteIntent>(characterPreviewEntity))
                globalWorld.Set(characterPreviewEntity, intent);
            else
                globalWorld.Add(characterPreviewEntity, intent);
        }

        public void ResetEmote()
        {
            ref var emoteComponent = ref globalWorld.Get<CharacterEmoteComponent>(characterPreviewEntity);
            emoteComponent.Reset();
        }

        public bool IsPlayingEmote() =>
            globalWorld.TryGet(characterPreviewEntity, out CharacterEmoteComponent emoteComponent) && emoteComponent.IsPlayingEmote;

        public bool TryGetPlayingEmote(out CharacterEmoteComponent emoteComponent) =>
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

        public void ResetAvatarMovement() =>
            cameraController.ResetAvatarMovement();

        public void ResetZoom()
        {
            cameraController.ResetZoom();
        }
    }
}
