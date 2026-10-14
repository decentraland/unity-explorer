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
        [SerializeField] private GameObject shadowCatcher;
        [SerializeField] private GameObject glowCatcher;
        [SerializeField] private GameObject wearableGlowCatcher;

        [SerializeField] private float wearablePadding = 0.15f;

        // Remembers the view the user last switched to. In WebGL PlayerPrefs is a per-origin
        // IndexedDB store, so this is shared by every app served from the preview origin and
        // survives across sessions: only ever a fallback, never an override of what was requested.
        private const string PREF_AVATAR_SHOWN = "PreviewAvatarShown";

        // #cc9b76, the skin the JS wrapper used to send whenever a builder caller left it out.
        private static readonly Color DEFAULT_SKIN_COLOR = new(204f / 255f, 155f / 255f, 118f / 255f);

        // The entities the caller asked to preview, as resolved on the last reload: the urns and base64
        // definitions, never the profile's own wearables or the body. Metrics are reported for these.
        private readonly HashSet<string> _requestedItemUrns = new();

        // Whether the emote is already held for a static camera, so a later camera option leaves it alone.
        private bool _emoteHeldForStaticCamera;

        private ScreenshotCapture _screenshotCapture;

        private bool _loading;
        private bool _shouldReload;
        private bool _shouldCleanup;
        private bool _showingWearable;

        // What the mode and the loaded content allow; the camera options narrow it further.
        private bool _cameraInputByMode;
        private bool _autoRotateByMode;

        /// <summary>
        /// True while a reload is rebuilding the avatar, during which nothing is rendered.
        /// </summary>
        public bool IsLoading => _loading;

        /// <summary>
        /// Counts the reloads started so far, so a caller spanning several frames can tell a reload ran
        /// under it even when it has already finished.
        /// </summary>
        public int ReloadGeneration { get; private set; }

        private void Awake()
        {
            _screenshotCapture = new ScreenshotCapture(mainCamera, avatarLoader, wearableLoader);
        }

        private void OnDestroy()
        {
            previewUIPresenter.ShowAvatarClicked -= OnShowAvatarClicked;
            previewUIPresenter.ShowWearableClicked -= OnShowWearableClicked;
            previewUIPresenter.EmoteToggleClicked -= OnEmoteToggleClicked;
            previewUIPresenter.ContainerDrag -= avatarRotator.OnDrag;
            previewUIPresenter.ContainerDrag -= wearableRotator.OnDrag;
            previewUIPresenter.ContainerPan -= previewCameraController.Pan;
            emoteAnimationController.PlaybackChanged -= OnEmotePlaybackChanged;
            _screenshotCapture.Dispose();
        }

        private void Start()
        {
            previewUIPresenter.ShowAvatarClicked += OnShowAvatarClicked;
            previewUIPresenter.ShowWearableClicked += OnShowWearableClicked;
            previewUIPresenter.EmoteToggleClicked += OnEmoteToggleClicked;
            previewUIPresenter.ContainerDrag += avatarRotator.OnDrag;
            previewUIPresenter.ContainerDrag += wearableRotator.OnDrag;
            previewUIPresenter.ContainerPan += previewCameraController.Pan;
            emoteAnimationController.PlaybackChanged += OnEmotePlaybackChanged;

            StartCoroutine(Reload());
        }

        // The index travels, so the per-frame report marshals no string. The canvas button follows
        // every change too, whether the page or the user made it.
        private void OnEmotePlaybackChanged(EmotePlaybackEvent playbackEvent, float time)
        {
            JSBridge.NativeCalls.OnEmoteEvent((int)playbackEvent, time);

            if (playbackEvent is EmotePlaybackEvent.Play or EmotePlaybackEvent.Pause or EmotePlaybackEvent.End)
            {
                previewUIPresenter.SetAnimationPlaying(playbackEvent == EmotePlaybackEvent.Play);
            }
        }

        // The canvas button resumes a paused clip and restarts any other; its stop returns to idle,
        // where the bridge's StopEmote holds the first frame instead.
        private void OnEmoteToggleClicked(bool playing)
        {
            if (playing)
            {
                PlayEmote();
            }
            else
            {
                emoteAnimationController.ReturnToIdle(true);
            }
        }

        // The item view floats the item, so it carries its own glow at the wearable position rather
        // than the one under the avatar's feet.
        private void ShowWearableView(bool showWearable)
        {
            _showingWearable = showWearable;
            previewCameraController.ShowMarketplaceWearable(showWearable);

            // One 20-unit plane spans both subjects, which sit 5 apart, so the avatar's shadow drifts
            // into the item view once zoomed out. Nothing there casts, so the plane goes with the view.
            shadowCatcher.SetActive(!showWearable && PreviewConfiguration.Instance.Shadow);
            wearableGlowCatcher.SetActive(showWearable && PreviewConfiguration.Instance.Glow);
        }

        private void OnShowWearableClicked()
        {
            PlayerPrefs.SetInt(PREF_AVATAR_SHOWN, 0);

            ShowWearableView(true);
            wearableRotator.ResetRotation();
        }

        private void OnShowAvatarClicked()
        {
            PlayerPrefs.SetInt(PREF_AVATAR_SHOWN, 1);

            ShowWearableView(false);
            avatarRotator.ResetRotation();
        }

        public void SetSpringBonesParams(SpringBones.SpringBonesParamsPayload payload) =>
            avatarLoader.SetSpringBonesParams(payload);

        /// <summary>
        /// Captures the current view at the given size. See <see cref="ScreenshotCapture.CaptureAsync"/>.
        /// </summary>
        public Awaitable<string> CaptureScreenshotAsync(int width, int height) =>
            _screenshotCapture.CaptureAsync(width, height);

        /// <summary>
        /// Sums the metrics of the entities the caller asked to preview, with how many of them are loaded;
        /// zero means there is nothing to report.
        /// </summary>
        public (ModelMetrics total, int entities) GetRequestedItemsMetrics()
        {
            var total = ModelMetrics.Empty;
            var entities = 0;

            foreach (var urn in _requestedItemUrns)
            {
                // The avatar wears the item in every mode; the item-alone view loads it a second time
                // with the same geometry, so it only matters when the avatar could not wear it.
                if (avatarLoader.TryGetMetrics(urn, out var metrics)
                    || wearableLoader.TryGetMetrics(urn, out metrics))
                {
                    total += metrics;
                    entities++;
                }
            }

            return (total, entities);
        }

        /// <summary>
        /// Hides or shows every in-canvas control without a reload. The configuration keeps the value,
        /// so a later reload applies it again.
        /// </summary>
        public void SetControlsHidden(bool hidden) => previewUIPresenter.SetControlsHidden(hidden);

        /// <summary>
        /// Applies the configuration's camera options to the live scene: input gates, locks,
        /// auto-rotate, the zoom range, the offset and the thumbnail frame. The current zoom and pan are
        /// left alone, so a reload within a mode keeps the creator's framing.
        /// </summary>
        public void ApplyCameraSettings()
        {
            var config = PreviewConfiguration.Instance;
            var interactive = config.Camera == PreviewCameraKind.Interactive;

            previewCameraController.Configure(config);
            previewUIPresenter.EnableZoom(_cameraInputByMode && interactive && !config.LockRadius);
            previewUIPresenter.EnablePan(_cameraInputByMode && interactive && config.Panning);
            previewUIPresenter.ShowThumbnailBoundaries(config.ShowThumbnailBoundaries);

            // The animation reference is Babylon's scene boundaries: the same cylinder on a grid floor.
            animationReference.SetActive(config.ShowAnimationReference || config.ShowSceneBoundaries);

            ConfigureRotator(avatarRotator, config, interactive);
            ConfigureRotator(wearableRotator, config, interactive);

            // The avatar stays on a turntable - tipping a standing figure just looks like falling over.
            // A solo item is being inspected, and yaw alone cannot reach a hood's inside or a shoe's sole.
            avatarRotator.AllowVertical = false;
            wearableRotator.AllowVertical = !config.LockBeta;
            // A locked turntable does not turn on its own either.
            avatarRotator.EnableAutoRotate =
                _autoRotateByMode && !config.DisableAutoRotate && !config.LockAlpha && interactive;
            wearableRotator.EnableAutoRotate = !config.DisableAutoRotate && !config.LockAlpha && interactive;

            // A static camera is a still: the emote is held on its first frame when the camera becomes
            // static, and a later camera option leaves it where the page put it.
            if (!interactive && !_emoteHeldForStaticCamera)
            {
                emoteAnimationController.StopEmote();
            }

            _emoteHeldForStaticCamera = !interactive;
        }

        /// <summary>
        /// Back to the start of the zoom range, after the zoom options changed.
        /// </summary>
        public void RestartZoom() => previewCameraController.RestartZoom();

        /// <summary>
        /// Clears to the configuration's background, premultiplied: the canvas holds premultiplied
        /// colour, so a straight clear would show a transparent white as white, on screen and in a
        /// capture alike.
        /// </summary>
        public void ApplyBackground()
        {
            var background = PreviewConfiguration.Instance.Background;

            mainCamera.backgroundColor = new Color(background.r * background.a, background.g * background.a,
                background.b * background.a, background.a);
        }

        /// <summary>
        /// A zoom button press from the page: positive pulls closer.
        /// </summary>
        public void ZoomBy(float delta) => previewCameraController.ZoomByDelta(delta);

        /// <summary>
        /// Puts every view at the configuration's offset, across and up the screen and back, in metres.
        /// </summary>
        public void ApplyCameraOffset() =>
            previewCameraController.SetOffset(PreviewConfiguration.Instance.Offset, PreviewConfiguration.Instance.OffsetDepth);

        /// <summary>
        /// Moves the view by Babylon's orbit deltas: alpha turns the subject, beta tilts it, both in
        /// radians, and radius pulls the orbit back by metres.
        /// </summary>
        public void MoveCamera(float alpha, float beta, float radius)
        {
            var rotator = _showingWearable ? wearableRotator : avatarRotator;

            // Babylon's beta lowers its camera, which reads as the subject's top tilting towards the
            // viewer; the rotator's pitch runs the other way.
            rotator.Rotate(alpha * Mathf.Rad2Deg, -beta * Mathf.Rad2Deg);
            previewCameraController.OrbitBy(radius);
        }

        // The page's camera position turns each view from where Babylon's default camera for it stands.
        // The avatar stays upright, as it does under a drag.
        private void ApplyCameraStart(PreviewConfiguration config)
        {
            var (avatarAlpha, _) = BabylonCamera.AnglesFrom(BabylonCamera.Resolve(config, BabylonCamera.AVATAR),
                BabylonCamera.AVATAR);
            var (itemAlpha, itemBeta) = BabylonCamera.AnglesFrom(BabylonCamera.Resolve(config, BabylonCamera.ITEM),
                BabylonCamera.ITEM);

            // The same signs as MoveCamera: Babylon's beta tilts the rotator's pitch the other way.
            avatarRotator.SetStartRotation(avatarAlpha, 0f);
            wearableRotator.SetStartRotation(itemAlpha, -itemBeta);
        }

        private static void ConfigureRotator(DragRotator rotator, PreviewConfiguration config, bool interactive)
        {
            rotator.InputEnabled = interactive;
            rotator.AllowHorizontal = !config.LockAlpha;
            rotator.AutoRotateSpeed = config.AutoRotateSpeed * Mathf.Rad2Deg;
        }

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
            ReloadGeneration++;
            mainCamera.cullingMask = 0; // Render nothing
            avatarLoader.enabled = false; // Disables Update for Outline
            wearableLoader.enabled = false; // Disables Update for Outline

            do
            {
                _shouldReload = false;

                // We store the instance in case it gets recreated by a call to PreviewConfiguration.RecreateFrom
                var config = PreviewConfiguration.Instance;

                _requestedItemUrns.Clear();
                var wasShowingWearable = _showingWearable;
                _showingWearable = false;
                previewUIPresenter.SetControlsHidden(config.HideControls);

                avatarRotator.enabled = false;
                wearableRotator.enabled = false;
                avatarRotator.ResetRotation();
                wearableRotator.ResetRotation();

                animationReference.SetActive(config.ShowAnimationReference || config.ShowSceneBoundaries);
                platform.SetActive(config.Mode is PreviewMode.Authentication);
                shadowCatcher.SetActive(config.Shadow);
                glowCatcher.SetActive(config.Glow);
                wearableGlowCatcher.SetActive(false);
                ApplyBackground();
                mainCamera.orthographic = config.Projection == "orthographic";
                previewUIPresenter.EnableLoader(!config.DisableLoader);

                // Before the mode, whose change starts the framing from these options.
                previewCameraController.Configure(config);
                previewCameraController.SetMode(config.Mode);
                confirmationVFX.gameObject.SetActive(config.Mode is PreviewMode.Jesus);

                var hasEmoteOverride = false;
                var hasWearableOverride = false;
                var hasEmoteAudio = false;
                var showingAvatar = false;

                try
                {
                    // The hosts the page chose, before the first request of this load.
                    APIService.PeerUrl = config.PeerUrl;
                    APIService.MarketplaceServerUrl = config.MarketplaceServerUrl;

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
                            await LoadForBuilder(config);
                            break;
                        case PreviewMode.Jesus:
                            showingAvatar = true;
                            confirmationVFX.Play();
                            await LoadForProfile(config.Profile, "character/Particles_Anim", true);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    // Only marketplace shows an item on its own. Any other mode drops one left over from
                    // an earlier marketplace load, or it would answer a metrics request for an item the
                    // avatar no longer wears. After the load, so a failed one still shows what was there.
                    if (config.Mode is not PreviewMode.Marketplace)
                    {
                        wearableLoader.Cleanup();
                    }

                    if (config.DisableFace)
                    {
                        avatarLoader.HideFacialFeatures();
                    }
                }
                catch (Exception e)
                {
                    JSBridge.NativeCalls.OnError(e.Message);

                    // Nothing of this request is loaded, and the reload ends so later requests are
                    // served; one queued meanwhile is started again since the loop is left here.
                    _requestedItemUrns.Clear();
                    var reloadQueued = _shouldReload;
                    EndReload();

                    // The failed load leaves the last subject on screen, so its view is restored while
                    // it is still there; the item view only exists in marketplace with an item loaded.
                    var itemViewAvailable = config.Mode is PreviewMode.Marketplace && wearableLoader.HasLoadedItem;

                    if (config.Mode is PreviewMode.Marketplace)
                    {
                        ShowWearableView(wasShowingWearable && itemViewAvailable);
                    }

                    if (!itemViewAvailable)
                    {
                        previewUIPresenter.SetSwitcherState(PreviewUIPresenter.SwitcherState.Avatar);
                        previewUIPresenter.EnableSwitcher(false);
                    }

                    if (reloadQueued) StartCoroutine(Reload());

                    throw;
                }

                // Wait for 1 frame for animation to kick in before measuring bounds for the framing below
                await Awaitable.NextFrameAsync();

                // The item view only: the avatar is never moved or scaled. Its feet have to stay on the
                // shadow and glow catchers, which sit at a fixed floor height, so an emote is framed by
                // the camera fit below instead.
                if (hasWearableOverride)
                {
                    GameObjectUtils.CenterAndFit(wearableLoader.transform, mainCamera, wearablePadding);
                    wearableLoader.transform.position += wearableOffset;
                }

                if (config.Mode is PreviewMode.Marketplace)
                {
                    // Here rather than earlier: the CenterAndFit above and the frame of animation before
                    // it are what settle the bounds these are measured from.
                    previewCameraController.FitAvatarView(avatarLoader.transform);

                    if (hasWearableOverride)
                        previewCameraController.FitWearableView(wearableLoader.transform);

                    ShowWearableView(!showingAvatar);
                }
                else if (config.Mode is PreviewMode.Builder)
                {
                    avatarRotator.DragSpeed = 2f;
                }

                _autoRotateByMode = config.Mode is PreviewMode.Marketplace && !hasEmoteOverride;
                _cameraInputByMode = config.Mode is PreviewMode.Marketplace or PreviewMode.Builder;

                // The load replayed the emote, so a static camera has to hold it again, whatever a
                // setter that arrived mid-load recorded.
                _emoteHeldForStaticCamera = false;
                ApplyCameraSettings();
                ApplyCameraStart(config);

                previewUIPresenter.EnableEmoteControls(hasEmoteOverride);
                previewUIPresenter.EnableSwitcher(hasWearableOverride && !config.DisableSwitcher);
                previewUIPresenter.EnableAudioControls(hasEmoteAudio);
            } while (_shouldReload);

            EndReload();
            JSBridge.NativeCalls.OnLoadComplete();
        }

        private void EndReload()
        {
            previewUIPresenter.ShowLoader(false);

            _loading = false;
            mainCamera.cullingMask = -1; // Render everything
            avatarLoader.enabled = true; // Enables Update for Outline
            wearableLoader.enabled = true;

            // On a failed load the previous subject is shown again, and has to turn as before.
            avatarRotator.enabled = true;
            wearableRotator.enabled = true;

            if (_shouldCleanup)
            {
                Cleanup();
            }
        }

        private async Awaitable LoadForBuilder(PreviewConfiguration config)
        {
            var bodyShape = ParseBodyShape(config.BodyShape) ?? BodyShape.Male;

            var localEntities = await LoadLocalEntities(config);
            var localEmote = localEntities.FirstOrDefault(e => e.Type == EntityType.Emote);
            var localWearables = localEntities.Where(e => e.Type != EntityType.Emote);

            var urnEntities = await EntityService.GetEntities(config.Urns.ToArray());

            foreach (var entity in urnEntities)
            {
                if (entity.Type != EntityType.Body) _requestedItemUrns.Add(entity.URN);
            }

            foreach (var entity in localEntities)
            {
                _requestedItemUrns.Add(entity.URN);
            }

            // Slot-based deduplication: one wearable per category, the page's own items take priority
            var slots = new Dictionary<string, EntityDefinition>();
            foreach (var entity in urnEntities.Where(e => e.Type != EntityType.Emote))
            {
                slots[entity.Category] = entity;
            }
            foreach (var entity in localWearables)
            {
                slots[entity.Category] = entity;
            }

            if (!config.DisableDefaultWearables)
            {
                await FillDefaultWearables(slots, bodyShape, !config.DisableFace);
            }

            var colors = new AvatarColors(config.EyeColor ?? Color.black, config.HairColor ?? Color.black,
                config.SkinColor ?? DEFAULT_SKIN_COLOR);

            await avatarLoader.LoadAvatar(bodyShape,
                slots.Values.ToArray(),
                localEmote ?? DefaultEmote(config.Emote, true),
                Array.Empty<string>(),
                colors);
        }

        // Null when the caller named none, so the profile's or the mode's own can take over.
        private static BodyShape? ParseBodyShape(string urn)
        {
            if (string.IsNullOrEmpty(urn)) return null;

            return urn.Equals(WearablesConstants.BODY_SHAPE_FEMALE, StringComparison.OrdinalIgnoreCase)
                ? BodyShape.Female
                : BodyShape.Male;
        }

        // "idle" and "none" resolve to no emote: the rig's own Idle clip plays with nothing to download.
        private static EntityDefinition DefaultEmote(string name, bool loop) =>
            name is PreviewConfiguration.DEFAULT_EMOTE or PreviewConfiguration.NO_EMOTE
                ? null
                : EntityDefinition.FromEmbeddedEmote(name, loop);

        // The items the page handed over itself: base64 definitions and definitions it hosts at a URL.
        private static async Awaitable<List<EntityDefinition>> LoadLocalEntities(PreviewConfiguration config)
        {
            var entities = config.Base64.Select(EntityDefinition.FromBase64).ToList();

            foreach (var url in config.Urls)
            {
                entities.Add(EntityDefinition.FromDefinitionJson(await APIService.GetText(url)));
            }

            return entities;
        }

        // Dresses the empty categories with the base wearables, so a lone item never shows on a naked
        // avatar.
        private static async Awaitable FillDefaultWearables(Dictionary<string, EntityDefinition> slots,
            BodyShape bodyShape, bool includeFacialFeatures)
        {
            var categories = includeFacialFeatures
                ? WearablesConstants.DEFAULT_BODY_CATEGORIES.Concat(WearablesConstants.DEFAULT_FACIAL_CATEGORIES)
                : WearablesConstants.DEFAULT_BODY_CATEGORIES;

            var missing = categories
                .Where(category => !slots.ContainsKey(category))
                .Select(category => WearablesConstants.GetDefaultWearableUrn(category, bodyShape))
                .ToArray();

            if (missing.Length == 0) return;

            foreach (var entity in await EntityService.GetEntities(missing))
            {
                slots[entity.Category] = entity;
            }
        }

        private async Awaitable<(bool emoteOverride, bool emoteOverrideAudio, bool validRepresentation,
                bool showsItemAlone, BodyShape avatarBodyShape)>
            LoadForMarketplace(PreviewConfiguration config, List<string> urns)
        {
            var profileID = config.Profile;
            var defaultEmote = config.Emote;

            Assert.IsNotNull(profileID);
            Assert.IsNotNull(defaultEmote);

            var avatar = await APIService.GetAvatar(profileID);
            // A caller-supplied body shape or color wins so combinations can be tried on any profile; the
            // rest stay the profile's.
            var avatarBodyShape = ParseBodyShape(config.BodyShape) ?? avatar.GetBodyShape();
            var profileColors = avatar.GetAvatarColors();
            var avatarColors = new AvatarColors(config.EyeColor ?? profileColors.Eyes,
                config.HairColor ?? profileColors.Hair, config.SkinColor ?? profileColors.Skin);

            // Without the defaults, the profile dresses nothing: only the requested items are worn.
            var profileWearables = config.DisableDefaultWearables ? Array.Empty<string>() : avatar.wearables;
            var allEntities = await EntityService.GetEntities(profileWearables.Concat(urns).ToArray());
            var localEntities = await LoadLocalEntities(config);

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

            // The page's own items count as requested too, after the urns, so the last one still wins
            // its category.
            foreach (var definition in localEntities)
            {
                if (!overrides.Contains(definition)) overrides.Add(definition);
            }

            foreach (var definition in overrides)
            {
                _requestedItemUrns.Add(definition.URN);
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

            var emoteDefinition = emoteOverride ?? DefaultEmote(defaultEmote, false);

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
                // From the avatar rig because that is where the Idle clip lives - the item view has none.
                await wearableLoader.LoadWearable(wearableOverrides[0], avatarBodyShape, avatarColors,
                    avatarLoader.GetIdlePose());
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
        private static bool ShouldShowAvatar(PreviewConfiguration config) =>
            config.Type switch
            {
                PreviewViewType.Avatar => true,
                PreviewViewType.Wearable => false,
                _ => PlayerPrefs.GetInt(PREF_AVATAR_SHOWN, 0) == 1
            };

        /// <summary>
        /// Unlike LoadForBuilder and LoadForMarketplace, this does NOT special-case "idle" — the default
        /// emote value — so Profile and Authentication fetch StreamingAssets/idle.glb (132KB) to play it
        /// once before crossfading to the scene rig's own Idle clip. The fetch buys the first 3s only.
        /// </summary>
        private async Awaitable LoadForProfile(string profileID, string defaultEmote, bool loop = false)
        {
            Assert.IsNotNull(profileID);

            var avatar = await APIService.GetAvatar(profileID);
            var entities = await EntityService.GetEntities(avatar.wearables);
            var emote = defaultEmote == PreviewConfiguration.NO_EMOTE
                ? null
                : EntityDefinition.FromEmbeddedEmote(defaultEmote, loop);

            await avatarLoader.LoadAvatar(avatar.GetBodyShape(), entities, emote, avatar.forceRender,
                avatar.GetAvatarColors());
        }

        private async Awaitable<List<string>> LoadUrns(PreviewConfiguration config)
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
