using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Loading;
using Services;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.VFX;
using Utils;

namespace Preview
{
    public class PreviewController : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;
        [SerializeField] private PreviewCameraController previewCameraController;

        [SerializeField] private DragRotator avatarRotator;
        [SerializeField] private DragRotator wearableRotator;
        [SerializeField] private PreviewUIPresenter previewUIPresenter;

        [SerializeField] private AvatarLoader avatarLoader;
        [SerializeField] private WearableLoader wearableLoader;
        [SerializeField] private Vector3 wearableOffset = new(-5, 0, 0);

        [SerializeField] private EmoteAnimationController emoteAnimationController;
        [SerializeField] private VisualEffect             confirmationVFX;

        [SerializeField] private GameObject animationReference;
        [SerializeField] private GameObject platform;

        [SerializeField] private float wearablePadding = 0.15f;

        // Remembers the view the user last switched to. In WebGL PlayerPrefs is a per-origin
        // IndexedDB store, so this is shared by every app served from the preview origin and
        // survives across sessions: only ever a fallback, never an override of what was requested.
        private const string PREF_AVATAR_SHOWN = "PreviewAvatarShown";

        // #cc9b76, the skin the JS wrapper used to send whenever a builder caller left it out.
        private static readonly Color DEFAULT_SKIN_COLOR = new(204f / 255f, 155f / 255f, 118f / 255f);

        private bool _loading;
        private bool _shouldReload;
        private bool _shouldCleanup;

        private void Start()
        {
            previewUIPresenter.ShowAvatarClicked += OnShowAvatarClicked;
            previewUIPresenter.ShowWearableClicked += OnShowWearableClicked;
            previewUIPresenter.EmoteToggleClicked += OnEmoteToggleClicked;
            previewUIPresenter.ContainerDrag += avatarRotator.OnDrag;
            previewUIPresenter.ContainerDrag += wearableRotator.OnDrag;
            emoteAnimationController.EmoteAnimationEnded += OnEmoteAnimationEnded;

            avatarRotator.AllowVertical = false;
            wearableRotator.AllowVertical = false;

            StartCoroutine(Reload());
        }

        private void OnEmoteAnimationEnded()
        {
            previewUIPresenter.SetAnimationPlaying(false);
        }

        private void OnEmoteToggleClicked(bool playing)
        {
            if (playing)
            {
                emoteAnimationController.ReplayEmote();
            }
            else
            {
                emoteAnimationController.StopEmote();
            }
        }

        private void OnShowWearableClicked()
        {
            PlayerPrefs.SetInt(PREF_AVATAR_SHOWN, 0);

            previewCameraController.ShowMarketplaceWearable(true);
            wearableRotator.ResetRotation();
        }

        private void OnShowAvatarClicked()
        {
            PlayerPrefs.SetInt(PREF_AVATAR_SHOWN, 1);

            previewCameraController.ShowMarketplaceWearable(false);
            avatarRotator.ResetRotation();
        }

        public void SetSpringBonesParams(SpringBones.SpringBonesParamsPayload payload) =>
            avatarLoader.SetSpringBonesParams(payload);

        public float GetEmoteLength() => emoteAnimationController.GetEmoteLength();

        public bool IsEmotePlaying() => emoteAnimationController.IsEmotePlaying();

        public void PlayEmote()
        {
            if (emoteAnimationController.IsPaused)
                emoteAnimationController.ResumeEmote();
            else
                emoteAnimationController.ReplayEmote();
        }

        public void PauseEmote() => emoteAnimationController.PauseEmote();

        public void GoToEmote(float seconds) => emoteAnimationController.GoToEmote(seconds);

        public void StopEmote() => emoteAnimationController.StopEmote();

        public void EnableSound() => emoteAnimationController.EnableSound();

        public void DisableSound() => emoteAnimationController.DisableSound();

        public bool HasSound() => emoteAnimationController.HasAudio;

        public void InvokeReload()
        {
            _shouldCleanup = false;
            StartCoroutine(Reload());
        }

        private async Awaitable Reload()
        {
            if (_loading)
            {
                _shouldReload = true;
                return;
            }

            Cleanup();
            previewUIPresenter.ShowLoader(true);
            _loading = true;
            mainCamera.cullingMask = 0; // Render nothing
            avatarLoader.enabled = false; // Disables Update for Outline
            wearableLoader.enabled = false; // Disables Update for Outline

            do
            {
                _shouldReload = false;

                // We store the instance in case it gets recreated by a call to AangConfiguration.RecreateFrom
                var config = AangConfiguration.Instance;

                avatarRotator.enabled = false;
                wearableRotator.enabled = false;
                avatarRotator.ResetRotation();
                wearableRotator.ResetRotation();

                animationReference.SetActive(config.ShowAnimationReference);
                platform.SetActive(config.Mode is PreviewMode.Authentication);
                mainCamera.backgroundColor = config.Background;
                mainCamera.orthographic = config.Projection == "orthographic";
                previewUIPresenter.EnableLoader(!config.DisableLoader);
                previewCameraController.SetMode(config.Mode);
                confirmationVFX.gameObject.SetActive(config.Mode is PreviewMode.Jesus);

                var hasEmoteOverride = false;
                var hasWearableOverride = false;
                var hasEmoteAudio = false;
                var showingAvatar = false;

                try
                {
                    await EntityService.PreloadBodyEntities();

                    switch (config.Mode)
                    {
                        case PreviewMode.Marketplace:
                            // With no urns there is nothing to override, so the profile avatar is the
                            // whole preview.
                            var urns = await LoadUrns(config);
                            var result = await LoadForMarketplace(config, urns);

                            previewUIPresenter.EnableEmoteControls(result.emoteOverride);

                            if (result.validRepresentation)
                            {
                                // When there is no single item to show on its own (an emote, or several
                                // urns at once) the avatar is the only view that exists.
                                showingAvatar = !result.showsItemAlone || ShouldShowAvatar(config);
                                previewUIPresenter.SetSwitcherState(
                                    showingAvatar
                                        ? PreviewUIPresenter.SwitcherState.Avatar
                                        : PreviewUIPresenter.SwitcherState.Wearable, result.avatarBodyShape);
                            }
                            else
                            {
                                previewUIPresenter.SetSwitcherState(PreviewUIPresenter.SwitcherState.WearableLocked,
                                    result.avatarBodyShape);
                            }

                            hasEmoteOverride = result.emoteOverride;
                            hasWearableOverride = result.showsItemAlone;
                            hasEmoteAudio = result.emoteOverrideAudio;
                            break;
                        case PreviewMode.Authentication:
                        case PreviewMode.Profile:
                            showingAvatar = true;
                            await LoadForProfile(config.Profile, config.Emote);
                            break;
                        case PreviewMode.Builder:
                            await LoadForBuilder(config.BodyShape,
                                config.EyeColor,
                                config.HairColor,
                                config.SkinColor,
                                config.Urns.ToArray(),
                                config.Emote,
                                config.Base64);
                            break;
                        case PreviewMode.Jesus:
                            showingAvatar = true;
                            confirmationVFX.Play();
                            await LoadForProfile(config.Profile, "character/Particles_Anim", true);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception e)
                {
                    JSBridge.NativeCalls.OnError(e.Message);
                    throw;
                }

                // Wait for 1 frame for animation to kick in before re-centering the object on screen
                await Awaitable.NextFrameAsync();

                if (hasWearableOverride)
                {
                    GameObjectUtils.CenterAndFit(wearableLoader.transform, mainCamera, wearablePadding);
                    wearableLoader.transform.position += wearableOffset;
                }
                else if (hasEmoteOverride)
                {
                    GameObjectUtils.CenterAndFit(avatarLoader.transform, mainCamera, wearablePadding);
                }

                if (config.Mode is PreviewMode.Marketplace)
                {
                    previewCameraController.ShowMarketplaceWearable(!showingAvatar);
                }
                else if (config.Mode is PreviewMode.Builder)
                {
                    avatarRotator.DragSpeed = 2f;
                }

                avatarRotator.enabled = true;
                wearableRotator.enabled = true;
                avatarRotator.EnableAutoRotate = config.Mode is PreviewMode.Marketplace && !hasEmoteOverride;

                previewUIPresenter.EnableEmoteControls(hasEmoteOverride);
                previewUIPresenter.EnableZoom(config.Mode is PreviewMode.Marketplace or PreviewMode.Builder);
                previewUIPresenter.EnableSwitcher(hasWearableOverride && !config.DisableSwitcher);
                previewUIPresenter.EnableAudioControls(hasEmoteAudio);
            } while (_shouldReload);

            previewUIPresenter.ShowLoader(false);
            _loading = false;
            mainCamera.cullingMask = -1; // Render everything
            avatarLoader.enabled = true; // Enables Update for Outline
            wearableLoader.enabled = true;

            if (_shouldCleanup)
            {
                Cleanup();
            }

            JSBridge.NativeCalls.OnLoadComplete();
        }

        private async Awaitable LoadForBuilder(string bodyShapeName,
            Color? eyeColor,
            Color? hairColor,
            Color? skinColor,
            string[] urns,
            string emoteName,
            List<byte[]> base64)
        {
            var bodyShape = bodyShapeName.Equals(WearablesConstants.BODY_SHAPE_FEMALE, StringComparison.OrdinalIgnoreCase)
                ? BodyShape.Female
                : BodyShape.Male;

            var base64Entities = base64.Select(EntityDefinition.FromBase64).ToArray();
            var base64Emote = base64Entities.FirstOrDefault(e => e.Type == EntityType.Emote);
            var base64WearableEntities = base64Entities.Where(e => e.Type != EntityType.Emote);

            var urnEntities = await EntityService.GetEntities(urns);

            // Slot-based deduplication: one wearable per category, base64 items take priority
            var slots = new Dictionary<string, EntityDefinition>();
            foreach (var entity in urnEntities.Where(e => e.Type != EntityType.Emote))
            {
                slots[entity.Category] = entity;
            }
            foreach (var entity in base64WearableEntities)
            {
                slots[entity.Category] = entity;
            }
            var wearableEntities = slots.Values.ToArray();

            var colors = new AvatarColors(eyeColor ?? Color.black, hairColor ?? Color.black,
                skinColor ?? DEFAULT_SKIN_COLOR);

            var emoteEntity = base64Emote ?? (emoteName == "idle" ? null : EntityDefinition.FromEmbeddedEmote(emoteName, true));

            await avatarLoader.LoadAvatar(bodyShape,
                wearableEntities,
                emoteEntity,
                Array.Empty<string>(),
                colors);

            if (AangConfiguration.Instance.DisableFace)
            {
                avatarLoader.HideFacialFeatures();
            }
        }

        private async Awaitable<(bool emoteOverride, bool emoteOverrideAudio, bool validRepresentation,
                bool showsItemAlone, BodyShape avatarBodyShape)>
            LoadForMarketplace(AangConfiguration config, List<string> urns)
        {
            var profileID = config.Profile;
            var defaultEmote = config.Emote;

            Assert.IsNotNull(profileID);
            Assert.IsNotNull(defaultEmote);

            var avatar = await APIService.GetAvatar(profileID);
            var avatarBodyShape = avatar.GetBodyShape();
            var profileColors = avatar.GetAvatarColors();
            // A caller-supplied color wins so combinations can be tried on any profile; the rest stay the profile's.
            var avatarColors = new AvatarColors(config.EyeColor ?? profileColors.Eyes,
                config.HairColor ?? profileColors.Hair, config.SkinColor ?? profileColors.Skin);
            var allEntities = await EntityService.GetEntities(avatar.wearables.Concat(urns).ToArray());

            // Resolve in request order so that, when two urns compete for the same slot below, the last
            // one wins. We match on the sanitized urn because EntityService normalizes token scoped
            // urns down to the item urn, so what comes back is not always what we asked for.
            var overrides = new List<EntityDefinition>();
            foreach (var urn in urns.Select(URNUtils.SanitizeURN))
            {
                var definition = allEntities.FirstOrDefault(ed => URNUtils.SanitizeURN(ed.URN) == urn);

                if (definition == null)
                    throw new NotSupportedException($"Could not resolve urn: {urn}");

                if (definition.Type is not (EntityType.Emote or EntityType.Wearable or EntityType.FacialFeature))
                    throw new NotSupportedException($"Trying to override type: {definition.Type}");

                if (!overrides.Contains(definition)) overrides.Add(definition);
            }

            // Only one emote can play at a time, so the first one wins and the rest is worn.
            var emoteOverride = overrides.FirstOrDefault(ed => ed.Type == EntityType.Emote);
            var wearableOverrides = overrides.Where(ed => ed.Type != EntityType.Emote).ToList();
            var renderableOverrides = wearableOverrides.Where(ed => ed.HasRepresentation(avatarBodyShape)).ToList();

            // The item-alone view renders exactly one model, so it only exists for a single wearable.
            // An emote or a multi item preview (a cart, an outfit) can only be shown on the avatar.
            var showsItemAlone = emoteOverride == null && wearableOverrides.Count == 1;

            bool hasValidRepresentation;

            if (showsItemAlone)
            {
                // Unchanged single item behaviour: skip the avatar and let the UI lock to the item view
                // with a tooltip explaining that this body shape has no representation.
                hasValidRepresentation = renderableOverrides.Count == 1;
            }
            else
            {
                if (renderableOverrides.Count < wearableOverrides.Count)
                    Debug.LogWarning(
                        $"Ignoring urns without a {avatarBodyShape} representation: " +
                        string.Join(", ", wearableOverrides.Except(renderableOverrides).Select(ed => ed.URN)));

                // There is no locked item view to fall back to here, and rendering the profile avatar
                // wearing none of the requested items would be a silent lie, so report it instead.
                if (wearableOverrides.Count > 0 && renderableOverrides.Count == 0 && emoteOverride == null)
                    throw new NotSupportedException(
                        $"None of the requested urns have a {avatarBodyShape} representation: {string.Join(", ", urns)}");

                hasValidRepresentation = true;
            }

            var emoteDefinition = emoteOverride ??
                                  (defaultEmote == "idle"
                                      ? null
                                      : EntityDefinition.FromEmbeddedEmote(defaultEmote, false));

            // Slot based composition, same as LoadForBuilder: unlike builder mode, the profile (a real
            // one or a default) fills every remaining category, so previewing a single hat still shows a
            // dressed avatar. The requested items win their own slot.
            var overriddenUrns = overrides.Select(ed => ed.URN).ToHashSet();
            var slots = new Dictionary<string, EntityDefinition>();
            foreach (var entity in allEntities.Where(ed =>
                         ed.Type != EntityType.Emote && !overriddenUrns.Contains(ed.URN)))
            {
                slots[entity.Category] = entity;
            }
            foreach (var entity in renderableOverrides)
            {
                slots[entity.Category] = entity;
            }

            // Load the avatar
            if (hasValidRepresentation)
            {
                await avatarLoader.LoadAvatar(avatarBodyShape, slots.Values, emoteDefinition,
                    // Force render every previewed category, so an item the user came to look at is
                    // never hidden by another wearable.
                    avatar.forceRender.Union(renderableOverrides.Select(ed => ed.Category)).ToArray(),
                    avatarColors);
            }

            if (showsItemAlone)
            {
                await wearableLoader.LoadWearable(wearableOverrides[0], avatarBodyShape, avatarColors);
            }
            else
            {
                wearableLoader.Cleanup();
            }

            // TODO: This check for audio clip is ugly
            return (emoteOverride != null, emoteAnimationController.HasAudio, hasValidRepresentation, showsItemAlone,
                avatarBodyShape);
        }

        /// <summary>
        /// Which view the marketplace preview opens in. An explicit request from the caller always wins;
        /// only when there is none do we fall back to the view the user last picked.
        /// </summary>
        private static bool ShouldShowAvatar(AangConfiguration config) =>
            config.Type switch
            {
                PreviewViewType.Avatar => true,
                PreviewViewType.Wearable => false,
                _ => PlayerPrefs.GetInt(PREF_AVATAR_SHOWN, 0) == 1
            };

        private async Awaitable LoadForProfile(string profileID, string defaultEmote, bool loop = false)
        {
            Assert.IsNotNull(profileID);

            var avatar = await APIService.GetAvatar(profileID);
            var entities = await EntityService.GetEntities(avatar.wearables);

            await avatarLoader.LoadAvatar(avatar.GetBodyShape(), entities,
                EntityDefinition.FromEmbeddedEmote(defaultEmote, loop), avatar.forceRender, avatar.GetAvatarColors());
        }

        private async Awaitable<List<string>> LoadUrns(AangConfiguration config)
        {
            if (config.Urns.Count > 0) return config.Urns;

            // If we have a contract and item id or token id we need to fetch the urn first
            if (config.Contract != null && (config.ItemID != null || config.TokenID != null))
            {
                return new List<string>
                {
                    config.ItemID != null
                        ? (await APIService.GetMarketplaceItemFromID(config.Contract, config.ItemID)).data[0].urn
                        : (await APIService.GetMarketplaceItemFromToken(config.Contract, config.TokenID)).data[0].nft
                        .urn
                };
            }

            return new List<string>();
        }

        public void Cleanup()
        {
            if (_loading)
            {
                _shouldCleanup = true;
                return;
            }

            _shouldCleanup = false;

            emoteAnimationController.ClearEmote();
            avatarLoader.ClearEmote();
        }
    }
}
