using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Data;
using Newtonsoft.Json.Linq;
using Preview;
using Services;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Utils;
using Loading;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Artist tool for composing an outfit from marketplace wearables, posing the avatar with an
    /// emote and capturing stills / video. Browsing and preset editing work in edit mode; the live
    /// preview and capture drive the existing renderer (builder mode) in play mode.
    /// </summary>
    public class OutfitStudioWindow : EditorWindow
    {
        private const int PAGE_SIZE = 36;
        private const int THUMB_SIZE = 90;

        // Cap on tag-matched items collected from the catalyst lambdas endpoint per search (see
        // RunSearch) - a discovery-only pass, not the full result set, so this can stay well below
        // FETCH_CAP without losing practical recall.
        private const int TAG_SEARCH_CAP = 500;

        // The live marketplace-api ignores sortBy entirely (verified: every documented value -
        // newest, recently_listed, recently_sold, cheapest, most_expensive - returns items in the
        // exact same server order, prices/dates included). So there's no way to get a correct sort
        // via pagination; instead we fetch every item matching the current filters (up to this cap)
        // in one go and sort the whole set client-side. A true, uncapped global sort isn't practical
        // for a broad, unfiltered browse (e.g. ~11k wearables) without fetching the entire catalog,
        // so results are labelled "first N of total" whenever the cap is hit.
        private const int FETCH_CAP = 3000;
        private const float NECK_LOOK_SHARE = 0.4f; // fraction of the look-at turn given to the neck vs. the head

        private static readonly List<string> WEARABLE_SLOTS = new()
        {
            "any", "upper_body", "lower_body", "feet", "hands_wear", "hat", "helmet", "hair",
            "facial_hair", "eyewear", "earring", "tiara", "top_head", "mask", "skin",
            "eyes", "eyebrows", "mouth"
        };

        private static readonly List<string> EMOTE_CATEGORIES = new()
        {
            "any", "dance", "poses", "fun", "greetings", "reactions", "stunt", "horror", "miscellaneous"
        };

        // Wearable categories that make up a face/body look rather than an outfit item. Browsed from
        // the Avatar tab, but equipped into outfit.urns like any other wearable, so they travel in
        // share codes and presets (see RegisterCatalystEntities for what makes that work).
        private static readonly List<string> FACE_SLOTS = new()
        {
            "eyes", "eyebrows", "mouth", "hair", "facial_hair"
        };

        private static readonly Dictionary<string, string> FACE_SLOT_LABELS = new()
        {
            ["eyes"] = "Eyes",
            ["eyebrows"] = "Eyebrows",
            ["mouth"] = "Mouth",
            ["hair"] = "Hair",
            ["facial_hair"] = "Facial Hair"
        };

        // Curated Decentraland base-avatar (off-chain) options per face-feature slot — first stage
        // deliberately skips the marketplace here: these off-chain URNs aren't resolvable via
        // CatalogService (marketplace-api only serves collection items), and the artist can still
        // reach marketplace hair/etc. through the Wearables tab. Mirrors the same curated set the
        // in-game avatar Configurator ships with (Assets/Scripts/Configurator/ConfiguratorController.cs,
        // faceCategories, serialized on the OutfitStudio scene).
        private static readonly Dictionary<string, string[]> DEFAULT_FACE_URNS = new()
        {
            ["hair"] = new[]
            {
                "urn:decentraland:off-chain:base-avatars:standard_hair",
                "urn:decentraland:off-chain:base-avatars:casual_hair_01",
                "urn:decentraland:off-chain:base-avatars:semi_afro",
                "urn:decentraland:off-chain:base-avatars:modern_hair",
                "urn:decentraland:off-chain:base-avatars:hair_anime_01",
                "urn:decentraland:off-chain:base-avatars:hair_undere",
                "urn:decentraland:off-chain:base-avatars:keanu_hair",
                "urn:decentraland:off-chain:base-avatars:shoulder_bob_hair",
                "urn:decentraland:off-chain:base-avatars:hair_f_oldie_02",
                "urn:decentraland:off-chain:base-avatars:cool_hair",
                "urn:decentraland:off-chain:base-avatars:tall_front_01",
                "urn:decentraland:off-chain:base-avatars:pony_tail",
                "urn:decentraland:off-chain:base-avatars:rasta",
                "urn:decentraland:off-chain:base-avatars:casual_hair_02",
                "urn:decentraland:off-chain:base-avatars:curtained_hair",
                "urn:decentraland:off-chain:base-avatars:semi_bold",
                "urn:decentraland:off-chain:base-avatars:curly_hair",
                "urn:decentraland:off-chain:base-avatars:double_bun",
                "urn:decentraland:off-chain:base-avatars:punk"
            },
            ["eyes"] = new[]
            {
                "urn:decentraland:off-chain:base-avatars:f_eyes_00",
                "urn:decentraland:off-chain:base-avatars:eyes_00",
                "urn:decentraland:off-chain:base-avatars:eyes_01",
                "urn:decentraland:off-chain:base-avatars:f_eyes_10",
                "urn:decentraland:off-chain:base-avatars:f_eyes_01",
                "urn:decentraland:off-chain:base-avatars:eyes_04",
                "urn:decentraland:off-chain:base-avatars:eyes_07",
                "urn:decentraland:off-chain:base-avatars:eyes_08",
                "urn:decentraland:off-chain:base-avatars:eyes_21",
                "urn:decentraland:off-chain:base-avatars:eyes_16",
                "urn:decentraland:off-chain:base-avatars:eyes_20",
                "urn:decentraland:off-chain:base-avatars:eyes_15",
                "urn:decentraland:off-chain:base-avatars:eyes_03",
                "urn:decentraland:off-chain:base-avatars:eyes_22",
                "urn:decentraland:off-chain:base-avatars:f_eyes_05",
                "urn:decentraland:off-chain:base-avatars:f_eyes_06",
                "urn:decentraland:off-chain:base-avatars:eyes_11",
                "urn:decentraland:off-chain:base-avatars:f_eyes_02",
                "urn:decentraland:off-chain:base-avatars:f_eyes_04",
                "urn:decentraland:off-chain:base-avatars:f_eyes_08"
            },
            ["eyebrows"] = new[]
            {
                "urn:decentraland:off-chain:base-avatars:eyebrows_00",
                "urn:decentraland:off-chain:base-avatars:f_eyebrows_00",
                "urn:decentraland:off-chain:base-avatars:eyebrows_01",
                "urn:decentraland:off-chain:base-avatars:eyebrows_02",
                "urn:decentraland:off-chain:base-avatars:eyebrows_04",
                "urn:decentraland:off-chain:base-avatars:eyebrows_05",
                "urn:decentraland:off-chain:base-avatars:eyebrows_07",
                "urn:decentraland:off-chain:base-avatars:eyebrows_09",
                "urn:decentraland:off-chain:base-avatars:eyebrows_11",
                "urn:decentraland:off-chain:base-avatars:eyebrows_12",
                "urn:decentraland:off-chain:base-avatars:eyebrows_14",
                "urn:decentraland:off-chain:base-avatars:eyebrows_15",
                "urn:decentraland:off-chain:base-avatars:eyebrows_17",
                "urn:decentraland:off-chain:base-avatars:f_eyebrows_02",
                "urn:decentraland:off-chain:base-avatars:f_eyebrows_03",
                "urn:decentraland:off-chain:base-avatars:f_eyebrows_04",
                "urn:decentraland:off-chain:base-avatars:f_eyebrows_05",
                "urn:decentraland:off-chain:base-avatars:f_eyebrows_06",
                "urn:decentraland:off-chain:base-avatars:f_eyebrows_07",
                "urn:decentraland:off-chain:base-avatars:eyebrows_8"
            },
            ["mouth"] = new[]
            {
                "urn:decentraland:off-chain:base-avatars:f_mouth_00",
                "urn:decentraland:off-chain:base-avatars:f_mouth_01",
                "urn:decentraland:off-chain:base-avatars:f_mouth_02",
                "urn:decentraland:off-chain:base-avatars:f_mouth_03",
                "urn:decentraland:off-chain:base-avatars:f_mouth_04",
                "urn:decentraland:off-chain:base-avatars:f_mouth_05",
                "urn:decentraland:off-chain:base-avatars:f_mouth_06",
                "urn:decentraland:off-chain:base-avatars:f_mouth_07",
                "urn:decentraland:off-chain:base-avatars:f_mouth_08",
                "urn:decentraland:off-chain:base-avatars:mouth_00",
                "urn:decentraland:off-chain:base-avatars:mouth_01",
                "urn:decentraland:off-chain:base-avatars:mouth_02",
                "urn:decentraland:off-chain:base-avatars:mouth_03",
                "urn:decentraland:off-chain:base-avatars:mouth_04",
                "urn:decentraland:off-chain:base-avatars:mouth_05",
                "urn:decentraland:off-chain:base-avatars:mouth_06",
                "urn:decentraland:off-chain:base-avatars:mouth_07",
                "urn:decentraland:off-chain:base-avatars:mouth_09",
                "urn:decentraland:off-chain:base-avatars:mouth_10",
                "urn:decentraland:off-chain:base-avatars:mouth_11"
            },
            ["facial_hair"] = new[]
            {
                "urn:decentraland:off-chain:base-avatars:balbo_beard",
                "urn:decentraland:off-chain:base-avatars:beard",
                "urn:decentraland:off-chain:base-avatars:chin_beard",
                "urn:decentraland:off-chain:base-avatars:french_beard",
                "urn:decentraland:off-chain:base-avatars:full_beard",
                "urn:decentraland:off-chain:base-avatars:goatee_beard",
                "urn:decentraland:off-chain:base-avatars:granpa_beard",
                "urn:decentraland:off-chain:base-avatars:handlebar",
                "urn:decentraland:off-chain:base-avatars:horseshoe_beard",
                "urn:decentraland:off-chain:base-avatars:lincoln_beard",
                "urn:decentraland:off-chain:base-avatars:mustache_short_beard",
                "urn:decentraland:off-chain:base-avatars:old_mustache_beard",
                "urn:decentraland:off-chain:base-avatars:short_boxed_beard"
            }
        };

        private static readonly List<string> RARITIES = new()
        {
            "any", "common", "uncommon", "rare", "epic", "legendary", "exotic", "mythic", "unique"
        };

        private static readonly List<string> GENDERS = new() { "any", "male", "female", "unisex" };

        // Labels match the Decentraland marketplace's own Sort dropdown, in the same order, so the
        // two tools read the same way. "Name" is a local-only convenience option the marketplace
        // doesn't have.
        private static readonly List<string> SORT_OPTIONS = new()
        {
            "Newest", "Recently Listed", "Recently Sold", "Cheapest", "Most Expensive", "Name"
        };

        private static readonly Dictionary<string, string> SORT_API_VALUES = new()
        {
            ["Newest"] = "newest",
            ["Recently Listed"] = "recently_listed",
            ["Recently Sold"] = "recently_sold",
            ["Cheapest"] = "cheapest",
            ["Most Expensive"] = "most_expensive",
            ["Name"] = "name"
        };

        // Every .glb sitting at the root of StreamingAssets/, which is exactly the set the embedded-emote
        // path can resolve (Representation.ForEmbeddedEmote → StreamingAssets/<name>.glb). Locomotion
        // first, then the gesture emotes. walk/run/jump/wave shipped all along but were never listed, so
        // the only way to reach them was to hand-edit the emote field.
        //
        // Deliberately NOT here: the character/ subfolder (Wave_Male, Wave_Female, Outfit_*, Particles_*).
        // Those are the configurator's own clips — body-shape-specific or tied to a wearable category —
        // so they'd need a body-shape match to be correct, unlike these which are shared by both shapes.
        private static readonly List<string> EMBEDDED_EMOTES = new()
        {
            "idle", "walk", "run", "jump",
            "clap", "dab", "dance", "wave", "fashion", "fashion-2", "fashion-3", "fashion-4",
            "love", "money", "fist-pump", "head-explode"
        };

        // Shown in the "Embedded" popup whenever outfit.emote isn't one of EMBEDDED_EMOTES (a pose,
        // a marketplace/draft emote URN, ...). Without this, the popup silently fell back to
        // showing "idle" (index 0) while a pose was actually loaded/playing — reselecting "idle"
        // from that state is a no-op (same value = no change event), so nothing reloaded and the
        // transport buttons kept controlling the stale pose instead of the emote the popup claimed
        // to have selected. Having a distinct sentinel means any real embedded-emote pick is always
        // a genuine value change.
        private const string EMBEDDED_EMOTE_NONE = "— pose/other selected —";

        // Top of the popup: no animation at all, just the neutral bind pose (see
        // OutfitDefinition.NEUTRAL_POSE_EMOTE). It's a bundled clip in Neutral/ rather than Poses/
        // so it doesn't also turn up as a pose button — the popup is where it belongs.
        private const string TPOSE_LABEL = "None (T-pose)";
        private const string TPOSE_EMOTE = OutfitDefinition.NEUTRAL_POSE_EMOTE;

        private static readonly List<string> EMBEDDED_EMOTE_CHOICES =
            new[] { TPOSE_LABEL, EMBEDDED_EMOTE_NONE }.Concat(EMBEDDED_EMOTES).ToList();

        // Single-frame screenshot poses, kept fully inside the tool folder (Assets/OutfitStudio/Poses/)
        // so nothing spills into the rest of the repo. They still ride the stock embedded-emote path
        // with ZERO renderer changes: the emote name is resolved as Path.Combine(streamingAssetsPath,
        // name + ".glb"), so a name that walks back out of StreamingAssets with ".." lands in the tool
        // folder — "../OutfitStudio/Poses/<file>" → <project>/Assets/OutfitStudio/Poses/<file>.glb.
        // The ".." is normalised by the OS/URI when the loader opens the file (same bare-path handling
        // the StreamingAssets emotes already rely on). Editor-only (poses aren't in production builds).
        private const string POSES_DIR_UNDER_ASSETS = "OutfitStudio/Poses";       // for the file scan
        private const string POSES_EMBEDDED_PREFIX = "../OutfitStudio/Poses";      // relative to StreamingAssets

        // Default folder the "Save current…" card-colour-preset dialog points at (presets can live
        // anywhere - they're discovered project-wide by type).
        private const string CARD_PRESETS_DIR = "Assets/OutfitStudio/CardPresets";

        // Default folders the shader tuning presets' "Save current…" dialogs point at (same
        // discover-by-type convention as CARD_PRESETS_DIR).
        private const string TOON_SHADER_PRESETS_DIR = "Assets/OutfitStudio/ShaderPresets/Toon";
        private const string PBR_SHADER_PRESETS_DIR = "Assets/OutfitStudio/ShaderPresets/PBR";

        private static readonly Dictionary<string, Color> RARITY_COLORS = new()
        {
            ["common"] = new Color(0.67f, 0.79f, 0.85f),
            ["uncommon"] = new Color(1.00f, 0.65f, 0.40f),
            ["rare"] = new Color(0.34f, 0.87f, 0.62f),
            ["epic"] = new Color(0.44f, 0.62f, 1.00f),
            ["legendary"] = new Color(0.63f, 0.40f, 0.90f),
            ["exotic"] = new Color(0.88f, 0.94f, 0.43f),
            ["mythic"] = new Color(1.00f, 0.43f, 0.86f),
            ["unique"] = new Color(1.00f, 0.75f, 0.25f)
        };

        // Persisted state (survives domain reload / play mode transitions)
        [SerializeField] private OutfitDefinition outfit = new();
        [SerializeField] private bool autoApply = true;
        [SerializeField] private bool applyOnPlay;
        [SerializeField] private int envIndex;
        [SerializeField] private int captureWidth = 2048;
        [SerializeField] private int captureHeight = 2048;
        [SerializeField] private int captureUpsample = 1;
        [SerializeField] private int captureFrameRate = 30;
        [SerializeField] private bool transparentBackground = true;
        [SerializeField] private string outputFolder = OutfitCapture.DEFAULT_OUTPUT_FOLDER;
        [SerializeField] private float turntableDuration = 6f;
        [SerializeField] private float rotationSnapAngle;
        [SerializeField] private bool cleanGameView = true;
        [SerializeField] private bool autoFrameItem = true;

        // Off by default, and stays off across domain reloads only because it's serialized like the rest.
        // Gates the toolbar's MANA/USD readout, and with it the only outbound request this tool makes on
        // a timer — so leaving it on shouldn't be the accident of having opened the window once.
        [SerializeField] private bool stressMode;

        // Browser state (session only)
        // On Sale / Primary Sales start on: the tool shoots marketing art for items that are actually
        // buyable, so the unfiltered catalog (every item ever published) is the rarer case. Defaulted
        // here rather than on CatalogQuery itself - the URN-hydration and published-collection queries
        // share that type and must stay unfiltered. The toggles read these at build time.
        private readonly CatalogQuery _query = new() { IsOnSale = true, OnlyMinting = true };
        private CatalogItem[] _fetchedItems = Array.Empty<CatalogItem>(); // raw, unsorted, current filters
        private CatalogItem[] _sortedResults = Array.Empty<CatalogItem>(); // _fetchedItems, sorted for display
        private int _fetchedTotal; // server-reported total for the current filters (may exceed FETCH_CAP)
        private int _displayOffset; // position of the current page within _sortedResults
        private int _searchSequence;

        // urn -> catalog item, used to resolve slot/name/thumbnail for outfit rows
        private readonly Dictionary<string, CatalogItem> _knownItems = new();

        private EntityDefinition[] _faceEntities = Array.Empty<EntityDefinition>();
        private string _faceCategory = FACE_SLOTS[0];
        private int _faceSearchSequence;
        private VisualElement _faceGrid;
        private Button[] _faceCategoryButtons;

        private static readonly Dictionary<string, Texture2D> THUMBNAIL_CACHE = new();
        private static readonly HashSet<string> THUMBNAILS_IN_FLIGHT = new();

        // UI references
        private VisualElement _grid;
        private VisualElement _avatarPane;
        private VisualElement _browserContent;
        private VisualElement _debugPane;
        private TextField _configField;
        private Label _pageLabel;
        private Button _prevButton, _nextButton;
        private Button _invertSortButton;
        private bool _invertSort;
        private VisualElement _slotsContainer;

        // Shown only while a wearable is isolated (see RefreshIsolation). The outfit list above it stays
        // put either way — isolation is a view of that list, not a second subject with its own sections.
        private VisualElement _framingSection;
        private Label _framingHeader;

        // Pushes outfit.solo* back onto the framing controls, which are built once and only shown/hidden.
        // Required, not polish: without it a preset load leaves them displaying the previous item's values
        // and the next drag writes that stale number back (§22).
        private Action _syncFramingFields;

        private VisualElement _presetsSection;
        private VisualElement _shareCodeSection;

        private Label _poseLabel;
        private Label _rotationLabel;
        private Label _manaRateLabel;
        private PopupField<string> _emotePopup;
        private TextField _shareCodeField;
        private Label _statusLabel;
        private Button _playButton;
        private Button _videoButton;
        private Slider _emoteSlider;
        private PopupField<string> _bodyShapePopup;
        private ColorField _skinField, _hairField, _eyeField;
        private IVisualElementScheduledItem _pendingApply;

        public const string STUDIO_SCENE_PATH = "Assets/OutfitStudio/Scenes/OutfitStudio.unity";

        [MenuItem("Decentraland/Outfit Studio")]
        public static void Open()
        {
            var window = GetWindow<OutfitStudioWindow>("Outfit Studio");
            window.minSize = new Vector2(760, 480);
        }

        /// <summary>
        /// Opens the dedicated studio scene (a stripped copy of Main.unity with set dressing —
        /// see IMPLEMENTATION.md). The tool works in whichever scene is open; this is a shortcut.
        /// </summary>
        [MenuItem("Decentraland/Open Outfit Studio Scene")]
        public static void OpenStudioScene()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[OutfitStudio] Exit play mode before switching scenes");
                return;
            }

            if (!System.IO.File.Exists(STUDIO_SCENE_PATH))
            {
                Debug.LogError($"[OutfitStudio] Studio scene not found at {STUDIO_SCENE_PATH}");
                return;
            }

            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(STUDIO_SCENE_PATH);
            }
        }

        private void OnEnable()
        {
            APIService.Environment = envIndex == 1 ? "zone" : "org";
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            OutfitHidingReport.Changed += OnHidingReportChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            OutfitHidingReport.Changed -= OnHidingReportChanged;
        }

        /// <summary>
        /// Re-labels the slot rows once an avatar assembly has resolved which categories are hidden.
        /// Fires from inside the loader, so the rebuild is deferred to the next frame rather than
        /// mutating the visual tree mid-load.
        /// </summary>
        private void OnHidingReportChanged()
        {
            if (_slotsContainer == null) return;

            rootVisualElement.schedule.Execute(RefreshSlots);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode when applyOnPlay:
                    applyOnPlay = false;
                    // Give Bootstrap a moment to parse the debug config and kick off its initial load
                    rootVisualElement.schedule.Execute(Apply).StartingIn(1000);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    OutfitCapture.StopVideo();
                    UpdatePlayModeUI();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    UpdatePlayModeUI();
                    break;
            }
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            root.Add(BuildToolbar());

            var split = new TwoPaneSplitView(0, 450, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            split.Add(BuildBrowserPane());
            split.Add(BuildOutfitPane());
            root.Add(split);

            _statusLabel = new Label("Ready");
            _statusLabel.style.paddingLeft = 6;
            _statusLabel.style.paddingBottom = 2;
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(_statusLabel);

            HydrateKnownItems();
            RefreshSlots();
            RefreshShareCode();
            UpdatePlayModeUI();
            RunSearch();

            // PreviewController re-enables the overlay controls after every reload,
            // so Clean View re-enforces suppression on a cadence instead of one-shot
            root.schedule.Execute(EnforceCleanGameView).Every(500);

            // Stress Mode's minute cadence. One permanent scheduled item that no-ops while the toggle is
            // off, rather than starting and stopping a schedule: the tick is cheap, and a paused-item
            // lifetime to get wrong is not. Also seeds the label if the toggle came back on serialized.
            RefreshStressMode();
            root.schedule.Execute(RefreshManaRate).Every(60_000);
        }

        // ---------------------------------------------------------------- Stress Mode (MANA/USD readout)

        /// <summary>
        /// Shows or hides the toolbar rate readout to match the toggle, and fetches immediately on the way
        /// on so it doesn't sit blank for up to a minute waiting for the cadence.
        /// </summary>
        private void RefreshStressMode()
        {
            if (_manaRateLabel == null) return;

            _manaRateLabel.style.display = stressMode ? DisplayStyle.Flex : DisplayStyle.None;

            if (!stressMode) return;

            _manaRateLabel.text = "MANA/USD …";
            RefreshManaRate();
        }

        /// <summary>
        /// One rate fetch, if Stress Mode is on. Failures are written into the label rather than logged:
        /// this is a morale readout, and a console warning every minute because CoinGecko rate-limited a
        /// joke feature would be worse than the feature is good.
        /// </summary>
        private void RefreshManaRate()
        {
            if (!stressMode || _manaRateLabel == null) return;

            ManaRateService.Fetch(
                usd =>
                {
                    // The window can be closed between request and response — a disposed label would
                    // throw inside the completion callback, where nothing would catch it.
                    if (_manaRateLabel?.panel == null) return;

                    _manaRateLabel.text = $"MANA/USD ${usd:0.0000}";
                    _manaRateLabel.tooltip = $"1 MANA = ${usd:0.0000}\n"
                                             + $"1 USD = {1f / usd:0.00} MANA\n"
                                             + "Refreshes every minute while Stress Mode is on.";
                },
                error =>
                {
                    if (_manaRateLabel?.panel == null) return;

                    _manaRateLabel.text = "MANA/USD —";
                    _manaRateLabel.tooltip = $"Rate unavailable: {error}";
                });
        }

        // ---------------------------------------------------------------- Game overlay suppression

        /// <summary>
        /// Hides the renderer's built-in play-mode overlay (debug panel, zoom, switcher, emote
        /// controls). The loader spinner and the drag surface (the Controls element itself,
        /// which carries the DragManipulator) are left untouched.
        /// </summary>
        private void EnforceCleanGameView()
        {
            if (!Application.isPlaying || !cleanGameView) return;

            var root = FindOverlayRoot();
            if (root == null) return;

            SetOverlayElementVisible(root, "DebugPanel", false);
            SetOverlayElementVisible(root, "ZoomControls", false);
            SetOverlayElementVisible(root, "Switcher", false);
            SetOverlayElementVisible(root, "EmoteControls", false);
        }

        private void RestoreGameOverlay()
        {
            if (!Application.isPlaying) return;

            var root = FindOverlayRoot();
            if (root == null) return;

            // Mirror the presenter: the debug panel is editor-only
            SetOverlayElementVisible(root, "DebugPanel", Application.isEditor);

            // Zoom/switcher/emote visibility is mode-dependent — a reload lets
            // PreviewController re-apply the canonical states
            SendToJSBridge("Reload", autoReload: false);
        }

        private static VisualElement FindOverlayRoot()
        {
            var presenter = FindAnyObjectByType<PreviewUIPresenter>();
            return presenter == null ? null : presenter.GetComponent<UIDocument>()?.rootVisualElement;
        }

        private static void SetOverlayElementVisible(VisualElement root, string name, bool visible)
        {
            var element = root.Q(name);
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------------------------------------------------------------- Toolbar

        private VisualElement BuildToolbar()
        {
            var bar = new Toolbar();

            var envPopup = new PopupField<string>(new List<string> { "prod (org)", "dev (zone)" }, envIndex);
            envPopup.RegisterValueChangedCallback(_ =>
            {
                envIndex = envPopup.index;
                APIService.Environment = envIndex == 1 ? "zone" : "org";
                ResetAndSearch();
            });
            bar.Add(envPopup);

            bar.Add(new ToolbarSpacer { style = { flexGrow = 1 } });

            // Stress Mode's readout. After the spacer, so it rides with the right-hand group immediately
            // left of Clean View rather than drifting as the window resizes. Hidden until the toggle in
            // the Debug tab turns it on (RefreshStressMode).
            _manaRateLabel = new Label
            {
                style =
                {
                    display = DisplayStyle.None,
                    unityTextAlign = TextAnchor.MiddleRight,
                    marginRight = 6,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            bar.Add(_manaRateLabel);

            var cleanViewToggle = new ToolbarToggle { text = "Clean View", value = cleanGameView };
            cleanViewToggle.tooltip = "Hide the renderer's built-in overlay (debug panel, zoom, switcher) in play mode";
            cleanViewToggle.RegisterValueChangedCallback(evt =>
            {
                cleanGameView = evt.newValue;
                if (!cleanGameView) RestoreGameOverlay();
            });
            bar.Add(cleanViewToggle);

            var autoToggle = new ToolbarToggle { text = "Auto apply", value = autoApply };
            autoToggle.RegisterValueChangedCallback(evt => autoApply = evt.newValue);
            bar.Add(autoToggle);

            var applyButton = new ToolbarButton(Apply) { text = "Apply" };
            bar.Add(applyButton);

            var clearButton = new ToolbarButton(() =>
            {
                if (Application.isPlaying) return;
                EditModeAvatarPreview.Clear();
                SetStatus("Edit-mode preview cleared");
            }) { text = "Clear Preview" };
            bar.Add(clearButton);

            _playButton = new Button(EnterPlayAndApply) { text = "▶ Enter Play" };
            _playButton.style.marginLeft = 4;
            bar.Add(_playButton);

            return bar;
        }

        private void UpdatePlayModeUI()
        {
            if (_playButton == null) return;
            _playButton.style.display = Application.isPlaying ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void EnterPlayAndApply()
        {
            if (Application.isPlaying) return;
            applyOnPlay = true;
            EditorApplication.EnterPlaymode();
        }

        // ---------------------------------------------------------------- Browser pane

        private VisualElement BuildBrowserPane()
        {
            var pane = new VisualElement { style = { minWidth = 300 } };

            // Tabs
            var tabs = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4, marginLeft = 4 } };
            var avatarTab = new Button { text = "Avatar" };
            var wearablesTab = new Button { text = "Wearables" };
            var emotesTab = new Button { text = "Emotes / Poses" };
            var debugTab = new Button { text = "Debug" };

            void SelectTab(string tab)
            {
                avatarTab.SetEnabled(tab != "avatar");
                wearablesTab.SetEnabled(tab != "wearable");
                emotesTab.SetEnabled(tab != "emote");
                debugTab.SetEnabled(tab != "debug");

                var isAvatar = tab == "avatar";
                var isDebug = tab == "debug";
                _avatarPane.style.display = isAvatar ? DisplayStyle.Flex : DisplayStyle.None;
                _browserContent.style.display = isAvatar || isDebug ? DisplayStyle.None : DisplayStyle.Flex;
                _debugPane.style.display = isDebug ? DisplayStyle.Flex : DisplayStyle.None;

                if (isAvatar || isDebug) return;

                _query.Category = tab;
                _query.WearableCategory = null;
                _query.EmoteCategory = null;
                ResetAndSearch();
            }

            avatarTab.clicked += () => SelectTab("avatar");
            wearablesTab.clicked += () => SelectTab("wearable");
            emotesTab.clicked += () => SelectTab("emote");
            debugTab.clicked += () => SelectTab("debug");
            wearablesTab.SetEnabled(false); // default active tab
            tabs.Add(avatarTab);
            tabs.Add(wearablesTab);
            tabs.Add(emotesTab);
            tabs.Add(debugTab);
            pane.Add(tabs);

            _avatarPane = BuildAvatarPane();
            _avatarPane.style.display = DisplayStyle.None;
            pane.Add(_avatarPane);

            _browserContent = new VisualElement { style = { flexGrow = 1 } };
            pane.Add(_browserContent);
            _debugPane = BuildDebugPane();
            _debugPane.style.display = DisplayStyle.None;
            pane.Add(_debugPane);

            // Search
            var search = new ToolbarSearchField { style = { marginLeft = 4, marginTop = 4, width = Length.Percent(95) } };
            IVisualElementScheduledItem pendingSearch = null;
            search.RegisterValueChangedCallback(evt =>
            {
                _query.Search = evt.newValue;
                pendingSearch?.Pause();
                pendingSearch = search.schedule.Execute(ResetAndSearch);
                pendingSearch.StartingIn(500);
            });
            _browserContent.Add(search);

            // Filters
            var filters = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginLeft = 4 } };

            var slotPopup = new PopupField<string>("Slot", WEARABLE_SLOTS, 0);
            slotPopup.RegisterValueChangedCallback(_ =>
            {
                if (_query.Category == "emote")
                    _query.EmoteCategory = slotPopup.value == "any" ? null : slotPopup.value;
                else
                    _query.WearableCategory = slotPopup.value == "any" ? null : slotPopup.value;
                ResetAndSearch();
            });
            filters.Add(slotPopup);

            var rarityPopup = new PopupField<string>("Rarity", RARITIES, 0);
            rarityPopup.RegisterValueChangedCallback(_ =>
            {
                _query.Rarity = rarityPopup.value == "any" ? null : rarityPopup.value;
                ResetAndSearch();
            });
            filters.Add(rarityPopup);

            var genderPopup = new PopupField<string>("Body", GENDERS, 0);
            genderPopup.RegisterValueChangedCallback(_ =>
            {
                _query.Gender = genderPopup.value == "any" ? null : genderPopup.value;
                ResetAndSearch();
            });
            filters.Add(genderPopup);

            var sortPopup = new PopupField<string>("Sort", SORT_OPTIONS, 0);
            sortPopup.RegisterValueChangedCallback(_ =>
            {
                _query.SortBy = SORT_API_VALUES[sortPopup.value];
                _displayOffset = 0;
                ApplySortAndRebuild(); // already have every matching item fetched; no need to re-query
            });
            filters.Add(sortPopup);

            var onSaleToggle = new Toggle("On Sale")
            {
                value = _query.IsOnSale,
                tooltip = "Only show items you can currently buy - mintable from their collection or " +
                          "with an open listing - exactly like the web marketplace's \"On Sale\" filter. " +
                          "Off shows everything, on sale or not.",
                style = { marginLeft = 4 }
            };
            var primarySalesToggle = new Toggle("Primary Sales")
            {
                value = _query.OnlyMinting,
                tooltip = "Only show primary sales - items still mintable from the creator's own " +
                          "collection. Every secondary sale (an item only available through someone " +
                          "else's listing) drops out. Implies \"On Sale\", which is switched on and " +
                          "off with it.",
                style = { marginLeft = 4 }
            };

            // The two filters are nested, not independent: a primary sale is always on sale, so the
            // pair is kept consistent instead of allowing the contradictory "Primary Sales on, On Sale
            // off" state. SetValueWithoutNotify on the follower avoids a second redundant re-query.
            onSaleToggle.RegisterValueChangedCallback(evt =>
            {
                _query.IsOnSale = evt.newValue;
                if (!evt.newValue && _query.OnlyMinting)
                {
                    _query.OnlyMinting = false;
                    primarySalesToggle.SetValueWithoutNotify(false);
                }

                ResetAndSearch(); // server-side filter (see CatalogService.BuildUrl), so re-query
            });
            filters.Add(onSaleToggle);

            primarySalesToggle.RegisterValueChangedCallback(evt =>
            {
                _query.OnlyMinting = evt.newValue;
                _query.IsOnSale = evt.newValue;
                onSaleToggle.SetValueWithoutNotify(evt.newValue);
                ResetAndSearch(); // server-side filter too, so re-query
            });
            filters.Add(primarySalesToggle);

            _invertSortButton = new Button(() =>
            {
                _invertSort = !_invertSort;
                UpdateInvertSortButton();
                _displayOffset = 0;
                ApplySortAndRebuild();
            })
            {
                style = { width = 20, marginLeft = 2 }
            };
            UpdateInvertSortButton();
            filters.Add(_invertSortButton);

            // Swap slot filter choices when the tab changes
            wearablesTab.clicked += () => { slotPopup.choices = WEARABLE_SLOTS; slotPopup.index = 0; };
            emotesTab.clicked += () => { slotPopup.choices = EMOTE_CATEGORIES; slotPopup.index = 0; };

            _browserContent.Add(filters);

            // Results grid
            var scroll = new ScrollView { style = { flexGrow = 1 } };
            _grid = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, paddingLeft = 4, paddingTop = 4 }
            };
            scroll.Add(_grid);
            _browserContent.Add(scroll);

            // Pagination
            var pager = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.Center, paddingBottom = 4 } };
            // Paging is purely local: every matching item (up to FETCH_CAP) is already in memory.
            _prevButton = new Button(() => { _displayOffset = Mathf.Max(0, _displayOffset - PAGE_SIZE); RebuildGrid(); }) { text = "◀" };
            _nextButton = new Button(() => { _displayOffset += PAGE_SIZE; RebuildGrid(); }) { text = "▶" };
            _pageLabel = new Label("") { style = { unityTextAlign = TextAnchor.MiddleCenter, marginLeft = 8, marginRight = 8 } };
            pager.Add(_prevButton);
            pager.Add(_pageLabel);
            pager.Add(_nextButton);
            _browserContent.Add(pager);

            return pane;
        }

        // ---------------------------------------------------------------- Avatar pane

        /// <summary>
        /// Body shape, colors and face features (eyes/eyebrows/mouth/hair/facial_hair) in one
        /// place, mirroring the marketplace's own avatar editor. Everything here writes straight to
        /// <c>outfit</c>, so all of it is carried by share codes and saved presets — face features
        /// are equipped into <c>outfit.urns</c> exactly like a wearable picked from the browser.
        /// </summary>
        private VisualElement BuildAvatarPane()
        {
            var pane = new ScrollView { style = { flexGrow = 1, paddingLeft = 6, paddingRight = 6, paddingTop = 4 } };

            pane.Add(Header("Body"));

            _bodyShapePopup = new PopupField<string>("Body shape", new List<string> { "Male", "Female" },
                outfit.bodyShape == WearablesConstants.BODY_SHAPE_FEMALE ? 1 : 0);
            _bodyShapePopup.RegisterValueChangedCallback(_ =>
            {
                outfit.bodyShape = _bodyShapePopup.index == 1
                    ? WearablesConstants.BODY_SHAPE_FEMALE
                    : WearablesConstants.BODY_SHAPE_MALE;
                RefreshFaceGrid(); // face options are body-shape specific (male/female variants)
                RefreshShareCode();
                ScheduleApply();
            });
            pane.Add(_bodyShapePopup);

            pane.Add(Header("Colors"));
            _skinField = ColorRow(pane, "Skin", outfit.skinColor, c => outfit.skinColor = c);
            _hairField = ColorRow(pane, "Hair", outfit.hairColor, c => outfit.hairColor = c);
            _eyeField = ColorRow(pane, "Eyes", outfit.eyeColor, c => outfit.eyeColor = c);

            pane.Add(Header("Face Features"));
            pane.Add(new Label("Saved in presets and share codes, and listed in the Outfit pane, "
                               + "same as any other wearable.")
            {
                style =
                {
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Italic,
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 4
                }
            });

            var categoryRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            _faceCategoryButtons = new Button[FACE_SLOTS.Count];
            for (var i = 0; i < FACE_SLOTS.Count; i++)
            {
                var slot = FACE_SLOTS[i];
                var button = new Button(() => SelectFaceCategory(slot))
                {
                    text = FACE_SLOT_LABELS[slot],
                    style = { marginRight = 2, marginBottom = 2 }
                };
                _faceCategoryButtons[i] = button;
                categoryRow.Add(button);
            }
            pane.Add(categoryRow);

            pane.Add(new Button(() =>
            {
                var equipped = EquippedUrnForSlot(_faceCategory);
                if (equipped == null)
                {
                    SetStatus($"No {FACE_SLOT_LABELS[_faceCategory]} equipped");
                    return;
                }

                outfit.urns.Remove(equipped);
                RefreshFaceGrid();
                RefreshSlots();
                RefreshShareCode();
                ScheduleApply();
                SetStatus($"Cleared {FACE_SLOT_LABELS[_faceCategory]}");
            }) { text = "Clear selection", style = { marginTop = 2, marginBottom = 4 } });

            // No nested ScrollView here: the pane itself already scrolls, and a ScrollView inside a
            // ScrollView left the outer one unable to size to its content, clipping the bottom of the
            // panel instead of scrolling to it.
            _faceGrid = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, paddingTop = 4 }
            };
            pane.Add(_faceGrid);

            UpdateFaceCategoryButtons();
            RunFaceSearch();

            return pane;
        }

        private void SelectFaceCategory(string slot)
        {
            _faceCategory = slot;
            UpdateFaceCategoryButtons();
            RunFaceSearch();
        }

        private void UpdateFaceCategoryButtons()
        {
            for (var i = 0; i < FACE_SLOTS.Count; i++)
                _faceCategoryButtons[i].SetEnabled(FACE_SLOTS[i] != _faceCategory);
        }

        /// <summary>
        /// Resolves the current category's curated URNs via the Catalyst entities endpoint (the
        /// only source that can serve off-chain base-avatar items — see DEFAULT_FACE_URNS). Async
        /// void, same pattern EditModeAvatarPreview.Apply already uses for editor-only await chains.
        /// </summary>
        private async void RunFaceSearch()
        {
            if (_faceGrid == null) return;

            var sequence = ++_faceSearchSequence;
            SetStatus($"Loading {FACE_SLOT_LABELS[_faceCategory]}...");

            EntityDefinition[] entities;
            try
            {
                entities = await EntityService.GetEntities((string[])DEFAULT_FACE_URNS[_faceCategory].Clone());
            }
            catch (Exception e)
            {
                if (sequence == _faceSearchSequence)
                    SetStatus($"Failed to load {FACE_SLOT_LABELS[_faceCategory]}: {e.Message}", true);
                return;
            }

            if (sequence != _faceSearchSequence) return;

            RegisterCatalystEntities(entities);

            _faceEntities = entities;
            RefreshFaceGrid();
            RefreshSlots(); // rows for already-equipped face items can now resolve name/thumbnail/slot
            SetStatus($"{_faceEntities.Length} {FACE_SLOT_LABELS[_faceCategory]} options");
        }

        /// <summary>
        /// Registers Catalyst-resolved entities in <see cref="_knownItems"/> as synthetic
        /// <see cref="CatalogItem"/>s. Base avatars are off-chain, so the marketplace API can never
        /// resolve them (<see cref="HydrateKnownItems"/> routes them through the Catalyst instead) —
        /// yet everything downstream reads slot, name, thumbnail and body-shape support off
        /// <see cref="_knownItems"/>: the one-per-slot rule (<see cref="OnFaceFeatureClicked"/>,
        /// <see cref="OnItemClicked"/>), the Outfit pane rows (<see cref="RefreshSlots"/>), and the
        /// representation guard that keeps <c>GLTFLoader.LoadModel</c> from throwing on a body shape
        /// the item has no representation for (<see cref="FilterForBodyShape"/>). Registering them
        /// here is what lets face features be ordinary <c>outfit.urns</c> entries with no
        /// special-casing anywhere else.
        /// </summary>
        private void RegisterCatalystEntities(IEnumerable<EntityDefinition> entities)
        {
            foreach (var entity in entities)
            {
                // Same "BaseMale"/"BaseFemale" spelling the marketplace payload uses, since
                // FilterForBodyShape compares against these strings.
                var bodyShapes = new List<string>(2);
                if (entity.HasRepresentation(BodyShape.Male)) bodyShapes.Add("BaseMale");
                if (entity.HasRepresentation(BodyShape.Female)) bodyShapes.Add("BaseFemale");

                _knownItems[entity.URN] = new CatalogItem
                {
                    urn = entity.URN,
                    name = FriendlyName(entity.URN), // entities carry no display name, only a thumbnail
                    thumbnail = entity.Thumbnail,
                    category = "wearable",
                    data = new CatalogItem.ItemData
                    {
                        wearable = new CatalogItem.WearableData
                        {
                            category = entity.Category,
                            bodyShapes = bodyShapes.ToArray()
                        }
                    }
                };
            }
        }

        /// <summary>
        /// The equipped URN occupying a slot, or null. Resolved through <see cref="_knownItems"/>, so
        /// it only sees items we have catalog/entity info for — the same limitation the one-per-slot
        /// rule has. Takes the last match because the renderer's own dedup is last-in-list-wins.
        /// </summary>
        private string EquippedUrnForSlot(string slot) =>
            outfit.urns.LastOrDefault(urn =>
                _knownItems.TryGetValue(urn, out var known) && known.Slot == slot);

        private BodyShape CurrentBodyShape() =>
            outfit.bodyShape.Equals(WearablesConstants.BODY_SHAPE_FEMALE, StringComparison.OrdinalIgnoreCase)
                ? BodyShape.Female
                : BodyShape.Male;

        private void RefreshFaceGrid()
        {
            if (_faceGrid == null) return;

            _faceGrid.Clear();

            var bodyShape = CurrentBodyShape();
            var slot = _faceCategory;
            var selectedUrn = EquippedUrnForSlot(slot);

            // Only options with a representation for the currently-selected body shape are shown —
            // this list mixes male and female-specific variants (the "f_"-prefixed URNs), and picking
            // one without a matching representation would just get silently skipped at apply time.
            foreach (var entity in _faceEntities.Where(e => e.HasRepresentation(bodyShape)))
            {
                _faceGrid.Add(BuildFaceTile(entity, slot, entity.URN == selectedUrn));
            }
        }

        private VisualElement BuildFaceTile(EntityDefinition entity, string slot, bool selected)
        {
            var label = FriendlyName(entity.URN);

            var tile = new VisualElement
            {
                tooltip = label,
                style =
                {
                    width = THUMB_SIZE + 8,
                    marginRight = 4,
                    marginBottom = 4,
                    paddingTop = 4,
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingBottom = 2,
                    backgroundColor = new Color(0, 0, 0, 0.25f)
                }
            };

            if (selected)
            {
                tile.style.borderTopWidth = tile.style.borderBottomWidth =
                    tile.style.borderLeftWidth = tile.style.borderRightWidth = 2;
                tile.style.borderTopColor = tile.style.borderBottomColor =
                    tile.style.borderLeftColor = tile.style.borderRightColor = Color.white;
            }

            var image = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = THUMB_SIZE, height = THUMB_SIZE }
            };
            tile.Add(image);

            var nameLabel = new Label(label)
            {
                style =
                {
                    fontSize = 10,
                    overflow = Overflow.Hidden,
                    whiteSpace = WhiteSpace.NoWrap,
                    textOverflow = TextOverflow.Ellipsis,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            tile.Add(nameLabel);

            // Entities don't carry a display name (unlike marketplace CatalogItems) — only a
            // thumbnail, same as WearableItemElement (the in-game Configurator's own tile).
            LoadThumbnail(entity.Thumbnail, tex =>
            {
                if (tex != null) image.image = tex;
            });

            tile.RegisterCallback<ClickEvent>(_ => OnFaceFeatureClicked(entity, slot));

            return tile;
        }

        private static string FriendlyName(string urn)
        {
            var suffix = urn[(urn.LastIndexOf(':') + 1)..];
            return string.Join(' ', suffix.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
        }

        /// <summary>
        /// Equips a face feature. Deliberately the same path an ordinary browser pick takes
        /// (<see cref="OnItemClicked"/>): one item per slot, appended last so it wins the renderer's
        /// last-in-list-wins dedup against any same-slot URN we couldn't resolve. <paramref name="slot"/>
        /// comes from the grid that built the tile rather than <c>_faceCategory</c>, so a click can't
        /// land in the wrong slot if the category changed while thumbnails were still loading.
        /// </summary>
        private void OnFaceFeatureClicked(EntityDefinition entity, string slot)
        {
            outfit.urns.RemoveAll(urn =>
                _knownItems.TryGetValue(urn, out var known) && known.Slot == slot);
            outfit.urns.Remove(entity.URN);
            outfit.urns.Add(entity.URN);

            RefreshFaceGrid();
            RefreshShareCode();

            // Same rule as an ordinary browser pick: equips, and takes over the isolation if one is active
            if (outfit.soloItem)
            {
                IsolateItem(entity.URN, null, slot, FriendlyName(entity.URN));
                return;
            }

            RefreshSlots();
            ScheduleApply();
            SetStatus($"Equipped {FriendlyName(entity.URN)} ({slot})");
        }

        /// <summary>
        /// Replicates the renderer's built-in play-mode debug overlay (PreviewUIPresenter's
        /// DebugPanel) so it can live in the window instead of covering the Game view.
        /// </summary>
        private VisualElement BuildDebugPane()
        {
            var pane = new ScrollView { style = { flexGrow = 1, paddingLeft = 6, paddingRight = 6, paddingTop = 4 } };

            pane.Add(new Label($"Renderer version: {Application.version}")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 }
            });

            pane.Add(new Label("These actions drive the play-mode renderer via JSBridge — enter play mode to use them.")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginBottom = 6 }
            });

            // --- JSBridge invoke (mirrors MethodNameDropdown/Parameter/InvokeButton)
            pane.Add(Header("Invoke JSBridge method"));

            var methodNames = typeof(JSBridge)
                .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
                .Select(m => m.Name)
                .ToList();

            var methodPopup = new PopupField<string>("Method", methodNames, 0);
            pane.Add(methodPopup);

            var parameterField = new TextField("Parameter");
            pane.Add(parameterField);

            pane.Add(new Button(() =>
            {
                SendToJSBridge(methodPopup.value, parameterField.value);
                SetStatus($"Invoked {methodPopup.value}");
            }) { text = "Invoke" });

            // --- URL presets (mirrors URLDropdown; same list, hoisted to a shared static)
            pane.Add(Header("Load from URL preset"));

            var presets = PreviewUIPresenter.DEBUG_URL_PRESETS;
            var presetPopup = new PopupField<string>(presets.Select(p => p.name).ToList(), 0);
            presetPopup.RegisterValueChangedCallback(evt =>
            {
                var selected = presets.Find(p => p.name == evt.newValue);
                if (selected.url == null) return;

                SendToJSBridge("ParseFromString", selected.url);
                SetStatus($"Loaded preset: {selected.name}");
            });
            pane.Add(presetPopup);

            // --- Outline debug. SMAA erodes the outline's thin stroke toward whatever's behind it, so
            // it picks up the card background. Outline width/color are shader-tuning knobs (Capture
            // pane); widen the stroke there so it survives AA. This selector A/Bs the camera AA live.
            pane.Add(Header("Outline Debug"));
            pane.Add(new Label("Antialiasing override — play mode only, not persisted. " +
                               "Outline width and color are under Shader Tuning.")
            {
                style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Italic, whiteSpace = WhiteSpace.Normal, marginBottom = 4 }
            });

            var aaOptions = new List<string> { "Scene Default", "None", "FXAA", "SMAA", "TAA" };
            var aaPopup = new PopupField<string>("Anti-aliasing", aaOptions, aaOptions.IndexOf("SMAA"))
            {
                tooltip = "Scene Default is SMAA — the likely source of the outline being tinted by the card background."
            };
            StudioCardFrame.DebugAntialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            aaPopup.RegisterValueChangedCallback(evt =>
            {
                StudioCardFrame.DebugAntialiasing = evt.newValue switch
                {
                    "None" => AntialiasingMode.None,
                    "FXAA" => AntialiasingMode.FastApproximateAntialiasing,
                    "SMAA" => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                    "TAA" => AntialiasingMode.TemporalAntiAliasing,
                    _ => (AntialiasingMode?)null
                };
                SetStatus(Application.isPlaying
                    ? $"Antialiasing set to {evt.newValue}"
                    : "Antialiasing override will apply once you're in play mode", false);
            });
            pane.Add(aaPopup);

            // --- Misc debug actions
            pane.Add(Header("Actions"));

            var actionsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };

            actionsRow.Add(new Button(() =>
            {
                var config = PreviewConfiguration.Instance.ToString();
                Debug.Log(config);
                _configField.value = config;
            }) { text = "Print Config" });

            actionsRow.Add(new Button(() =>
            {
                SendToJSBridge("SetProfile", $"default{UnityEngine.Random.Range(1, 160)}");
                SetStatus("Loading random profile...");
            }) { text = "Random Profile" });

            actionsRow.Add(new Button(() => WithCamera(c => c.ZoomIn())) { text = "Zoom In" });
            actionsRow.Add(new Button(() => WithCamera(c => c.ZoomOut())) { text = "Zoom Out" });

            var stressToggle = new Toggle("Stress Mode")
            {
                value = stressMode,
                // Centred so the checkbox lines up with the button row it sits in rather than hugging the
                // top; the label is left short so it doesn't push the row into a second line on a narrow
                // window (the row wraps).
                style = { marginLeft = 6, alignSelf = Align.Center },
                tooltip = "Show the live MANA/USD rate in the toolbar, refreshed every minute. "
                          + "Purely for morale."
            };
            stressToggle.RegisterValueChangedCallback(evt =>
            {
                stressMode = evt.newValue;
                RefreshStressMode();
            });
            actionsRow.Add(stressToggle);

            pane.Add(actionsRow);

            // Fly Camera used to sit here; it moved to the outfit pane's "Scene and Camera settings"
            // foldout (BuildSceneAndCamera), where it's next to the lights it's composed against.
            //
            // _configField stays: it's the Print Config button's output, not part of that move.
            _configField = new TextField { multiline = true, isReadOnly = true };
            _configField.style.whiteSpace = WhiteSpace.Normal;
            _configField.style.marginTop = 4;
            pane.Add(_configField);

            // --- Load from Collection (draft UUID via signed builder-api, or published 0x contract)
            pane.Add(Header("Load from Collection"));

            _identityStatusLabel = new Label { style = { whiteSpace = WhiteSpace.Normal } };
            pane.Add(_identityStatusLabel);

            var identityField = new TextField("Identity JSON") { isPasswordField = true };
            identityField.tooltip = "Paste your Decentraland identity from builder.decentraland.org " +
                                    "(devtools > Application > Local Storage). Stored in EditorPrefs only.";
            pane.Add(identityField);

            var identityButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            identityButtons.Add(new Button(() =>
            {
                try
                {
                    _identity = BuilderIdentity.Parse(identityField.value);
                    _identity.Save();
                    identityField.value = string.Empty;
                    RefreshIdentityStatus();
                    SetStatus("Identity saved");
                }
                catch (Exception e)
                {
                    SetStatus($"Invalid identity: {e.Message}", true);
                }
            }) { text = "Save Identity" });
            identityButtons.Add(new Button(() =>
            {
                BuilderIdentity.Clear();
                _identity = null;
                RefreshIdentityStatus();
                SetStatus("Identity cleared");
            }) { text = "Clear" });
            pane.Add(identityButtons);

            var collectionRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            _collectionIdField = new TextField("Collection ID") { style = { flexGrow = 1 } };
            _collectionIdField.tooltip = "Draft collection UUID (needs identity) or published 0x contract address";
            collectionRow.Add(_collectionIdField);
            collectionRow.Add(new Button(LoadCollection) { text = "Load" });
            pane.Add(collectionRow);

            _collectionGrid = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 4 }
            };
            pane.Add(_collectionGrid);

            var collectionPager = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.Center, marginTop = 2 }
            };
            _collectionPrevButton = new Button(() => { _collectionSkip = Mathf.Max(0, _collectionSkip - PAGE_SIZE); ShowCollectionPage(); }) { text = "◀" };
            _collectionNextButton = new Button(() => { _collectionSkip += PAGE_SIZE; ShowCollectionPage(); }) { text = "▶" };
            _collectionPageLabel = new Label("") { style = { unityTextAlign = TextAnchor.MiddleCenter, marginLeft = 8, marginRight = 8 } };
            collectionPager.Add(_collectionPrevButton);
            collectionPager.Add(_collectionPageLabel);
            collectionPager.Add(_collectionNextButton);
            collectionPager.style.display = DisplayStyle.None;
            _collectionPager = collectionPager;
            pane.Add(collectionPager);

            RefreshIdentityStatus();

            return pane;
        }

        // ---------------------------------------------------------------- Load from Collection

        private BuilderIdentity _identity;
        private Label _identityStatusLabel;
        private TextField _collectionIdField;
        private VisualElement _collectionGrid;
        private VisualElement _collectionPager;
        private Button _collectionPrevButton, _collectionNextButton;
        private Label _collectionPageLabel;
        private List<BuilderCollectionService.DraftItem> _draftItems;
        private int _collectionSkip;

        private void RefreshIdentityStatus()
        {
            _identity ??= BuilderIdentity.Load();

            if (_identityStatusLabel == null) return;

            if (_identity == null)
            {
                _identityStatusLabel.text = "No identity saved — needed for draft (UUID) collections only.";
            }
            else
            {
                var state = _identity.IsExpired ? "EXPIRED" : "valid";
                _identityStatusLabel.text = $"Identity: {_identity.WalletAddress} — {state} until {_identity.Expiration:yyyy-MM-dd}";
            }
        }

        private void LoadCollection()
        {
            var id = _collectionIdField.value?.Trim();

            if (string.IsNullOrEmpty(id))
            {
                SetStatus("Enter a collection ID", true);
                return;
            }

            SetStatus("Loading collection...");
            _draftItems = null;
            _collectionSkip = 0;

            if (id.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                // Published collection: unauthenticated marketplace catalog, server-paged
                LoadPublishedCollectionPage(id);
            }
            else
            {
                RefreshIdentityStatus();
                BuilderCollectionService.LoadDraftCollection(id, _identity,
                    items =>
                    {
                        _draftItems = items;
                        ShowCollectionPage();
                        SetStatus($"Collection loaded: {items.Count} items");
                    },
                    error => SetStatus(error, true));
            }
        }

        private void LoadPublishedCollectionPage(string contractAddress)
        {
            var query = new CatalogQuery
            {
                ContractAddress = contractAddress,
                Category = null, // collections can mix wearables and emotes
                First = PAGE_SIZE,
                Skip = _collectionSkip
            };

            CatalogService.Search(query,
                page =>
                {
                    _collectionGrid.Clear();
                    foreach (var item in page.data)
                    {
                        _collectionGrid.Add(BuildTile(item, OnItemClicked)); // published items equip via the normal URN flow
                    }

                    var from = _collectionSkip + 1;
                    var to = _collectionSkip + page.data.Length;
                    _collectionPageLabel.text = page.total > 0 ? $"{from}–{to} of {page.total}" : "no items";
                    _collectionPrevButton.SetEnabled(_collectionSkip > 0);
                    _collectionNextButton.SetEnabled(to < page.total);
                    _collectionPager.style.display = DisplayStyle.Flex;
                    SetStatus($"Collection loaded: {page.total} items");
                },
                error => SetStatus($"Catalog error: {error}", true));
        }

        private void ShowCollectionPage()
        {
            // Published (0x) collections page server-side
            if (_draftItems == null)
            {
                var id = _collectionIdField.value?.Trim();
                if (!string.IsNullOrEmpty(id) && id.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    LoadPublishedCollectionPage(id);
                return;
            }

            _collectionGrid.Clear();

            foreach (var item in _draftItems.Skip(_collectionSkip).Take(PAGE_SIZE))
            {
                _collectionGrid.Add(BuildDraftTile(item));
            }

            var from = _collectionSkip + 1;
            var to = Mathf.Min(_collectionSkip + PAGE_SIZE, _draftItems.Count);
            _collectionPageLabel.text = _draftItems.Count > 0 ? $"{from}–{to} of {_draftItems.Count}" : "no items";
            _collectionPrevButton.SetEnabled(_collectionSkip > 0);
            _collectionNextButton.SetEnabled(to < _draftItems.Count);
            _collectionPager.style.display = DisplayStyle.Flex;
        }

        private VisualElement BuildDraftTile(BuilderCollectionService.DraftItem item)
        {
            var tile = new VisualElement
            {
                tooltip = $"{item.Name}\n{item.Rarity} · {item.Category} · {item.Type} (draft)",
                style =
                {
                    width = THUMB_SIZE + 8,
                    marginRight = 4,
                    marginBottom = 4,
                    paddingTop = 4,
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingBottom = 2,
                    backgroundColor = new Color(0, 0, 0, 0.25f),
                    borderBottomWidth = 3,
                    borderBottomColor = RARITY_COLORS.GetValueOrDefault(item.Rarity ?? "", Color.gray)
                }
            };

            var image = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = THUMB_SIZE, height = THUMB_SIZE }
            };
            tile.Add(image);

            tile.Add(new Label(item.Name)
            {
                style =
                {
                    fontSize = 10,
                    overflow = Overflow.Hidden,
                    whiteSpace = WhiteSpace.NoWrap,
                    textOverflow = TextOverflow.Ellipsis,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            });

            LoadThumbnail(item.ThumbnailUrl, tex =>
            {
                if (tex != null) image.image = tex;
            });

            tile.RegisterCallback<ClickEvent>(_ => EquipDraft(item));

            return tile;
        }

        private void EquipDraft(BuilderCollectionService.DraftItem item)
        {
            if (item.Type == "emote")
            {
                RemoveDraftEmote();
                outfit.base64Items.Add(item.Base64Entity);
                outfit.emote = "idle"; // the base64 emote takes pose priority in builder mode
                _poseLabel.text = $"Pose: {item.Name} (draft)";
                SyncEmotePopup();
                SetStatus($"Pose set: {item.Name} (draft, play mode only)");
            }
            else
            {
                // One item per slot: displace both draft and catalog occupants of this category
                outfit.base64Items.RemoveAll(base64 =>
                {
                    var (_, category, isEmote) = DescribeDraft(base64);
                    return !isEmote && category == item.Category;
                });
                outfit.urns.RemoveAll(urn =>
                    _knownItems.TryGetValue(urn, out var known) && known.Slot == item.Category);

                outfit.base64Items.Add(item.Base64Entity);

                // Same rule as a catalog pick: equips, and takes over the isolation if one is active.
                // Returns early, so it repeats the shared tail's RefreshShareCode — the equip above changed
                // the outfit either way — and skips its ScheduleApply, which IsolateItem does itself.
                if (outfit.soloItem)
                {
                    RefreshShareCode();
                    IsolateItem(null, item.Base64Entity, item.Category, $"{item.Name} (draft)");
                    return;
                }

                SetStatus($"Equipped {item.Name} ({item.Category}, draft)");
                RefreshSlots();
            }

            RefreshShareCode();
            ScheduleApply();
        }

        /// <summary>
        /// Same invocation contract as the overlay's Invoke button: SendMessage to the JSBridge
        /// GameObject, then auto-Reload unless the method manages loading itself.
        /// </summary>
        private void SendToJSBridge(string method, string parameter = null, bool autoReload = true)
        {
            if (!Application.isPlaying)
            {
                SetStatus("Enter play mode first", true);
                return;
            }

            var bridge = GameObject.Find("JSBridge");
            if (bridge == null)
            {
                SetStatus("JSBridge object not found in the scene", true);
                return;
            }

            if (string.IsNullOrEmpty(parameter))
                bridge.SendMessage(method);
            else
                bridge.SendMessage(method, parameter);

            if (autoReload && method != "Reload" && method != "TakeScreenshot" && method != "Cleanup")
            {
                bridge.SendMessage("Reload");
            }
        }

        private void WithCamera(Action<PreviewCameraController> action)
        {
            if (!Application.isPlaying)
            {
                SetStatus("Enter play mode first", true);
                return;
            }

            var cameraController = FindAnyObjectByType<PreviewCameraController>();
            if (cameraController != null) action(cameraController);
        }

        private void ResetAndSearch()
        {
            _displayOffset = 0;
            RunSearch();
        }

        private void RunSearch()
        {
            if (_grid == null) return;

            SetStatus("Searching catalog...");

            var sequence = ++_searchSequence; // guard against out-of-order responses

            void OnError(string error)
            {
                if (sequence != _searchSequence) return;
                SetStatus($"Catalog error: {error}", true);
            }

            CatalogService.SearchAll(_query, FETCH_CAP,
                (items, total) =>
                {
                    if (sequence != _searchSequence) return;

                    if (string.IsNullOrEmpty(_query.Search))
                    {
                        _fetchedItems = items;
                        _fetchedTotal = total;
                        ApplySortAndRebuild();
                        return;
                    }

                    AugmentWithTagMatches(items, sequence, OnError);
                },
                OnError);
        }

        /// <summary>
        /// marketplace-api's own <c>search</c> param (already applied by the caller's CatalogQuery)
        /// only matches item name/description, with no concept of tags - so a query like "jacket"
        /// misses an item named "Black Jacket" that's tagged "Jacket" but doesn't say so in its name.
        /// The catalyst lambdas endpoint (<see cref="CatalystTextSearchService"/>) indexes tags, so
        /// it's used here purely to find items marketplace-api's name search missed. Those extra
        /// items are built directly from the lambdas payload (name/thumbnail/rarity/slot/bodyShapes)
        /// rather than hydrated through marketplace-api's URN lookup - that lookup only resolves
        /// collections-v2 (Polygon) URNs and silently returns nothing for legacy collections-v1
        /// (Ethereum) items, which are exactly the kind of older item this tag search tends to
        /// surface. The current slot/rarity/gender filters are re-applied to the extras client-side,
        /// since they only ever went through the lambdas query, not marketplace-api's own filtering.
        /// </summary>
        private void AugmentWithTagMatches(CatalogItem[] nameMatches, int sequence, Action<string> onError)
        {
            CatalystTextSearchService.SearchItems(_query.Category, _query.Search, TAG_SEARCH_CAP,
                tagMatches =>
                {
                    if (sequence != _searchSequence) return;

                    var knownUrns = nameMatches.Select(i => i.urn).ToHashSet();
                    var extras = tagMatches.Where(i => !knownUrns.Contains(i.urn) && MatchesActiveFilters(i));

                    var merged = nameMatches.Concat(extras).ToArray();
                    _fetchedItems = merged;
                    _fetchedTotal = merged.Length;
                    ApplySortAndRebuild();
                },
                onError);
        }

        /// <summary>
        /// Re-applies the slot/rarity/gender/on-sale filters an ordinary marketplace-api browse would
        /// already have enforced server-side (see CatalogService.BuildUrl) - needed only for tag-matched
        /// items built from the lambdas payload, which was never filtered by any of these. Gender is
        /// approximated from bodyShapes (matches the live API's own observed behavior: "male"/"female"
        /// match items serving that shape at all, "unisex" requires both).
        /// </summary>
        private bool MatchesActiveFilters(CatalogItem item)
        {
            var wearableSlot = _query.Category == "emote" ? _query.EmoteCategory : _query.WearableCategory;
            if (!string.IsNullOrEmpty(wearableSlot) && item.Slot != wearableSlot) return false;

            if (!string.IsNullOrEmpty(_query.Rarity) && item.rarity != _query.Rarity) return false;

            // The lambdas payload carries no price or listing data at all, so a tag-only match can
            // never be shown to be on sale - with the toggle on, those extras drop out and the results
            // narrow to what marketplace-api itself filtered.
            if (_query.IsOnSale && !item.IsBuyable) return false;

            // Same reasoning, one step narrower: isOnSale on its own is exactly "mintable from the
            // creator's collection", which is what onlyMinting selects server-side.
            if (_query.OnlyMinting && !item.isOnSale) return false;

            if (!string.IsNullOrEmpty(_query.Gender))
            {
                var bodyShapes = item.data?.wearable?.bodyShapes ?? item.data?.emote?.bodyShapes;
                var hasMale = bodyShapes?.Contains("BaseMale") ?? false;
                var hasFemale = bodyShapes?.Contains("BaseFemale") ?? false;
                var matchesGender = _query.Gender switch
                {
                    "male" => hasMale,
                    "female" => hasFemale,
                    "unisex" => hasMale && hasFemale,
                    _ => true
                };
                if (!matchesGender) return false;
            }

            return true;
        }

        private void UpdateInvertSortButton()
        {
            _invertSortButton.text = _invertSort ? "↑" : "↓";
            _invertSortButton.tooltip = _invertSort
                ? "Sort direction inverted (e.g. Newest shows oldest first) - click to restore"
                : "Click to invert sort direction (e.g. Newest → oldest first)";
        }

        private void ApplySortAndRebuild()
        {
            _sortedResults = SortForDisplay(_fetchedItems, _query.SortBy, _invertSort).ToArray();
            RebuildGrid();

            var capped = _fetchedTotal > _sortedResults.Length;
            SetStatus(capped
                ? $"{_sortedResults.Length} of {_fetchedTotal} items (sort limited to the first {FETCH_CAP})"
                : $"{_sortedResults.Length} items");
        }

        private void RebuildGrid()
        {
            _grid.Clear();

            foreach (var item in _sortedResults.Skip(_displayOffset).Take(PAGE_SIZE))
            {
                _grid.Add(BuildTile(item, OnItemClicked));
            }

            var shown = Mathf.Clamp(_sortedResults.Length - _displayOffset, 0, PAGE_SIZE);
            var from = shown > 0 ? _displayOffset + 1 : 0;
            var to = _displayOffset + shown;
            _pageLabel.text = _sortedResults.Length > 0 ? $"{from}–{to} of {_sortedResults.Length}" : "no results";
            _prevButton.SetEnabled(_displayOffset > 0);
            _nextButton.SetEnabled(to < _sortedResults.Length);
        }

        /// <summary>
        /// The live marketplace-api ignores <c>sortBy</c> entirely (verified: newest, recently_listed,
        /// recently_sold, cheapest and most_expensive all return items in the exact same server order,
        /// prices/dates included) — so this sorts client-side instead, over every item matching the
        /// current filters (fetched up to FETCH_CAP by <see cref="RunSearch"/>), not just one page.
        /// Values match the real marketplace sortBy enum; "name" is local-only.
        ///
        /// <paramref name="invert"/> flips the natural direction of whichever option is selected (e.g.
        /// "Newest" + invert shows the oldest items first) — the only way to reach the tail of a sort,
        /// since the marketplace itself has no "oldest"/"least expensive"-style option of its own.
        /// Items lacking the relevant value (not on sale, never sold) always trail last regardless of
        /// direction, so inverting "Cheapest" doesn't flood the top with unpriced items.
        /// </summary>
        private static IEnumerable<CatalogItem> SortForDisplay(CatalogItem[] items, string sortBy, bool invert) =>
            sortBy switch
            {
                "name" => invert
                    ? items.OrderByDescending(i => i.name, StringComparer.OrdinalIgnoreCase)
                    : items.OrderBy(i => i.name, StringComparer.OrdinalIgnoreCase),
                "cheapest" => OrderByPrice(items, descending: invert),
                "most_expensive" => OrderByPrice(items, descending: !invert),
                "recently_listed" => OrderByTimestamp(items, i => i.updatedAt, descending: !invert),
                "recently_sold" => OrderByTimestamp(items, i => i.soldAt, descending: !invert),
                _ => OrderByTimestamp(items, i => i.createdAt, descending: !invert), // "newest"
            };

        /// <summary>
        /// Sorts on <c>minPrice</c> (the cheapest way to actually acquire the item - its mint price or
        /// its lowest open listing, whichever is lower), which is what the marketplace's own
        /// cheapest/most-expensive options rank by. Items that aren't buyable at all carry 2^256-1
        /// there rather than a real price, so they're excluded from the ranking and trail last instead.
        /// </summary>
        private static IEnumerable<CatalogItem> OrderByPrice(CatalogItem[] items, bool descending)
        {
            double Price(CatalogItem i) => double.TryParse(i.minPrice, out var price) ? price : 0;

            var buyable = items.Where(i => i.IsBuyable);
            var priced = descending ? buyable.OrderByDescending(Price) : buyable.OrderBy(Price);
            return priced.Concat(items.Where(i => !i.IsBuyable));
        }

        private static IEnumerable<CatalogItem> OrderByTimestamp(CatalogItem[] items,
            Func<CatalogItem, long> selector, bool descending)
        {
            var withValue = items.Where(i => selector(i) > 0);
            var ordered = descending
                ? withValue.OrderByDescending(selector)
                : withValue.OrderBy(selector);
            return ordered.Concat(items.Where(i => selector(i) <= 0));
        }

        private VisualElement BuildTile(CatalogItem item, Action<CatalogItem> onClick)
        {
            var tile = new VisualElement
            {
                tooltip = $"{item.name}\n{item.rarity} · {item.Slot}",
                style =
                {
                    width = THUMB_SIZE + 8,
                    marginRight = 4,
                    marginBottom = 4,
                    paddingTop = 4,
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingBottom = 2,
                    backgroundColor = new Color(0, 0, 0, 0.25f),
                    borderBottomWidth = 3,
                    borderBottomColor = RARITY_COLORS.GetValueOrDefault(item.rarity ?? "", Color.gray)
                }
            };

            var image = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = THUMB_SIZE, height = THUMB_SIZE }
            };
            tile.Add(image);

            var label = new Label(item.name)
            {
                style =
                {
                    fontSize = 10,
                    overflow = Overflow.Hidden,
                    whiteSpace = WhiteSpace.NoWrap,
                    textOverflow = TextOverflow.Ellipsis,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            tile.Add(label);

            // No panel-attachment check: cached textures invoke the callback synchronously,
            // before the tile is added to the grid, and setting .image while detached is fine
            LoadThumbnail(item.thumbnail, tex =>
            {
                if (tex != null) image.image = tex;
            });

            tile.RegisterCallback<ClickEvent>(_ => onClick(item));

            return tile;
        }

        private static void LoadThumbnail(string url, Action<Texture2D> callback)
        {
            if (string.IsNullOrEmpty(url))
            {
                callback(null);
                return;
            }

            if (THUMBNAIL_CACHE.TryGetValue(url, out var cached))
            {
                // Unity-null means the texture was destroyed since caching — re-download
                if (cached != null)
                {
                    callback(cached);
                    return;
                }

                THUMBNAIL_CACHE.Remove(url);
            }

            var request = UnityWebRequestTexture.GetTexture(url);
            var operation = request.SendWebRequest();
            THUMBNAILS_IN_FLIGHT.Add(url);

            operation.completed += _ =>
            {
                THUMBNAILS_IN_FLIGHT.Remove(url);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                    texture.hideFlags = HideFlags.HideAndDontSave;
                    THUMBNAIL_CACHE[url] = texture;
                    callback(texture);
                }
                else
                {
                    callback(null);
                }

                request.Dispose();
            };
        }

        private void OnItemClicked(CatalogItem item)
        {
            _knownItems[item.urn] = item;

            if (item.category == "emote")
            {
                outfit.emote = item.urn;
                RemoveDraftEmote();
                _poseLabel.text = $"Pose: {item.name}";
                SyncEmotePopup();
                RememberItemPose();
                RefreshShareCode();
                SetStatus($"Pose set: {item.name}");

                // Play mode: animate ONLY the currently-loaded avatar (which may be a Random
                // Profile from the Debug tab), same as the pose buttons / Embedded popup — don't
                // force a reload of the custom Builder outfit just to change the emote. Edit mode
                // still routes through the full Apply.
                if (Application.isPlaying)
                    ApplyPoseOnly(outfit.emote);
                else
                    ScheduleApply();
                return;
            }

            var slot = item.Slot;

            // One wearable per slot: drop anything we know occupies the same category
            outfit.urns.RemoveAll(urn =>
                _knownItems.TryGetValue(urn, out var known) && known.Slot == slot);
            outfit.urns.Remove(item.urn);
            outfit.urns.Add(item.urn);

            RefreshShareCode();

            // A pick always equips — but while a row is isolated it also becomes the isolated one, so the
            // thing just clicked is the thing on screen. Without that, browsing during a shot equips
            // items that don't render and the browser looks broken. IsolateItem refreshes and applies.
            if (outfit.soloItem)
            {
                IsolateItem(item.urn, null, slot, item.name);
                return;
            }

            SetStatus($"Equipped {item.name} ({slot})");
            RefreshSlots();
            ScheduleApply();
        }

        // ---------------------------------------------------------------- Outfit pane

        private VisualElement BuildOutfitPane()
        {
            var pane = new ScrollView { style = { paddingLeft = 6, paddingRight = 6, paddingTop = 4 } };

            // --- Shader (selection persists via StudioAvatarShaderSwitcher and re-applies after
            // every avatar reload, edit and play mode, until another shader is picked). The selector
            // buttons stay visible for quick access; only the tuning panel is tucked into a
            // collapsible "Shader Settings" foldout (matching the Card frame section below).
            // Order matches StudioShaderMode — the buttons are indexed by the enum value.
            pane.Add(Header("Shader"));

            // Wraps: four labels this long don't fit one row at the default pane width, and a
            // truncated "DCL_Styliz…" is worse than a second row.
            var shaderRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap }
            };
            var shaderButtons = new Button[4];
            var shaderLabels = new[] { "DCL_Toon", "DCL_Toon_Studio", "DCL_Stylized_PBR", "DCL_Emotes" };

            // Tuning panel (rebuilt per selected shader; empty for the stock DCL_Toon)
            var shaderTuning = new VisualElement();

            void RefreshShaderButtons()
            {
                var current = (int)StudioAvatarShaderSwitcher.Mode;
                for (var i = 0; i < shaderButtons.Length; i++)
                    shaderButtons[i].SetEnabled(i != current); // disabled = selected, same as the tabs
            }

            for (var i = 0; i < shaderButtons.Length; i++)
            {
                var mode = (StudioShaderMode)i;
                shaderButtons[i] = new Button(() =>
                {
                    StudioAvatarShaderSwitcher.Mode = mode;
                    RefreshShaderButtons();
                    BuildShaderTuning(shaderTuning);
                }) { text = shaderLabels[i], style = { flexGrow = 1, minWidth = 108 } };
                shaderRow.Add(shaderButtons[i]);
            }

            RefreshShaderButtons();
            pane.Add(shaderRow);

            var shaderFold = new Foldout { text = "Shader Settings", value = false, style = { marginTop = 4 } };
            BuildShaderTuning(shaderTuning);
            shaderFold.Add(shaderTuning);
            pane.Add(shaderFold);

            // --- Card Frame (Fortnite-style item-card composite; studio-scene only, captured for free)
            BuildCardFrame(pane);

            // --- Scene lighting (studio-scene only, like the card frame)
            BuildSceneAndCamera(pane);

            // --- Outfit (body shape and colors live on the Avatar tab now). Always visible: isolating a
            // wearable for a shot is a per-row action on this list (the ◉ button), not a mode that
            // replaces it.
            pane.Add(Header("Outfit"));
            _slotsContainer = new VisualElement();
            pane.Add(_slotsContainer);

            // --- Framing, directly under the list it belongs to: shown only while a row is isolated,
            // because every control in it is about fitting *one item* in the frame.
            _framingSection = new VisualElement();
            BuildFramingSection(_framingSection);
            pane.Add(_framingSection);

            // --- Pose
            pane.Add(Header("Pose"));

            _poseLabel = new Label($"Pose: {PoseDisplayName(outfit.emote)}");
            pane.Add(_poseLabel);

            // Quick-pose buttons — one per single-frame GLB in StreamingAssets/poses/, auto-discovered.
            var poseGrid = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 2, marginBottom = 2 }
            };
            BuildPoseButtons(poseGrid);
            pane.Add(poseGrid);

            _emotePopup = new PopupField<string>("Embedded", EMBEDDED_EMOTE_CHOICES, 0);
            SyncEmotePopup();
            _emotePopup.RegisterValueChangedCallback(_ =>
            {
                // Selecting the sentinel isn't a real emote choice; only "idle" would land back
                // here anyway, so just treat it the same way.
                outfit.emote = _emotePopup.value switch
                {
                    EMBEDDED_EMOTE_NONE => "idle",
                    TPOSE_LABEL => TPOSE_EMOTE,
                    _ => _emotePopup.value
                };
                RemoveDraftEmote(); // an equipped draft emote would override the pose
                _poseLabel.text = $"Pose: {PoseDisplayName(outfit.emote)}";
                RememberItemPose(); // becomes the default for this item's category
                RefreshShareCode();

                // Play mode: animate ONLY the currently-loaded avatar (which may be a Random
                // Profile from the Debug tab), same as the pose buttons — don't force a reload of
                // the custom Builder outfit just to change the animation. Edit mode still routes
                // through the full Apply (animations aren't sampled onto the edit-mode skeleton).
                if (Application.isPlaying)
                    ApplyPoseOnly(outfit.emote);
                else
                    ScheduleApply();
            });
            pane.Add(_emotePopup);

            var transport = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            transport.Add(new Button(() => WithPreview(pc => pc.PlayEmote())) { text = "▶" });
            transport.Add(new Button(() => WithPreview(pc => pc.PauseEmote())) { text = "❚❚" });
            transport.Add(new Button(() => WithPreview(pc => pc.StopEmote())) { text = "■" });

            _emoteSlider = new Slider(0f, 1f) { style = { flexGrow = 1, marginLeft = 6 } };
            _emoteSlider.RegisterValueChangedCallback(evt => WithPreview(pc =>
            {
                pc.PauseEmote();
                pc.GoToEmote(evt.newValue);
            }));
            transport.Add(_emoteSlider);
            pane.Add(transport);

            // Keep the scrub slider range in sync with the loaded emote
            pane.schedule.Execute(() =>
            {
                if (!Application.isPlaying) return;
                var pc = FindPreviewController();
                if (pc == null) return;
                var length = pc.GetEmoteLength();
                if (length > 0f) _emoteSlider.highValue = length;
            }).Every(500);

            // --- Presets
            //
            // Always visible, including while a wearable is isolated — they act on the whole outfit,
            // which is on screen right above. CloneForPreset still defaults every solo field, so a preset
            // saves the list you can see and loads as a full avatar; isolation is a view, not outfit state.
            _presetsSection = new VisualElement();
            _presetsSection.Add(Header("Presets"));

            var presetField = new ObjectField("Preset") { objectType = typeof(OutfitPreset) };
            _presetsSection.Add(presetField);

            var presetButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            presetButtons.Add(new Button(() =>
            {
                if (presetField.value is not OutfitPreset preset)
                {
                    SetStatus("Select a preset first", true);
                    return;
                }

                LoadOutfit(preset.outfit.Clone());
                SetStatus($"Preset loaded: {preset.name}");
            }) { text = "Load" });

            presetButtons.Add(new Button(() =>
            {
                if (presetField.value is not OutfitPreset preset)
                {
                    SetStatus("Select a preset to overwrite (or use Save As)", true);
                    return;
                }

                preset.outfit = outfit.CloneForPreset();
                EditorUtility.SetDirty(preset);
                AssetDatabase.SaveAssets();
                SetStatus($"Preset saved: {preset.name}");
            }) { text = "Save" });

            presetButtons.Add(new Button(() =>
            {
                var path = EditorUtility.SaveFilePanelInProject("Save Outfit Preset", "OutfitPreset", "asset",
                    "Choose where to save the outfit preset");
                if (string.IsNullOrEmpty(path)) return;

                var preset = CreateInstance<OutfitPreset>();
                preset.outfit = outfit.CloneForPreset();
                AssetDatabase.CreateAsset(preset, path);
                AssetDatabase.SaveAssets();
                presetField.value = preset;
                SetStatus($"Preset created: {path}");
            }) { text = "Save As..." });
            _presetsSection.Add(presetButtons);
            pane.Add(_presetsSection);

            // --- Capture
            pane.Add(Header("Capture"));

            pane.Add(new Label("Rotation"));
            var rotationRow = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            rotationRow.Add(new Button(() => SnapRotate(15f)) { text = "<", style = { width = 30 } });
            _rotationLabel = new Label($"{rotationSnapAngle:0}°")
                { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleCenter } };
            rotationRow.Add(_rotationLabel);
            rotationRow.Add(new Button(() => SnapRotate(-15f)) { text = ">", style = { width = 30 } });
            pane.Add(rotationRow);

            pane.Add(new Button(LookAtCamera) { text = "Look at Camera" });

            var sizeRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var widthField = new IntegerField("Size") { value = captureWidth, style = { flexGrow = 1 } };
            widthField.RegisterValueChangedCallback(evt => captureWidth = Mathf.Clamp(evt.newValue, 64, 8192));
            var heightField = new IntegerField("x") { value = captureHeight, style = { flexGrow = 1 } };
            heightField.RegisterValueChangedCallback(evt => captureHeight = Mathf.Clamp(evt.newValue, 64, 8192));
            sizeRow.Add(widthField);
            sizeRow.Add(heightField);
            pane.Add(sizeRow);

            // Renders the still at this multiple of Size, then box-downsamples back to Size for the
            // exported PNG — every edge (including the extruded-shell avatar outline, which SMAA/TAA
            // can erode into a thin or noisy line at low native resolutions) gets factor² sub-pixel
            // samples instead of the one a direct render at Size gets. Stills only; Video is unaffected.
            var upsampleOptions = new List<string> { "1x (off)", "2x", "4x" };
            var upsampleValues = new[] { 1, 2, 4 };
            var upsampleIndex = Mathf.Max(0, Array.IndexOf(upsampleValues, captureUpsample));
            var upsamplePopup = new PopupField<string>("Upsample", upsampleOptions, upsampleIndex)
            {
                tooltip = "Renders the still at this multiple of Size, then downsamples back down to " +
                          "Size for the exported PNG — sharper edges and a cleaner outline than a " +
                          "direct render at Size, at the cost of a slower capture. Stills only."
            };
            upsamplePopup.RegisterValueChangedCallback(evt =>
                captureUpsample = upsampleValues[upsampleOptions.IndexOf(evt.newValue)]);
            pane.Add(upsamplePopup);

            // WYSIWYG: the PNG is always exactly Size × Size (the Recorder renders the camera at that
            // resolution regardless of the Game view), but the *framing* only matches what's on screen
            // if the Game view renders at the same resolution too — otherwise the card is laid out for
            // one aspect and exported at another. This pins the Game view to a Fixed Resolution entry
            // of exactly that size. See IMPLEMENTATION.md §20.
            var matchButton = new Button { text = "Match Game view to capture size" };
            var matchStatus = new Label
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal, fontSize = 10, opacity = 0.8f,
                    marginLeft = 3, marginBottom = 2
                }
            };

            void RefreshMatchStatus()
            {
                var gv = StudioGameViewSize.Current;
                if (gv.x == captureWidth && gv.y == captureHeight)
                {
                    matchStatus.text = $"Game view is {gv.x}×{gv.y} — matches, framing is WYSIWYG.";
                    matchStatus.style.color = new StyleColor(new Color(0.55f, 0.8f, 0.55f));
                }
                else
                {
                    matchStatus.text = $"Game view is {gv.x}×{gv.y}, capture is {captureWidth}×{captureHeight} " +
                                       "— the export will be framed differently from what you see.";
                    matchStatus.style.color = new StyleColor(new Color(0.95f, 0.75f, 0.4f));
                }
            }

            matchButton.clicked += () =>
            {
                if (!StudioGameViewSize.TryApply(captureWidth, captureHeight, out var error))
                    Debug.LogWarning($"[OutfitStudio] Couldn't set the Game view size ({error}). " +
                                     "Set it by hand: Game view ▸ size dropdown ▸ + ▸ Fixed Resolution " +
                                     $"{captureWidth} × {captureHeight}.");
                RefreshMatchStatus();
            };
            pane.Add(matchButton);
            pane.Add(matchStatus);
            RefreshMatchStatus();
            // Polled rather than event-driven: the artist can change the Game view's size from its own
            // dropdown at any time, and there's no notification for that.
            pane.schedule.Execute(RefreshMatchStatus).Every(500);

            var transparentToggle = new Toggle("Transparent background") { value = transparentBackground };
            transparentToggle.RegisterValueChangedCallback(evt => transparentBackground = evt.newValue);
            pane.Add(transparentToggle);

            var fpsField = new IntegerField("Video FPS") { value = captureFrameRate };
            fpsField.RegisterValueChangedCallback(evt => captureFrameRate = Mathf.Clamp(evt.newValue, 10, 60));
            pane.Add(fpsField);

            var folderRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var folderField = new TextField("Output folder") { value = outputFolder, style = { flexGrow = 1 } };
            folderField.RegisterValueChangedCallback(evt => outputFolder = evt.newValue);
            folderRow.Add(folderField);
            folderRow.Add(new Button(() =>
            {
                var chosen = EditorUtility.OpenFolderPanel("Capture output folder", outputFolder, "");
                if (!string.IsNullOrEmpty(chosen))
                {
                    outputFolder = chosen;
                    folderField.SetValueWithoutNotify(chosen);
                }
            }) { text = "..." });
            pane.Add(folderRow);

            pane.Add(new Button(CaptureStill) { text = "📷  Capture Still" });

            _videoButton = new Button(ToggleVideo) { text = "⏺  Start Video" };
            pane.Add(_videoButton);

            pane.Add(new Button(RecordEmote) { text = "🎬  Record Emote (full length)" });

            var turntableRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var turntableButton = new Button(RecordTurntable) { text = "🔄  Record Turntable", style = { flexGrow = 1 } };
            var durationField = new FloatField("s") { value = turntableDuration, style = { width = 70 } };
            durationField.RegisterValueChangedCallback(evt => turntableDuration = Mathf.Clamp(evt.newValue, 1f, 60f));
            turntableRow.Add(turntableButton);
            turntableRow.Add(durationField);
            pane.Add(turntableRow);

            // --- Share code
            //
            // Also always visible, and always the whole outfit: ToShareCode reads urns/base64Items
            // directly rather than the isolated substitution, so Copy can't quietly publish a one-item
            // outfit while a six-row list sits two sections above it. Nothing is lost — FromShareCode has
            // no way to express isolation (no hide-body parameter), so there was never a round trip.
            _shareCodeSection = new VisualElement();
            _shareCodeSection.Add(Header("Share code"));

            _shareCodeField = new TextField { multiline = true };
            _shareCodeField.style.whiteSpace = WhiteSpace.Normal;
            _shareCodeSection.Add(_shareCodeField);

            var shareButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            shareButtons.Add(new Button(() =>
            {
                EditorGUIUtility.systemCopyBuffer = outfit.ToShareCode();
                SetStatus("Share code copied to clipboard");
            }) { text = "Copy" });
            shareButtons.Add(new Button(() =>
            {
                LoadOutfit(OutfitDefinition.FromShareCode(_shareCodeField.value));
                SetStatus("Outfit loaded from share code");
            }) { text = "Load from code" });
            _shareCodeSection.Add(shareButtons);
            pane.Add(_shareCodeSection);

            // Builds the initial slot list (with its isolation banner and ◉ states) and shows or hides the
            // framing section to match whatever isolation survived the domain reload. Also where a stale
            // soloUrn from a previous session gets reconciled away — see ReconcileSoloSelection.
            RefreshIsolation();

            return pane;
        }

        // ---------------------------------------------------------------- Item pose memory

        private const string K_ITEM_POSE_PREFIX = "OutfitStudio.ItemPose.";

        /// <summary>
        /// The display pose last used for a wearable category while isolated, or null. Per
        /// category because that's the unit the answer varies by: every upper body wants the same
        /// jacket pose, a hat wants a neutral one. EditorPrefs rather than the outfit, since it's a
        /// working preference rather than part of any one item's look.
        /// </summary>
        private static string RememberedItemPose(string category) =>
            string.IsNullOrEmpty(category) ? null : EditorPrefs.GetString(K_ITEM_POSE_PREFIX + category, null);

        /// <summary>
        /// Records the current pose as the default for the current item's category. Called from every
        /// pose-mutation site, but only while a wearable is isolated — poses picked for a whole avatar say
        /// nothing about how a lone jacket should hang.
        /// </summary>
        private void RememberItemPose()
        {
            if (!outfit.soloItem) return;

            var category = SoloItemCategory();
            if (string.IsNullOrEmpty(category)) return;

            EditorPrefs.SetString(K_ITEM_POSE_PREFIX + category, outfit.emote ?? "idle");
        }

        /// <summary>The isolated item's wearable category, or null when it can't be resolved.</summary>
        private string SoloItemCategory()
        {
            if (!string.IsNullOrEmpty(outfit.soloBase64))
                return DescribeDraft(outfit.soloBase64).category;

            return string.IsNullOrEmpty(outfit.soloUrn)
                ? null
                : _knownItems.GetValueOrDefault(outfit.soloUrn)?.Slot;
        }

        // ---------------------------------------------------------------- Isolating one wearable

        /// <summary>
        /// Drops isolation, camera included, without touching the outfit — the isolated item stays
        /// equipped, since it was always a row in the list rather than a separate pick.
        ///
        /// The camera is the part that isn't optional. FrameItem parked it on the item and left the
        /// Cinemachine brain disabled, so the full avatar would otherwise be shot from the item's framing.
        /// Handing the brain back restores builderCamera's authored shot — exactly what the Framing
        /// section's "Reset" button does. The other direction is already covered: entering isolation ends
        /// in ScheduleFrameItem, which re-derives framing from the item's bounds. Neither view needs a
        /// *saved* camera — both regenerate theirs from an authoritative source, and a stale saved
        /// transform would be its own source of wrong framing.
        /// </summary>
        private void ClearIsolation()
        {
            outfit.soloItem = false;
            outfit.soloUrn = null;
            outfit.soloBase64 = null;

            StudioItemCamera.Release();
        }

        /// <summary>Back to the full avatar. Wired to the ◉ button on the isolated row and to the banner.</summary>
        private void ExitIsolation()
        {
            if (!outfit.soloItem) return;

            ClearIsolation();
            RefreshIsolation();
            ScheduleApply();

            SetStatus("Showing the full avatar again — camera handed back to the scene's shot");
        }

        /// <summary>
        /// Makes an equipped wearable the isolated subject: only it renders, with the body hidden. Applies
        /// the remembered display pose for its category so an upper body doesn't land in A-pose (see
        /// <see cref="RememberedItemPose"/>).
        ///
        /// Callers must have the item in <c>outfit.urns</c>/<c>base64Items</c> already — that invariant is
        /// what lets the list carry the ◉ button, and <see cref="ReconcileSoloSelection"/> enforces it.
        /// </summary>
        private void IsolateItem(string urn, string base64, string category, string displayName)
        {
            outfit.soloItem = true;
            outfit.soloUrn = urn;
            outfit.soloBase64 = base64;

            var pose = RememberedItemPose(category);
            if (!string.IsNullOrEmpty(pose) && pose != outfit.emote)
            {
                outfit.emote = pose;
                RemoveDraftEmote();
                if (_poseLabel != null) _poseLabel.text = $"Pose: {PoseDisplayName(outfit.emote)}";
                SyncEmotePopup();
            }

            RefreshIsolation();
            ScheduleApply(); // ends in ScheduleFrameItem, so the item frames itself once the load lands

            SetStatus($"Isolated {displayName} ({category}) — the rest of the outfit is kept but hidden");
        }

        /// <summary>
        /// Shows the framing controls when a wearable is isolated and hides them when it isn't, then
        /// rebuilds the slot list so the banner, the row accent and the ◉ states follow.
        /// </summary>
        private void RefreshIsolation()
        {
            if (_framingSection != null)
                _framingSection.style.display = outfit.soloItem ? DisplayStyle.Flex : DisplayStyle.None;

            _syncFramingFields?.Invoke();
            RefreshSlots();
        }

        /// <summary>
        /// Enforces the invariant that the isolated item is one of the equipped ones, dropping isolation
        /// when it no longer is — unequipped with ✕, displaced by a same-slot pick, replaced wholesale by a
        /// preset, or left over in serialized window state from before isolation moved into the list.
        /// Returns whether it changed anything.
        ///
        /// Called from exactly one place, the top of <see cref="RefreshSlots"/>: every mutation of
        /// <c>urns</c>/<c>base64Items</c> already funnels through there, and each of those callers already
        /// schedules an apply afterwards. Deliberately does NOT call <see cref="RefreshIsolation"/> —
        /// that calls RefreshSlots, and a nested rebuild mid-loop would garble the list.
        /// </summary>
        private bool ReconcileSoloSelection()
        {
            if (!outfit.soloItem) return false;

            var stillEquipped =
                (!string.IsNullOrEmpty(outfit.soloUrn) && outfit.urns.Contains(outfit.soloUrn))
                || (!string.IsNullOrEmpty(outfit.soloBase64) && outfit.base64Items.Contains(outfit.soloBase64));

            if (stillEquipped) return false;

            ClearIsolation();
            return true;
        }

        /// <summary>
        /// The Framing section: how the isolated item sits in the frame. Built once and only shown or
        /// hidden, so <see cref="_syncFramingFields"/> is what keeps its controls truthful.
        /// </summary>
        private void BuildFramingSection(VisualElement section)
        {
            _framingHeader = Header("Framing");
            section.Add(_framingHeader);

            section.Add(new Label("Only this wearable renders — the body is hidden but the skeleton stays, "
                                  + "so it still skins and poses. Use the Pose section below so an upper "
                                  + "body isn't shot in A-pose. Posing and capture need play mode.")
            {
                style =
                {
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Italic,
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 4
                }
            });

            var framingRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            framingRow.Add(new Button(FrameItem) { text = "Frame item", style = { flexGrow = 1 } });
            framingRow.Add(new Button(() =>
            {
                StudioItemCamera.Release();
                SetStatus("Framing handed back to Cinemachine");
            }) { text = "Reset", style = { flexGrow = 1 } });
            section.Add(framingRow);

            var autoFrame = new Toggle("Auto-frame on item change")
            {
                value = autoFrameItem,
                tooltip = "Re-frame when the item or body shape changes. Deliberately NOT on pose "
                          + "changes — an outstretched arm inflates the item's bounds enormously, so "
                          + "re-fitting per pose makes the camera lurch and the garment change size."
            };
            autoFrame.RegisterValueChangedCallback(evt => autoFrameItem = evt.newValue);
            section.Add(autoFrame);

            // One zoom rather than the Margin X / Margin Y pair this replaced: only whichever axis bound
            // the fit ever did anything, so the second slider read as a control while behaving as a no-op
            // (see §22). Past 100% the item overspills and crops, which is what negative margins were for.
            var zoom = new Slider("Zoom Frame (%)", 25f, 250f)
            {
                value = outfit.soloZoomPct,
                showInputField = true,
                tooltip = "How much of the frame the item fills. 100% = exactly touching the edge on "
                          + "whichever axis binds (the status line names it), above that it grows and "
                          + "crops, below it leaves margin. Fits the card rect instead when 'Fit to card' "
                          + "is on."
            };
            zoom.RegisterValueChangedCallback(evt =>
            {
                outfit.soloZoomPct = evt.newValue;
                if (Application.isPlaying) FrameItem(); // cheap and instant — no reload involved
            });
            section.Add(zoom);

            var offsetY = new Slider("Vertical Offset (px)", -300f, 300f)
            {
                value = outfit.soloOffsetYPx,
                showInputField = true,
                tooltip = "Nudge the item down (positive) or up — image-editor convention, Y grows "
                          + "downward. Defaults to 70 because items otherwise land systematically high: "
                          + "their bounds' geometric centre sits below where the eye reads the centre."
            };
            offsetY.RegisterValueChangedCallback(evt =>
            {
                outfit.soloOffsetYPx = evt.newValue;
                if (Application.isPlaying) FrameItem();
            });
            section.Add(offsetY);

            var offsetX = new Slider("Horizontal Offset (px)", -300f, 300f)
            {
                value = outfit.soloOffsetXPx,
                showInputField = true,
                tooltip = "Nudge the item right (positive) or left. Defaults to 0 — unlike the vertical "
                          + "axis there's no bias to correct, since an item sits on the avatar's centre "
                          + "line; this is for asymmetric items like a single earring or a held staff."
            };
            offsetX.RegisterValueChangedCallback(evt =>
            {
                outfit.soloOffsetXPx = evt.newValue;
                if (Application.isPlaying) FrameItem();
            });
            section.Add(offsetX);

            var fitToCard = new Toggle("Fit to card instead of frame")
            {
                value = outfit.soloFitToCard,
                tooltip = "Off (default): fill the whole render, for compositing a card around the item "
                          + "in an image editor. On: fit inside the studio's own card rect — which is "
                          + "0.55 of the frame's height wide, so the item comes out around half the "
                          + "frame's width by design."
            };
            fitToCard.RegisterValueChangedCallback(evt =>
            {
                outfit.soloFitToCard = evt.newValue;
                if (Application.isPlaying) FrameItem();
            });
            section.Add(fitToCard);

            var garmentOnly = new Toggle("Measure garment only (ignore arms)")
            {
                value = outfit.soloFitGarmentOnly,
                tooltip = "Off (default): fit the whole silhouette, so nothing is cut off. On: measure "
                          + "only the garment and ignore bare skin, so it reads at the same size in every "
                          + "pose — useful across a sheet of items, but extended limbs will overspill the "
                          + "margin and crop."
            };
            garmentOnly.RegisterValueChangedCallback(evt =>
            {
                outfit.soloFitGarmentOnly = evt.newValue;
                if (Application.isPlaying) FrameItem();
            });
            section.Add(garmentOnly);

            section.Add(new Label("Rotate with the mouse as usual; framing keeps whatever angle the "
                                  + "camera is at, so re-framing after a drag won't undo it.")
            {
                style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Italic, whiteSpace = WhiteSpace.Normal }
            });

            // These controls are built once and only shown/hidden, so nothing above re-reads the outfit
            // when it's replaced wholesale by a preset or a share code — they'd keep displaying the
            // previous values and the next drag would write one of them back (§22). SetValueWithoutNotify
            // throughout: firing the callbacks here would re-frame mid-load with half-applied state.
            _syncFramingFields = () =>
            {
                zoom.SetValueWithoutNotify(outfit.soloZoomPct);
                offsetY.SetValueWithoutNotify(outfit.soloOffsetYPx);
                offsetX.SetValueWithoutNotify(outfit.soloOffsetXPx);
                fitToCard.SetValueWithoutNotify(outfit.soloFitToCard);
                garmentOnly.SetValueWithoutNotify(outfit.soloFitGarmentOnly);

                // Names its subject, so the section still identifies itself once the list has scrolled off
                _framingHeader.text = outfit.soloItem ? $"Framing — {DescribeSoloItem()}" : "Framing";
            };
        }

        /// <summary>"[slot] Name" for the isolated item, matching how its row in the outfit list reads.</summary>
        private string DescribeSoloItem()
        {
            if (!string.IsNullOrEmpty(outfit.soloBase64))
            {
                var (name, category, _) = DescribeDraft(outfit.soloBase64);
                return $"[{category}] {name} (draft)";
            }

            if (string.IsNullOrEmpty(outfit.soloUrn)) return "no item";

            var known = _knownItems.GetValueOrDefault(outfit.soloUrn);
            var slot = known?.Slot ?? "?";
            return $"[{slot}] {known?.name ?? outfit.soloUrn[(outfit.soloUrn.LastIndexOf(':') + 1)..]}";
        }

        /// <summary>
        /// Frames the camera on the isolated item. Deferred by a couple of frames when called straight
        /// after an apply: skinned bounds only become real once the pose has been applied and a frame
        /// has rendered (same reason PreviewController awaits a frame before CenterAndFit).
        /// </summary>
        private void FrameItem()
        {
            if (!EnsurePlaying()) return;

            // captureHeight, not the live Game view: the offsets are authored in capture pixels, and
            // "Match Game view to capture size" is what keeps the two the same.
            if (StudioItemCamera.FrameItem(outfit, captureHeight, out var error, out var report))
                SetStatus($"Framed — {report}");
            else
                SetStatus(error, true);
        }

        /// <summary>
        /// Re-frames after an item change, once the reload it triggered has had time to land — skinned
        /// bounds are only meaningful after the new mesh is posed and a frame has rendered. Framed twice
        /// deliberately: a slow load would otherwise be measured half-assembled and stay wrong until the
        /// artist noticed, and the second pass costs nothing.
        /// </summary>
        private void ScheduleFrameItem()
        {
            if (!autoFrameItem || !outfit.soloItem || !Application.isPlaying) return;

            foreach (var delayMs in new[] { 700, 1800 })
            {
                rootVisualElement.schedule.Execute(() =>
                {
                    if (autoFrameItem && outfit.soloItem && Application.isPlaying)
                        StudioItemCamera.FrameItem(outfit, captureHeight, out _, out _);
                }).StartingIn(delayMs);
            }
        }

        // Rebuilds the live tuning sliders for the currently selected shader. Values are stored
        // and applied by StudioAvatarShaderSwitcher (the knob list is its single source of truth),
        // so a change pushes onto every avatar material immediately, in edit and play mode.
        private void BuildShaderTuning(VisualElement container)
        {
            container.Clear();

            var mode = StudioAvatarShaderSwitcher.Mode;
            var knobs = StudioAvatarShaderSwitcher.KnobsFor(mode);
            if (knobs.Length == 0)
            {
                container.Add(new Label("Stock shader — no tunable properties.")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Italic, marginTop = 4, opacity = 0.7f }
                });
                return;
            }

            // DCL_Emotes skips both of the blocks below: its two outline knobs aren't worth a
            // preset asset type of their own, and nothing in a flat white surface reflects a
            // matcap. Everything from the knob loop down is shared.
            if (mode != StudioShaderMode.DclEmotes)
            {
                // Shader tuning presets — save/apply the full knob set (all sliders + colors below)
                // as a reusable asset. One preset type per shader since their knob tables differ.
                container.Add(new Label("Presets") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } });
                if (mode == StudioShaderMode.DclToonStudio)
                    BuildShaderPresetsRow<StudioToonShaderPreset>(container, mode, TOON_SHADER_PRESETS_DIR);
                else
                    BuildShaderPresetsRow<StudioPbrShaderPreset>(container, mode, PBR_SHADER_PRESETS_DIR);

                // Matcap selector — the metal reflection texture bound to stylized-metal materials.
                // Both studio shaders use it; the list comes from the loaded MatcapPresets library.
                var matcapNames = StudioAvatarShaderSwitcher.GetMatcapNames();
                if (matcapNames.Length == 0)
                {
                    container.Add(new Label("Matcap: library not loaded yet — load an outfit first.")
                    {
                        style = { unityFontStyleAndWeight = FontStyle.Italic, marginTop = 4, opacity = 0.7f }
                    });
                }
                else
                {
                    var active = StudioAvatarShaderSwitcher.ActiveMatcapName;
                    if (Array.IndexOf(matcapNames, active) < 0) active = matcapNames[0];
                    var matcapField = new PopupField<string>("Matcap", matcapNames.ToList(), active)
                    {
                        tooltip = "Which matcap texture the stylized metal reflects (from MatcapPresets)."
                    };
                    matcapField.RegisterValueChangedCallback(evt =>
                        StudioAvatarShaderSwitcher.ActiveMatcapName = evt.newValue);
                    container.Add(matcapField);
                }
            }

            foreach (var knob in knobs)
            {
                if (knob.Kind == StudioKnobKind.Toggle)
                {
                    var toggle = new Toggle(knob.Label)
                    {
                        value = StudioAvatarShaderSwitcher.GetFloat(mode, knob) > 0.5f,
                        tooltip = knob.Tooltip
                    };
                    toggle.RegisterValueChangedCallback(evt =>
                        StudioAvatarShaderSwitcher.SetFloat(mode, knob, evt.newValue ? 1f : 0f));
                    container.Add(toggle);
                }
                else if (knob.Kind == StudioKnobKind.Float)
                {
                    var slider = new Slider(knob.Label, knob.Min, knob.Max)
                    {
                        value = StudioAvatarShaderSwitcher.GetFloat(mode, knob),
                        showInputField = true,
                        tooltip = knob.Tooltip
                    };
                    slider.RegisterValueChangedCallback(evt =>
                        StudioAvatarShaderSwitcher.SetFloat(mode, knob, evt.newValue));
                    container.Add(slider);
                }
                else
                {
                    var color = new ColorField(knob.Label)
                    {
                        value = StudioAvatarShaderSwitcher.GetColor(mode, knob),
                        showAlpha = false,
                        tooltip = knob.Tooltip
                    };
                    color.RegisterValueChangedCallback(evt =>
                        StudioAvatarShaderSwitcher.SetColor(mode, knob, evt.newValue));
                    container.Add(color);
                }
            }

            container.Add(new Button(() =>
            {
                StudioAvatarShaderSwitcher.ResetKnobs(mode);
                BuildShaderTuning(container); // reflect reset values back into the fields
            }) { text = "Reset shader defaults", style = { marginTop = 4 } });
        }

        // One button per preset asset of type T (applies it to the live knobs) plus "Save current…"
        // (snapshots the live knobs into a new asset under dir) and "⟳ Rescan" — same layout as the
        // card-colour presets' button row (see BuildCardBody).
        private void BuildShaderPresetsRow<T>(VisualElement container, StudioShaderMode mode, string dir)
            where T : StudioShaderPreset
        {
            var presetRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };

            foreach (var preset in LoadShaderPresets<T>())
            {
                var p = preset; // capture per-iteration
                presetRow.Add(new Button(() =>
                {
                    StudioAvatarShaderSwitcher.ApplyPreset(mode, p);
                    BuildShaderTuning(container);
                }) { text = p.name, style = { marginRight = 2, marginBottom = 2 } });
            }

            if (presetRow.childCount == 0)
                presetRow.Add(new Label($"No presets — create via Assets ▸ Create ▸ Outfit Studio ▸ {ObjectNames.NicifyVariableName(typeof(T).Name)}")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Italic, opacity = 0.7f, marginRight = 4 }
                });

            presetRow.Add(new Button(() =>
            {
                EnsureFolder(dir); // so the dialog opens in the intended default folder
                var path = EditorUtility.SaveFilePanelInProject($"Save {ObjectNames.NicifyVariableName(typeof(T).Name)}",
                    typeof(T).Name, "asset", "Save the current shader tuning values as a reusable preset.", dir);
                if (string.IsNullOrEmpty(path)) return;

                var preset = ScriptableObject.CreateInstance<T>();
                StudioAvatarShaderSwitcher.CaptureKnobValues(mode, preset);
                AssetDatabase.CreateAsset(preset, path);
                AssetDatabase.SaveAssets();
                BuildShaderTuning(container); // show the new preset button
            }) { text = "Save current…", tooltip = "Save the current tuning values as a new preset asset", style = { marginRight = 2, marginBottom = 2 } });

            presetRow.Add(new Button(() => BuildShaderTuning(container))
            {
                text = "⟳", tooltip = "Rescan for preset assets", style = { marginBottom = 2 }
            });

            container.Add(presetRow);
        }

        // All assets of type T, sorted by name (one button each) — same discover-by-type pattern as
        // LoadCardPresets.
        private static List<T> LoadShaderPresets<T>() where T : StudioShaderPreset =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(p => p != null)
                .OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        // Fortnite-style "item card" frame around the avatar (rounded card → avatar → bottom fade →
        // border), composed by StudioCardFrame as camera-parented quads so it renders through the
        // capture camera. There's no background layer — outside the card is empty (black live,
        // transparent on export) and the card itself carries the Decentraland vignette/pattern paint.
        /// <summary>
        /// "Scene and Camera settings" — live tuning for the studio scene's three lights, shaped like the
        /// Card frame foldout above it. Named for more than it currently holds: camera knobs are the
        /// intended next tenant, so the section exists under the general name rather than "Lights".
        ///
        /// Values live in <see cref="StudioSceneLights"/> (EditorPrefs), which also owns the scene-authored
        /// defaults that Reset restores.
        /// </summary>
        private void BuildSceneAndCamera(VisualElement pane)
        {
            var fold = new Foldout
            {
                text = "Scene and Camera settings",
                value = false,
                style = { marginTop = 4 }
            };

            fold.Add(new Label("The lights tune live, in edit mode and play mode. Their values are kept in "
                               + "EditorPrefs rather than the scene, so nothing is written until you "
                               + "actually change something — and Reset puts the scene's own values back.")
            {
                style =
                {
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Italic,
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 4
                }
            });

            // Sub-headers rather than nested foldouts: three lights with two or three knobs each is a
            // short list, and a foldout per light would hide the thing being compared.
            static Label SubHeader(string text) => new Label(text)
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6, marginBottom = 2 }
            };

            // --- Directional (the key light: colour, brightness and which way it comes from)
            fold.Add(SubHeader("Directional Light"));

            var dirColor = new ColorField("Color") { value = StudioSceneLights.DirColor };
            dirColor.RegisterValueChangedCallback(evt => StudioSceneLights.DirColor = evt.newValue);
            fold.Add(dirColor);

            var dirIntensity = new Slider("Intensity", 0f, 10f)
            {
                value = StudioSceneLights.DirIntensity,
                showInputField = true
            };
            dirIntensity.RegisterValueChangedCallback(evt => StudioSceneLights.DirIntensity = evt.newValue);
            fold.Add(dirIntensity);

            var dirYaw = new Slider("Y Rotation", 0f, 360f)
            {
                value = StudioSceneLights.DirYaw,
                showInputField = true,
                tooltip = "Spins the key light around the avatar. Only Y is exposed — the light's tilt is "
                          + "held at the scene's authored angle, so this stays a pure orbit and can't "
                          + "accidentally point the light at the floor."
            };
            dirYaw.RegisterValueChangedCallback(evt => StudioSceneLights.DirYaw = evt.newValue);
            fold.Add(dirYaw);

            // --- Spotlights (colour and brightness only, as asked — their placement is the scene's)
            fold.Add(SubHeader("Spot Light Front"));

            var frontColor = new ColorField("Color") { value = StudioSceneLights.FrontColor };
            frontColor.RegisterValueChangedCallback(evt => StudioSceneLights.FrontColor = evt.newValue);
            fold.Add(frontColor);

            var frontIntensity = new Slider("Intensity", 0f, 100f)
            {
                value = StudioSceneLights.FrontIntensity,
                showInputField = true
            };
            frontIntensity.RegisterValueChangedCallback(evt =>
                StudioSceneLights.FrontIntensity = evt.newValue);
            fold.Add(frontIntensity);

            fold.Add(SubHeader("Spot Light Back"));

            var backColor = new ColorField("Color") { value = StudioSceneLights.BackColor };
            backColor.RegisterValueChangedCallback(evt => StudioSceneLights.BackColor = evt.newValue);
            fold.Add(backColor);

            // 0..100 on both spots even though the defaults sit low (8 front, 15 back): one shared range
            // makes the two readable against each other, and the input field covers precise values.
            var backIntensity = new Slider("Intensity", 0f, 100f)
            {
                value = StudioSceneLights.BackIntensity,
                showInputField = true
            };
            backIntensity.RegisterValueChangedCallback(evt =>
                StudioSceneLights.BackIntensity = evt.newValue);
            fold.Add(backIntensity);

            fold.Add(new Button(() =>
            {
                StudioSceneLights.ResetToSceneDefaults();

                // SetValueWithoutNotify, or each write would call back into the setter and re-create the
                // override this just deleted.
                dirColor.SetValueWithoutNotify(StudioSceneLights.DirColor);
                dirIntensity.SetValueWithoutNotify(StudioSceneLights.DirIntensity);
                dirYaw.SetValueWithoutNotify(StudioSceneLights.DirYaw);
                frontColor.SetValueWithoutNotify(StudioSceneLights.FrontColor);
                frontIntensity.SetValueWithoutNotify(StudioSceneLights.FrontIntensity);
                backColor.SetValueWithoutNotify(StudioSceneLights.BackColor);
                backIntensity.SetValueWithoutNotify(StudioSceneLights.BackIntensity);

                SetStatus("Scene lights reset to the values the scene ships with");
            })
            {
                text = "Reset lights to scene defaults",
                style = { marginTop = 8 }
            });

            // --- Fly camera. Moved here from the Debug tab: it's a camera control, and it's tuned
            // against the lights above it rather than against anything else in Debug. Its own settings
            // live in StudioFlyCameraController (EditorPrefs), so nothing about the move changes state —
            // and "Reset lights to scene defaults" above deliberately stays scoped to the lights.
            fold.Add(SubHeader("Fly Camera"));
            fold.Add(new Label("Hold the right mouse button and use WASD/QE to fly (Shift to go " +
                               "faster) — like Unity's Scene view. Play mode only.")
            {
                style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Italic, whiteSpace = WhiteSpace.Normal, marginBottom = 4 }
            });

            var flyEnabled = new Toggle("Enable")
            {
                value = StudioFlyCameraController.Enabled,
                tooltip = "Takes over the camera's transform from Cinemachine while the right mouse " +
                          "button is held. The view stays wherever you fly it on release — use " +
                          "\"Reset View\" below to hand framing back to Cinemachine."
            };
            flyEnabled.RegisterValueChangedCallback(evt => StudioFlyCameraController.Enabled = evt.newValue);
            fold.Add(flyEnabled);

            CardSlider(fold, "Move Speed", 1f, 20f,
                () => StudioFlyCameraController.MoveSpeed, v => StudioFlyCameraController.MoveSpeed = v);
            CardSlider(fold, "Look Speed", 0.02f, 0.5f,
                () => StudioFlyCameraController.LookSpeed, v => StudioFlyCameraController.LookSpeed = v);

            fold.Add(new Button(StudioFlyCameraController.ResetView) { text = "Reset View" });

            pane.Add(fold);
        }

        // Studio scene only; a collapsible section since it's beauty-shot dressing, not part of the
        // outfit. See IMPLEMENTATION.md §18.
        private void BuildCardFrame(VisualElement pane)
        {
            var fold = new Foldout { text = "Card frame (beauty shot)", value = false, style = { marginTop = 4 } };

            var enable = new Toggle("Enable") { value = StudioCardFrame.Enabled };
            enable.RegisterValueChangedCallback(evt => StudioCardFrame.Enabled = evt.newValue);
            fold.Add(enable);

            var disableMiddleCard = new Toggle("Disable Middle Card")
            {
                value = StudioCardFrame.DisableMiddleCard,
                tooltip = "Hides the card panel, its border, and the bottom fade, leaving just the " +
                          "avatar over an empty (transparent) frame."
            };
            fold.Add(disableMiddleCard);

            var sideMask = new Toggle("Mask avatar to card sides")
            {
                value = StudioCardFrame.SideMask,
                tooltip = "Erase arms/hands that spill past the card's left/right edges (the head " +
                          "still overflows the top), like the Fortnite cards. On by default; switched " +
                          "off automatically by Disable Middle Card."
            };
            sideMask.RegisterValueChangedCallback(evt => StudioCardFrame.SideMask = evt.newValue);
            fold.Add(sideMask);

            var bottomMask = new Toggle("Mask avatar below card")
            {
                value = StudioCardFrame.BottomMask,
                tooltip = "Erase feet/shoes that hang below the card's bottom edge on a tall pose. " +
                          "On by default — unlike the head overflowing the top, a subject poking out " +
                          "of the bottom reads as a mistake. Switched off automatically by Disable " +
                          "Middle Card."
            };
            bottomMask.RegisterValueChangedCallback(evt => StudioCardFrame.BottomMask = evt.newValue);
            fold.Add(bottomMask);

            var closedBorder = new Toggle("Closed border (item card)")
            {
                value = StudioCardFrame.ClosedBorder,
                tooltip = "Run the border ring the whole way round and crop the top edge like any " +
                          "other, instead of leaving the top open. Off by default: the open top is " +
                          "deliberate for avatars, whose heads are meant to overflow. Turn it on for " +
                          "isolated-item shots, where the subject belongs fully inside the card.\n\n" +
                          "The top crop needs at least one mask toggle on to have a mask to crop with."
            };
            closedBorder.RegisterValueChangedCallback(evt => StudioCardFrame.ClosedBorder = evt.newValue);
            fold.Add(closedBorder);

            // Registered after the two mask toggles exist, since hiding the card also clears them (see
            // StudioCardFrame.DisableMiddleCard) and the checkboxes have to follow — otherwise they'd
            // sit visibly ticked while the crop was actually off.
            disableMiddleCard.RegisterValueChangedCallback(evt =>
            {
                StudioCardFrame.DisableMiddleCard = evt.newValue;
                sideMask.SetValueWithoutNotify(StudioCardFrame.SideMask);
                bottomMask.SetValueWithoutNotify(StudioCardFrame.BottomMask);
            });

            var hideOutline = new Toggle("Hide avatar outline")
            {
                value = StudioCardFrame.HideOutline,
                tooltip = "Suppress the avatar's outline (a thin silhouette line, most visible over " +
                          "the head against a light card) for clean beauty shots. Play mode only."
            };
            hideOutline.RegisterValueChangedCallback(evt => StudioCardFrame.HideOutline = evt.newValue);
            fold.Add(hideOutline);

            var body = new VisualElement();
            BuildCardBody(body);
            fold.Add(body);
            // Registered on `body` (which survives BuildCardBody's Clear()) rather than inside
            // BuildCardBody, so a rebuild doesn't stack up a second poll on every preset apply.
            body.schedule.Execute(SyncCardPxFields).Every(500);

            pane.Add(fold);
        }

        private void BuildCardBody(VisualElement c)
        {
            c.Clear();
            _cardPxSync.Clear(); // the px fields we're about to replace are gone with the children
            _cardSizeLabel = null;

            Label Section(string t) => new(t) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } };

            // Colour presets — one button per CardColorPreset asset. Applies ONLY the card paint (3
            // colours + the pattern texture + the glow), leaving the current margins/sizes/toggles
            // intact. The whole body is rebuilt on apply so the fields below reflect the new values.
            c.Add(Section("Presets"));
            var presetRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            foreach (var preset in LoadCardPresets())
            {
                var p = preset; // capture per-iteration
                presetRow.Add(new Button(() =>
                {
                    ApplyCardPreset(p);
                    BuildCardBody(c);
                }) { text = p.name, style = { marginRight = 2, marginBottom = 2 } });
            }

            if (presetRow.childCount == 0)
                presetRow.Add(new Label("No presets — create via Assets ▸ Create ▸ Outfit Studio ▸ Card Color Preset")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Italic, opacity = 0.7f, marginRight = 4 }
                });

            presetRow.Add(new Button(() =>
            {
                EnsureFolder(CARD_PRESETS_DIR); // so the dialog opens in the intended default folder
                var path = EditorUtility.SaveFilePanelInProject("Save Card Color Preset", "CardColorPreset",
                    "asset", "Save the current card colours, pattern and glow as a reusable preset.",
                    CARD_PRESETS_DIR);
                if (string.IsNullOrEmpty(path)) return;

                var preset = ScriptableObject.CreateInstance<CardColorPreset>();
                preset.cardInner = StudioCardFrame.DclInnerColor;
                preset.cardOuter = StudioCardFrame.DclOuterColor;
                preset.border = StudioCardFrame.Border;
                preset.pattern = StudioCardFrame.PatternTexture;
                preset.patternEnabled = StudioCardFrame.PatternEnabled;
                preset.glowColor = StudioCardFrame.GlowColor;
                preset.glowRadius = StudioCardFrame.GlowRadius;
                AssetDatabase.CreateAsset(preset, path);
                AssetDatabase.SaveAssets();
                BuildCardBody(c); // show the new preset button
            }) { text = "Save current…", tooltip = "Save the current 3 colours + pattern texture + glow as a new preset asset", style = { marginRight = 2, marginBottom = 2 } });

            presetRow.Add(new Button(() => BuildCardBody(c))
            {
                text = "⟳", tooltip = "Rescan for preset assets", style = { marginBottom = 2 }
            });
            c.Add(presetRow);

            // The card's two vignette colours (inner = centre, outer = edges/corners) plus the pattern
            // texture tiled over them. The glow gets its own section below; the rest of the
            // Decentraland paint (tiling ratio, scroll speed) stays fixed at the reference values.
            c.Add(Section("Card"));
            CardColor(c, "Inner Color", () => StudioCardFrame.DclInnerColor, v => StudioCardFrame.DclInnerColor = v);
            CardColor(c, "Outer Color", () => StudioCardFrame.DclOuterColor, v => StudioCardFrame.DclOuterColor = v);

            var pattern = new ObjectField("Pattern")
            {
                objectType = typeof(Texture2D),
                allowSceneObjects = false,
                value = StudioCardFrame.PatternEnabled ? StudioCardFrame.PatternTexture : null,
                tooltip = "Tiling pattern drawn over the card's vignette. Defaults to the bundled " +
                          "DclBackgroundPattern (Explorer's icon atlas). Set to None for no pattern at " +
                          "all — just the Inner/Outer vignette. Import a replacement with Wrap Mode = " +
                          "Repeat, or it will clamp into streaks at the card edges."
            };
            pattern.RegisterValueChangedCallback(e =>
            {
                var tex = e.newValue as Texture2D;
                if (tex != null) StudioCardFrame.PatternTexture = tex;
                else StudioCardFrame.PatternEnabled = false;
            });
            c.Add(pattern);

            // The radial hotspot behind the avatar. It is ADDED on top of the vignette, so it is the
            // one bit of paint that survives blacking out Inner/Outer and turning the pattern off —
            // hence its own knobs rather than the fixed reference values it used to run on. The radii
            // are in the card's own 0..1 UV space, NOT pixels like the lengths below.
            c.Add(Section("Glow"));
            CardColor(c, "Glow Color", () => StudioCardFrame.GlowColor,
                v => StudioCardFrame.GlowColor = v, showAlpha: true);
            CardSlider(c, "Glow Width", 0.001f, 0.6f,
                () => StudioCardFrame.GlowRadius.x,
                v => StudioCardFrame.GlowRadius = new Vector2(v, StudioCardFrame.GlowRadius.y));
            CardSlider(c, "Glow Height", 0.001f, 0.6f,
                () => StudioCardFrame.GlowRadius.y,
                v => StudioCardFrame.GlowRadius = new Vector2(StudioCardFrame.GlowRadius.x, v));

            // Every length below is shown and entered in PIXELS of the current capture size, so an artist
            // can work straight off a Figma spec — see CardPxSlider and IMPLEMENTATION.md §20.
            //
            // Card Width is a WIDTH, not a pair of side margins: it's stored per frame HEIGHT so the card
            // keeps its shape (and its size relative to the avatar) at any capture aspect. It's the field
            // that used to be "Margin Sides", which was width-relative and reshaped the card on every
            // resolution change. The card is always horizontally centred.
            CardPxSlider(c, "Card Width", () => FrameWPx,
                () => StudioCardFrame.CardWidth * FrameHPx, px => StudioCardFrame.CardWidth = px / FrameHPx);
            CardPxSlider(c, "Margin Top", () => FrameHPx * 0.4f,
                () => StudioCardFrame.MarginTop * FrameHPx, px => StudioCardFrame.MarginTop = px / FrameHPx);
            CardPxSlider(c, "Margin Bottom", () => FrameHPx * 0.3f,
                () => StudioCardFrame.MarginBottom * FrameHPx, px => StudioCardFrame.MarginBottom = px / FrameHPx);

            _cardSizeLabel = new Label
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal, // grows to two lines once the outer-border half is shown
                    fontSize = 10, opacity = 0.7f, marginLeft = 3, marginBottom = 2
                }
            };
            c.Add(_cardSizeLabel);

            // Radius and border widths are stored per frame HEIGHT, so their px value is untouched by the
            // margins — a 24 px radius stays 24 px however the card is stretched. The slider's *range*
            // still tracks the card, since past ~a quarter of the card's height a "radius" stops meaning
            // anything (PushParams clamps there for the same reason).
            CardPxSlider(c, "Corner Radius", () => CardHPx * 0.25f,
                () => StudioCardFrame.CornerRadius * FrameHPx, px => StudioCardFrame.CornerRadius = px / FrameHPx);
            CardColor(c, "Border", () => StudioCardFrame.Border, v => StudioCardFrame.Border = v);
            CardPxSlider(c, "Inner Border Width", () => FrameHPx * 0.03f,
                () => StudioCardFrame.InnerBorderWidth * FrameHPx, px => StudioCardFrame.InnerBorderWidth = px / FrameHPx);
            CardPxSlider(c, "Outer Border Width", () => FrameHPx * 0.03f,
                () => StudioCardFrame.OuterBorderWidth * FrameHPx, px => StudioCardFrame.OuterBorderWidth = px / FrameHPx);

            // No colour here any more: the fade paints the card's own vignette/pattern at the same UV,
            // so it's a pure alpha ramp that can't drift out of sync with the card.
            c.Add(Section("Bottom fade"));
            CardPxSlider(c, "Fade Height", () => CardHPx,
                () => StudioCardFrame.FadeHeight * FrameHPx, px => StudioCardFrame.FadeHeight = px / FrameHPx);
            // Stays a 0-1 ratio, not px: it's how much of the fade height is ramp vs. solid, so it
            // scales with Fade Height by design and has no length of its own.
            CardSlider(c, "Fade Softness", 0f, 1f, () => StudioCardFrame.FadeSoftness, v => StudioCardFrame.FadeSoftness = v);

            c.Add(new Button(() =>
            {
                StudioCardFrame.ResetDefaults();
                BuildCardBody(c); // reflect reset values back into the fields
            }) { text = "Reset card defaults", style = { marginTop = 6 } });
        }

        private static void CardSlider(VisualElement c, string label, float min, float max,
            Func<float> get, Action<float> set)
        {
            var s = new Slider(label, min, max) { value = get(), showInputField = true };
            s.RegisterValueChangedCallback(e => set(e.newValue));
            c.Add(s);
        }

        // --- Card knobs in pixels ----------------------------------------------------------------
        //
        // The card's knobs are STORED as fractions of the CAPTURE, because the shader has to stay
        // resolution-independent: the same settings must export identically at 1200×800 and at 2400×1600,
        // just twice the pixels. Marketing artists work from Figma specs in pixels, so every length is
        // shown and entered as px of the current capture — a plain multiply, since the stored unit is
        // capture-relative and nothing here depends on the card's own size. That's deliberate: it's what
        // makes a 24 px radius stay 24 px when the card is stretched. `StudioCardFrame.PushParams()` does
        // the card-relative restatement the shader needs. Fractions remain the single source of truth —
        // nothing about the render path or presets changes. See §20.
        //
        // Frame = the whole capture; card = the frame minus the margins (used for slider RANGES and the
        // readout, never for a value conversion).
        private float FrameWPx => Mathf.Max(1, captureWidth);
        private float FrameHPx => Mathf.Max(1, captureHeight);
        // Note CardWPx is off FrameHPx, not FrameWPx — the card's width is stored per frame height so its
        // shape survives an aspect change (see StudioCardFrame.Layout).
        private float CardWPx => FrameHPx * Mathf.Max(0.01f, StudioCardFrame.CardWidth);
        private float CardHPx => FrameHPx * StudioCardFrame.CardHeightFraction;

        private readonly List<(Slider slider, Func<float> maxPx, Func<float> toPx)> _cardPxSync = new();
        private Label _cardSizeLabel;

        /// <summary>
        /// A card knob shown in pixels. <paramref name="maxPx"/> is a callback, not a value, because the
        /// range moves with the capture size (and, for card-relative lengths, with the margins).
        /// </summary>
        private void CardPxSlider(VisualElement c, string label, Func<float> maxPx,
            Func<float> toPx, Action<float> fromPx)
        {
            var s = new Slider($"{label} (px)", 0f, Mathf.Max(1f, maxPx())) { showInputField = true };
            s.SetValueWithoutNotify(RoundPx(toPx()));
            s.RegisterValueChangedCallback(e => fromPx(e.newValue));
            c.Add(s);
            _cardPxSync.Add((s, maxPx, toPx));
        }

        // Display only — the model keeps full float precision, and this never writes back (the sync uses
        // SetValueWithoutNotify), so rounding here can't accumulate drift.
        private static float RoundPx(float px) => Mathf.Round(px * 100f) / 100f;

        /// <summary>
        /// Re-read the px fields from the model. Needed because the same stored fraction is a different
        /// pixel count after a capture-size change (or, for the card-relative ones, a margin change), and
        /// there's no event for either. Whatever the artist is currently interacting with is skipped, so
        /// this can never fight a drag or overwrite a half-typed number.
        /// </summary>
        private void SyncCardPxFields()
        {
            foreach (var (slider, maxPx, toPx) in _cardPxSync)
            {
                if (slider.panel == null) continue; // survived a rebuild in the list but not in the UI
                var focused = slider.focusController?.focusedElement as VisualElement;
                if (focused != null && (focused == slider || slider.Contains(focused))) continue;
                slider.highValue = Mathf.Max(1f, maxPx());
                slider.SetValueWithoutNotify(RoundPx(toPx()));
            }

            if (_cardSizeLabel?.panel != null)
            {
                var text = $"Card is {CardWPx:0} × {CardHPx:0} px of a {FrameWPx:0} × {FrameHPx:0} capture.";

                // The outer border ring is painted OUTSIDE the card edge (dist ∈ (0, _OuterBorderWidth)),
                // so it genuinely enlarges the card's footprint while the fill rect above stays the same.
                // Asymmetric on purpose: the ring fades out over the top 12% (_BorderTopFade, so the head
                // can overflow), so it adds width on both sides but height only at the bottom. Uses the
                // Effective* value, i.e. after the clamp PushParams applies, so the number can't claim a
                // border wider than what actually gets drawn.
                var outerPx = StudioCardFrame.EffectiveOuterBorderWidth * FrameHPx;
                if (outerPx >= 0.5f)
                    text += $" With the {outerPx:0.#} px outer border it paints " +
                            $"{CardWPx + 2f * outerPx:0} × {CardHPx + outerPx:0} px (no ring across the top).";

                _cardSizeLabel.text = text;
            }
        }

        private static void CardColor(VisualElement c, string label, Func<Color> get, Action<Color> set,
            bool showAlpha = false)
        {
            var f = new ColorField(label) { value = get(), showAlpha = showAlpha };
            f.RegisterValueChangedCallback(e => set(e.newValue));
            c.Add(f);
        }

        // All CardColorPreset assets in the project, sorted by name (one button each).
        private static List<CardColorPreset> LoadCardPresets() =>
            AssetDatabase.FindAssets($"t:{nameof(CardColorPreset)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CardColorPreset>)
                .Where(p => p != null)
                .OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        // Push a preset's paint onto the live card frame. Each setter refreshes the frame; the
        // margins/sizes/toggles are deliberately not touched. A preset with no pattern (i.e. one
        // authored before the field existed) applies the bundled default rather than keeping whatever
        // is currently set — a preset should fully determine the look, not half-inherit it.
        private static void ApplyCardPreset(CardColorPreset p)
        {
            StudioCardFrame.DclInnerColor = p.cardInner;
            StudioCardFrame.DclOuterColor = p.cardOuter;
            StudioCardFrame.Border = p.border;
            StudioCardFrame.PatternTexture = p.pattern; // may provisionally flip PatternEnabled if null
            StudioCardFrame.PatternEnabled = p.patternEnabled; // preset's actual on/off wins
            StudioCardFrame.GlowColor = p.glowColor;
            StudioCardFrame.GlowRadius = p.glowRadius;
        }

        // Create an "Assets/…"-relative folder (and any missing parents) if it doesn't exist yet.
        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        // One button per single-frame GLB in StreamingAssets/poses/. Clicking sets the pose as the
        // active emote (it holds its one frame in play mode — where stills are captured). The active
        // pose's button is disabled (= selected), same convention as the shader buttons. Rebuilt in
        // place so the selection highlight and a fresh folder scan (⟳) both refresh live.
        private void BuildPoseButtons(VisualElement grid)
        {
            grid.Clear();

            var names = GetPoseNames();
            foreach (var name in names)
            {
                var emoteName = $"{POSES_EMBEDDED_PREFIX}/{name}";
                var btn = new Button(() =>
                {
                    outfit.emote = emoteName;
                    RemoveDraftEmote(); // an equipped draft emote would override the pose
                    _poseLabel.text = $"Pose: {name}";
                    SyncEmotePopup();
                    RememberItemPose(); // becomes the default for this item's category
                    RefreshShareCode();
                    // Play mode: pose ONLY the currently-loaded avatar (which may be a Random Profile
                    // from the Debug tab) without reloading the custom outfit. Edit mode: assemble the
                    // outfit + pose onto the preview skeleton as before.
                    if (Application.isPlaying)
                        ApplyPoseOnly(emoteName);
                    else
                        ScheduleApply();
                    BuildPoseButtons(grid); // refresh the selected-highlight
                }) { text = name, style = { marginRight = 2, marginBottom = 2 } };
                btn.SetEnabled(outfit.emote != emoteName); // disabled = selected
                grid.Add(btn);
            }

            if (names.Count == 0)
                grid.Add(new Label($"No poses — drop single-frame GLBs in Assets/{POSES_DIR_UNDER_ASSETS}/")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Italic, opacity = 0.7f, marginRight = 4 }
                });

            grid.Add(new Button(() => BuildPoseButtons(grid))
            {
                text = "⟳",
                tooltip = "Rescan the poses folder",
                style = { marginBottom = 2 }
            });
        }

        // Base names (no extension) of the pose GLBs in Assets/OutfitStudio/Poses/, sorted.
        // Editor-time file scan (Application.dataPath = <project>/Assets), valid outside play mode.
        private static List<string> GetPoseNames()
        {
            var dir = System.IO.Path.Combine(Application.dataPath, POSES_DIR_UNDER_ASSETS);
            if (!System.IO.Directory.Exists(dir)) return new List<string>();

            return System.IO.Directory.GetFiles(dir, "*.glb")
                .Select(System.IO.Path.GetFileNameWithoutExtension)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static Label Header(string text)
        {
            return new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 10,
                    marginBottom = 2
                }
            };
        }

        private ColorField ColorRow(VisualElement parent, string label, Color initial, Action<Color> setter)
        {
            var field = new ColorField(label) { value = initial, showAlpha = false };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                RefreshShareCode();
                ScheduleApply();
            });
            parent.Add(field);
            return field;
        }

        private void RefreshSlots()
        {
            if (_slotsContainer == null) return;

            // Before the rebuild, so the banner and the ◉ states below describe reconciled state. Hides the
            // framing section directly rather than through RefreshIsolation, which would call back into
            // here and clear the container mid-rebuild (see ReconcileSoloSelection).
            if (ReconcileSoloSelection())
            {
                if (_framingSection != null) _framingSection.style.display = DisplayStyle.None;
                _syncFramingFields?.Invoke();
                SetStatus("Isolation cleared — that wearable is no longer equipped");
            }

            _slotsContainer.Clear();

            BuildIsolationBanner();
            BuildHidingControls();

            // Categories that got their own row, so BuildHiddenBodyRows can cover the rest
            var rowCategories = new HashSet<string>();

            // Draft (builder collection) items — shown above the catalog ones
            foreach (var base64 in outfit.base64Items.ToList())
            {
                var (name, category, isEmote) = DescribeDraft(base64);
                if (isEmote) continue; // the pose row covers draft emotes

                rowCategories.Add(category);

                var row = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 }
                };

                MarkIsolationState(row, outfit.soloItem && outfit.soloBase64 == base64);

                row.Add(new Label($"[{category}] {name} (draft)")
                {
                    style = { flexGrow = 1, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, marginLeft = 4 }
                });

                AddHidingBadge(row, category);
                AddSoloButton(row, null, base64, category, $"{name} (draft)");

                row.Add(new Button(() =>
                {
                    outfit.base64Items.Remove(base64);
                    RefreshSlots();
                    RefreshShareCode();
                    ScheduleApply();
                }) { text = "✕" });

                _slotsContainer.Add(row);
            }

            if (outfit.urns.Count == 0 && outfit.base64Items.Count == 0)
            {
                _slotsContainer.Add(new Label("No wearables equipped — pick items from the browser")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Italic, marginTop = 4, marginBottom = 4 }
                });
                return;
            }

            foreach (var urn in outfit.urns.ToList())
            {
                var row = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 }
                };

                MarkIsolationState(row, outfit.soloItem && outfit.soloUrn == urn);

                var known = _knownItems.GetValueOrDefault(urn);

                var thumb = new Image { scaleMode = ScaleMode.ScaleToFit, style = { width = 24, height = 24 } };
                row.Add(thumb);
                if (known != null)
                {
                    LoadThumbnail(known.thumbnail, tex =>
                    {
                        if (tex != null) thumb.image = tex;
                    });
                }

                var slot = known?.Slot ?? "?";
                rowCategories.Add(slot);

                var name = known?.name ?? urn[(urn.LastIndexOf(':') + 1)..];
                row.Add(new Label($"[{slot}] {name}")
                {
                    tooltip = urn,
                    style = { flexGrow = 1, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, marginLeft = 4 }
                });

                AddHidingBadge(row, slot);
                AddSoloButton(row, urn, null, slot, name);

                row.Add(new Button(() =>
                {
                    outfit.urns.Remove(urn);
                    RefreshSlots(); // reconciles isolation away if this was the isolated row
                    RefreshShareCode();
                    ScheduleApply();
                }) { text = "✕" });

                _slotsContainer.Add(row);
            }

            BuildHiddenBodyRows(rowCategories);
        }

        // ---------------------------------------------------------------- Isolation, on the rows

        /// <summary>
        /// Says which wearable is being shot alone and offers the way back, at the top of the list it's
        /// describing. Styled like the hide-override warning below it, for the same reason: the list is
        /// showing something other than what will render, and that has to be impossible to miss.
        /// </summary>
        private void BuildIsolationBanner()
        {
            if (!outfit.soloItem) return;

            var banner = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 }
            };

            banner.Add(new Label($"Isolating {DescribeSoloItem()} — only this item renders; "
                                 + "the rest of the outfit is kept but hidden.")
            {
                style =
                {
                    flexGrow = 1,
                    unityFontStyleAndWeight = FontStyle.Italic,
                    color = new Color(0.85f, 0.6f, 0.2f),
                    whiteSpace = WhiteSpace.Normal
                }
            });

            banner.Add(new Button(ExitIsolation) { text = "Show full avatar" });

            _slotsContainer.Add(banner);
        }

        /// <summary>
        /// Marks a row as the isolated one (green left accent) or, while something else is isolated, dims
        /// it — those rows genuinely aren't rendering, and saying so on the row is what keeps "where did my
        /// outfit go" from being a support question.
        /// </summary>
        private void MarkIsolationState(VisualElement row, bool isolated)
        {
            if (isolated)
            {
                row.style.borderLeftWidth = 2;
                row.style.borderLeftColor = new Color(0.45f, 0.85f, 0.45f);
                row.style.paddingLeft = 4;
            }
            else if (outfit.soloItem)
            {
                row.style.opacity = 0.45f;
            }
        }

        /// <summary>
        /// The per-row isolate toggle: shoot this one wearable with the body hidden, or go back to the full
        /// avatar. Same 20px active/inactive vocabulary as the force-render button next to it
        /// (<see cref="AddHidingBadge"/>) — green and bold means it's on.
        /// </summary>
        private void AddSoloButton(VisualElement row, string urn, string base64, string category, string name)
        {
            var isolated = outfit.soloItem
                           && (urn != null ? outfit.soloUrn == urn : outfit.soloBase64 == base64);

            var button = new Button
            {
                text = "◉",
                tooltip = isolated
                    ? "Isolating this wearable. Click to go back to the full avatar."
                    : "Isolate for a shot: render only this wearable with the body hidden. It stays on "
                      + "the skeleton, so it still poses. The rest of your outfit is kept, just not "
                      + "rendered.",
                style =
                {
                    width = 20,
                    color = isolated ? new Color(0.45f, 0.85f, 0.45f) : Color.gray,
                    unityFontStyleAndWeight = isolated ? FontStyle.Bold : FontStyle.Normal
                }
            };
            button.clicked += () =>
            {
                if (isolated) ExitIsolation();
                else IsolateItem(urn, base64, category, name);
            };
            row.Add(button);
        }

        // ---------------------------------------------------------------- Hide overrides (forceRender)

        /// <summary>
        /// The master "ignore all hides" switch plus the warning that share codes can't carry hide
        /// overrides. Per-slot toggles live on the rows themselves (see <see cref="AddHidingBadge"/>).
        /// </summary>
        private void BuildHidingControls()
        {
            var master = new Toggle("Ignore all hides")
            {
                value = outfit.ignoreAllHides,
                tooltip = "Force-renders every category, so no wearable can hide another.\n\n" +
                          "Body parts displaced by a wearable in the same slot (bare feet under shoes) " +
                          "stay hidden regardless — that's slot occupancy, not a hide. Expect the " +
                          "geometry these hides existed to avoid: doubled hands on older upper bodies, " +
                          "everything poking through a skin."
            };
            master.RegisterValueChangedCallback(evt =>
            {
                outfit.ignoreAllHides = evt.newValue;
                RefreshSlots();
                ScheduleApply();
            });
            _slotsContainer.Add(master);

            if (!outfit.HasForceRenderOverrides) return;

            _slotsContainer.Add(new Label("Hide overrides are preset-only — share codes and the web " +
                                          "renderer load with the hides back on.")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Italic,
                    color = new Color(0.85f, 0.6f, 0.2f),
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 2
                }
            });
        }

        /// <summary>
        /// Rows for categories that are hidden but have no equipped item of their own — body geometry
        /// like <c>hands</c> and <c>head</c>. Without these there'd be no way to reach the most common
        /// hide of all: an older upper body suppressing the body's hands (AvatarUtils.ShouldHideHands),
        /// since "hands" is never a slot in the outfit list.
        /// </summary>
        private void BuildHiddenBodyRows(HashSet<string> rowCategories)
        {
            // Force-rendered categories stay listed even though they're no longer hidden — otherwise
            // forcing one removes the only row carrying its toggle and it can never be un-forced.
            var bodyOnly = OutfitHidingReport.HiddenBy.Keys
                .Concat(outfit.forceRender)
                .Distinct()
                .Where(category => !rowCategories.Contains(category))
                .OrderBy(category => category)
                .ToList();

            if (bodyOnly.Count == 0) return;

            _slotsContainer.Add(new Label("Hidden body parts")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 }
            });

            foreach (var category in bodyOnly)
            {
                var row = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 }
                };

                row.Add(new Label($"[{category}]")
                {
                    style = { flexGrow = 1, marginLeft = 4, color = Color.gray }
                });

                AddHidingBadge(row, category);
                _slotsContainer.Add(row);
            }
        }

        /// <summary>
        /// Adds the "hidden by &lt;category&gt;" label and the per-slot force-render toggle to a slot row.
        ///
        /// The hidden set comes from <see cref="OutfitHidingReport"/>, which runs the renderer's own
        /// hiding rules over the outfit, so the badge reflects what the loader would do rather than a
        /// guess. It's recomputed per apply, so a freshly equipped item is badged when the debounced
        /// apply completes rather than on the click.
        /// </summary>
        private void AddHidingBadge(VisualElement row, string category)
        {
            if (string.IsNullOrEmpty(category) || category == "?") return;

            var forced = outfit.forceRender.Contains(category);
            var hiddenBy = OutfitHidingReport.HiddenBy.GetValueOrDefault(category);

            if (hiddenBy != null)
            {
                row.Add(new Label($"hidden by {hiddenBy}")
                {
                    tooltip = $"The equipped {hiddenBy} lists {category} in its hides/replaces, " +
                              "so this item is not rendered.",
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Italic,
                        color = new Color(0.85f, 0.6f, 0.2f),
                        marginRight = 4
                    }
                });
            }
            else if (forced)
            {
                // Nothing is hiding it anymore *because* it's forced — say so, otherwise the lit
                // toggle looks like it's doing nothing.
                row.Add(new Label("forced")
                {
                    tooltip = $"{category} is force-rendered, so other wearables can't hide it.",
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Italic,
                        color = new Color(0.45f, 0.75f, 0.45f),
                        marginRight = 4
                    }
                });
            }

            var toggle = new Button
            {
                text = "F",
                tooltip = outfit.ignoreAllHides
                    ? "Every category is already force-rendered by \"Ignore all hides\"."
                    : $"Force-render {category}: keep it visible even when another wearable hides it.\n\n" +
                      "Only un-hides this category — if two items hide each other, force both.",
                style =
                {
                    width = 20,
                    color = forced ? new Color(0.45f, 0.85f, 0.45f) : Color.gray,
                    unityFontStyleAndWeight = forced ? FontStyle.Bold : FontStyle.Normal
                }
            };
            // The master switch already forces everything; leaving these live would let a click
            // toggle a list entry with no visible effect.
            toggle.SetEnabled(!outfit.ignoreAllHides);
            toggle.clicked += () =>
            {
                if (forced) outfit.forceRender.Remove(category);
                else outfit.forceRender.Add(category);
                RefreshSlots();
                ScheduleApply();
            };
            row.Add(toggle);
        }

        private void RefreshShareCode()
        {
            _shareCodeField?.SetValueWithoutNotify(outfit.ToShareCode());
        }

        // ---------------------------------------------------------------- Draft (builder) items

        private readonly Dictionary<string, (string name, string category, bool isEmote)> _draftDescriptions = new();

        /// <summary>Reads name/category/type from a base64 RawActiveEntity without full parsing.</summary>
        private (string name, string category, bool isEmote) DescribeDraft(string base64)
        {
            if (_draftDescriptions.TryGetValue(base64, out var cached)) return cached;

            (string, string, bool) description;
            try
            {
                var json = JObject.Parse(Encoding.UTF8.GetString(OutfitDefinition.DecodeBase64(base64)));
                var isEmote = json["emoteDataADR74"] is JObject;
                description = (
                    json["name"]?.Value<string>() ?? "draft item",
                    isEmote ? "emote" : json["data"]?["category"]?.Value<string>() ?? "?",
                    isEmote);
            }
            catch
            {
                description = ("invalid draft item", "?", false);
            }

            _draftDescriptions[base64] = description;
            return description;
        }

        private void RemoveDraftEmote() =>
            outfit.base64Items.RemoveAll(base64 => DescribeDraft(base64).isEmote);

        private void LoadOutfit(OutfitDefinition loaded)
        {
            // Nothing loadable carries isolation — presets go through CloneForPreset (solo fields
            // defaulted) and a share code can't express it at all — so the load silently drops it. That
            // means ReconcileSoloSelection never sees anything to reconcile, and the camera FrameItem
            // parked on the item would stay parked while a full avatar loaded into it. Hand it back here.
            var wasIsolated = outfit.soloItem;

            outfit = loaded;

            if (wasIsolated && !outfit.soloItem) StudioItemCamera.Release();

            // Face features live in outfit.urns now, so the grid reflects whatever was just loaded.
            // Off-chain URNs in slots not browsed this session resolve asynchronously via
            // HydrateKnownItems below, which refreshes the grid again once they land.
            RefreshFaceGrid();

            // `loaded` replaces `outfit` wholesale, so the framing section's visibility and its build-once
            // controls both have to be re-synced against the new instance.
            RefreshIsolation();

            _bodyShapePopup.SetValueWithoutNotify(
                outfit.bodyShape == WearablesConstants.BODY_SHAPE_FEMALE ? "Female" : "Male");
            _skinField.SetValueWithoutNotify(outfit.skinColor);
            _hairField.SetValueWithoutNotify(outfit.hairColor);
            _eyeField.SetValueWithoutNotify(outfit.eyeColor);
            _poseLabel.text = $"Pose: {PoseDisplayName(outfit.emote)}";
            SyncEmotePopup();

            HydrateKnownItems();
            RefreshSlots();
            RefreshShareCode();

            // Not via ScheduleApply: with auto-apply off nothing else would recompute, leaving the
            // badges describing the outfit that was loaded before this one.
            OutfitHidingReport.Refresh(outfit);

            ScheduleApply();
        }

        /// <summary>
        /// Keeps the "Embedded" popup's displayed value truthful whenever outfit.emote changes from
        /// somewhere other than the popup itself (pose buttons, draft/catalog emote picks, loading a
        /// share code or preset). Falls back to <see cref="EMBEDDED_EMOTE_NONE"/> for anything that
        /// isn't a literal EMBEDDED_EMOTES entry (poses, URNs) so the popup never silently shows a
        /// stale emote while something else is actually loaded/playing.
        /// </summary>
        private void SyncEmotePopup() =>
            _emotePopup?.SetValueWithoutNotify(
                outfit.emote == TPOSE_EMOTE ? TPOSE_LABEL :
                EMBEDDED_EMOTES.Contains(outfit.emote) ? outfit.emote : EMBEDDED_EMOTE_NONE);

        /// <summary>What the "Pose:" line calls the current emote. Everything reads as itself except
        /// the neutral T-pose, whose emote value is a relative file path nobody wants to read.</summary>
        private static string PoseDisplayName(string emote) => emote == TPOSE_EMOTE ? TPOSE_LABEL : emote;

        /// <summary>
        /// Resolves names/thumbnails for URNs we don't have catalog info for
        /// (pasted share codes, presets, domain reloads). Two sources, because neither covers the
        /// other: the marketplace API serves collection items only, while off-chain base avatars —
        /// the face features the Avatar tab equips — exist solely on the Catalyst.
        /// </summary>
        private void HydrateKnownItems()
        {
            // soloUrn is always one of outfit.urns now (see ReconcileSoloSelection), so it needs no entry
            // of its own — its row is what carries the name/thumbnail/slot.
            var unknown = outfit.urns.Append(outfit.emote)
                .Where(urn => !string.IsNullOrEmpty(urn)
                              && urn.StartsWith("urn:", StringComparison.OrdinalIgnoreCase)
                              && !_knownItems.ContainsKey(urn))
                .Distinct()
                .ToArray();

            if (unknown.Length == 0) return;

            var offChain = unknown.Where(urn => urn.Contains(":off-chain:")).ToArray();
            if (offChain.Length > 0) HydrateOffChainItems(offChain);

            var marketplace = unknown.Where(urn => !urn.Contains(":off-chain:")).ToArray();
            if (marketplace.Length == 0) return;

            CatalogService.Search(new CatalogQuery { Urns = marketplace, First = marketplace.Length },
                page =>
                {
                    foreach (var item in page.data)
                        _knownItems[item.urn] = item;

                    // RefreshIsolation, not just RefreshSlots: the Framing header names the isolated item,
                    // and this lookup is where a preset-loaded URN's name and slot come from.
                    RefreshIsolation();
                },
                error => Debug.LogWarning($"[OutfitStudio] Failed to resolve URNs: {error}"));
        }

        /// <summary>
        /// Resolves off-chain (base-avatar) URNs via the Catalyst so face features carried by a preset
        /// or share code get a slot, name and thumbnail even in categories the Avatar tab hasn't
        /// browsed this session — without this they'd stay unknown, skipping the one-per-slot rule and
        /// the body-shape representation guard. Async void, same editor-only pattern as
        /// <see cref="RunFaceSearch"/>.
        /// </summary>
        private async void HydrateOffChainItems(string[] urns)
        {
            EntityDefinition[] entities;
            try
            {
                entities = await EntityService.GetEntities((string[])urns.Clone());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OutfitStudio] Failed to resolve off-chain URNs: {e.Message}");
                return;
            }

            RegisterCatalystEntities(entities);
            RefreshIsolation(); // rebuilds the slot list too; the Framing header names the isolated item
            RefreshFaceGrid(); // the selected-tile highlight depends on the slot we just resolved
        }

        // ---------------------------------------------------------------- Apply

        private void ScheduleApply()
        {
            if (!autoApply) return;

            _pendingApply?.Pause();
            _pendingApply = rootVisualElement.schedule.Execute(Apply);
            _pendingApply.StartingIn(400);
        }

        private void Apply()
        {
            // Badges come from the outfit, not from the loaded avatar, so they're computed for both
            // preview paths (and stay correct if a Random Profile is loaded in play mode afterwards).
            OutfitHidingReport.Refresh(outfit);

            if (!Application.isPlaying)
            {
                // Edit-mode 3D preview: assembles onto the scene skeleton without play mode.
                // Pose/emote playback and capture still require play mode.
                EditModeAvatarPreview.Apply(outfit, SetStatus);
                return;
            }

            var previewController = FindPreviewController();
            if (previewController == null)
            {
                SetStatus("No PreviewController in the scene — open Assets/Scenes/Main.unity", true);
                return;
            }

            var config = PreviewConfiguration.Instance;
            config.SetMode("builder");
            config.BodyShape = outfit.bodyShape;
            config.Urns = FilterForBodyShape(outfit.EffectiveUrns()).Select(URNUtils.SanitizeURN).ToList();
            config.SetSkinColor(ColorUtility.ToHtmlStringRGB(outfit.skinColor));
            config.SetHairColor(ColorUtility.ToHtmlStringRGB(outfit.hairColor));
            config.SetEyeColor(ColorUtility.ToHtmlStringRGB(outfit.eyeColor));
            config.Emote = string.IsNullOrEmpty(outfit.emote) ? "idle" : outfit.emote;
            config.ForceRender = outfit.EffectiveForceRender();

            // Single-Item mode: body geometry is dropped after the load (PreviewController honours this
            // right after LoadAvatar). The skeleton stays, so the item still skins and poses.
            config.HideBodyShape = outfit.soloItem;

            // Draft (builder) items — LoadForBuilder gives base64 per-category priority
            // and a base64 emote overrides the pose
            config.Base64.Clear();
            foreach (var base64 in outfit.EffectiveBase64Items())
            {
                try
                {
                    config.AddBase64(base64);
                }
                catch (Exception e)
                {
                    SetStatus($"Invalid draft item skipped: {e.Message}", true);
                }
            }

            previewController.gameObject.SetActive(true);
            previewController.InvokeReload();

            ScheduleFrameItem();

            SetStatus(outfit.soloItem ? "Item applied" : "Outfit applied");
        }

        /// <summary>
        /// Play-mode pose/animation change that does NOT reload the custom outfit: sets only
        /// <c>config.Emote</c> and reloads, so whatever avatar is loaded keeps its identity and just
        /// changes emote (the AvatarLoader diffs the unchanged wearables, so only the emote reloads).
        /// Shared by the pose buttons and the "Embedded" emote popup — either way the currently-loaded
        /// avatar (which may be a Random Profile from the Debug tab) gets re-posed/re-animated in
        /// place instead of switching to the studio's custom Builder outfit.
        ///
        /// Mode handling: <b>Builder</b> (the custom outfit) is kept as-is. <b>Any other</b> mode is
        /// switched to <b>Profile</b> — because Jesus mode hard-codes its emote (<c>Particles_Anim</c>,
        /// the arms-out "jesus" pose) and Marketplace shows a wearable, both ignoring
        /// <c>config.Emote</c>; Profile mode applies it. <c>config.Profile</c> is preserved, so a
        /// Random Profile stays the same avatar, now posed/animated. Edit mode routes through Apply.
        /// </summary>
        private void ApplyPoseOnly(string emoteName)
        {
            var pc = FindPreviewController();
            if (pc == null)
            {
                SetStatus("No PreviewController in the scene", true);
                return;
            }

            var config = PreviewConfiguration.Instance;
            config.Emote = string.IsNullOrEmpty(emoteName) ? "idle" : emoteName;

            // Keep the custom outfit (Builder); otherwise pose the current profile avatar in Profile
            // mode, where config.Emote is actually applied (Jesus/Marketplace ignore it).
            if (config.Mode != PreviewMode.Builder)
                config.SetMode("profile");

            // Loop in Profile mode, matching how Builder mode already always loops embedded emotes
            // (ResolveBuilderEmote hardcodes loop:true) — without this a single-frame pose would end
            // instantly and revert to the base idle, and a multi-frame animation wouldn't hold either.
            config.EmoteLoop = true;

            pc.gameObject.SetActive(true);
            pc.InvokeReload();

            // Deliberately NO re-framing here. A pose does change the item's bounds — an outstretched
            // arm inflates them enormously — but re-fitting to that means the camera lurches every time
            // the artist tries a pose, and the garment itself changes apparent size by ~1.6x between
            // arms-down and arm-out. Framing belongs to the item, not the pose; use "Frame item" once
            // the pose is chosen.

            SetStatus("Applied to the loaded avatar");
        }

        /// <summary>
        /// Drops wearables that have no representation for the selected body shape (the loader
        /// would throw). Only known catalog items can be checked; unknown URNs pass through.
        /// </summary>
        private List<string> FilterForBodyShape(IEnumerable<string> urns)
        {
            var shapeName = outfit.bodyShape == WearablesConstants.BODY_SHAPE_FEMALE ? "BaseFemale" : "BaseMale";
            var result = new List<string>();

            foreach (var urn in urns)
            {
                var known = _knownItems.GetValueOrDefault(urn);
                var bodyShapes = known?.data?.wearable?.bodyShapes;

                if (known != null && bodyShapes is { Length: > 0 } && !bodyShapes.Contains(shapeName))
                {
                    SetStatus($"Skipped {known.name}: no {shapeName} representation", true);
                    continue;
                }

                result.Add(urn);
            }

            return result;
        }

        private static PreviewController FindPreviewController() =>
            FindAnyObjectByType<PreviewController>(FindObjectsInactive.Include);

        private static void WithPreview(Action<PreviewController> action)
        {
            if (!Application.isPlaying) return;
            var pc = FindPreviewController();
            if (pc != null) action(pc);
        }

        // ---------------------------------------------------------------- Capture

        private bool EnsurePlaying()
        {
            if (Application.isPlaying) return true;
            SetStatus("Enter play mode first", true);
            return false;
        }

        private void CaptureStill()
        {
            if (!EnsurePlaying()) return;

            SetStatus("Capturing...");
            OutfitCapture.CaptureStill(captureWidth, captureHeight, transparentBackground, outputFolder,
                captureUpsample, path =>
            {
                if (path != null)
                {
                    SetStatus($"Saved {path}");
                    OutfitCapture.RevealInFinder(path);
                }
                else
                {
                    SetStatus("Capture failed", true);
                }
            });
        }

        private void ToggleVideo()
        {
            if (OutfitCapture.IsRecording)
            {
                OutfitCapture.StopVideo();
                _videoButton.text = "⏺  Start Video";
                SetStatus("Video saved");
                return;
            }

            if (!EnsurePlaying()) return;

            OutfitCapture.StartVideo(captureWidth, captureHeight, captureFrameRate, outputFolder);
            _videoButton.text = "⏹  Stop Video";
            SetStatus("Recording...");
        }

        private void RecordEmote()
        {
            if (!EnsurePlaying() || OutfitCapture.IsRecording) return;

            var pc = FindPreviewController();
            var length = pc != null ? pc.GetEmoteLength() : 0f;

            if (length <= 0f)
            {
                SetStatus("No emote loaded — pick a pose first", true);
                return;
            }

            OutfitCapture.StartVideo(captureWidth, captureHeight, captureFrameRate, outputFolder);
            pc.PlayEmote();
            SetStatus($"Recording emote ({length:0.0}s)...");

            rootVisualElement.schedule.Execute(() =>
            {
                OutfitCapture.StopVideo();
                _videoButton.text = "⏺  Start Video";
                SetStatus("Emote video saved");
            }).StartingIn((long)((length + 0.5f) * 1000));
        }

        private void RecordTurntable()
        {
            if (!EnsurePlaying() || OutfitCapture.IsRecording) return;

            var avatarLoader = FindAnyObjectByType<AvatarLoader>();
            if (avatarLoader == null)
            {
                SetStatus("No avatar loaded", true);
                return;
            }

            var rotator = avatarLoader.GetComponentInParent<DragRotator>();
            var target = rotator != null ? rotator.gameObject : avatarLoader.gameObject;

            var driver = target.GetComponent<TurntableDriver>();
            if (driver == null) driver = target.AddComponent<TurntableDriver>();

            driver.enabled = false;
            driver.Duration = turntableDuration;
            driver.Completed += () =>
            {
                OutfitCapture.StopVideo();
                _videoButton.text = "⏺  Start Video";
                SetStatus("Turntable video saved");
            };

            OutfitCapture.StartVideo(captureWidth, captureHeight, captureFrameRate, outputFolder);
            driver.enabled = true;
            SetStatus($"Recording turntable ({turntableDuration:0.0}s)...");
        }

        private void SnapRotate(float deltaDegrees)
        {
            if (!EnsurePlaying()) return;

            var avatarLoader = FindAnyObjectByType<AvatarLoader>();
            var rotator = avatarLoader != null ? avatarLoader.GetComponentInParent<DragRotator>() : null;
            if (rotator == null)
            {
                SetStatus("No avatar loaded", true);
                return;
            }

            rotationSnapAngle += deltaDegrees;
            _rotationLabel.text = $"{rotationSnapAngle:0}°";
            rotator.SnapRotation(rotationSnapAngle);
        }

        /// <summary>Turns the head bone toward the camera (or lets the current pose/emote drive
        /// it again), independent of body rotation. Returns false (and leaves the toggle
        /// unapplied) when the head bone/avatar/camera can't be resolved.</summary>
        /// <summary>Rotates the head bone to face the camera, giving the neck bone a share of
        /// the turn (see <see cref="NECK_LOOK_SHARE"/>) so it doesn't read as the head twisting
        /// on its own. One-shot: freezes the current pose first (<see cref="AvatarLoader.FreezePose"/>)
        /// since the legacy Animation component would otherwise re-drive these bones back to the
        /// clip pose on the very next frame, undoing the adjustment.</summary>
        private void LookAtCamera()
        {
            if (!EnsurePlaying()) return;

            var avatarLoader = FindAnyObjectByType<AvatarLoader>();
            if (avatarLoader == null)
            {
                SetStatus("No avatar loaded", true);
                return;
            }

            var headBone = avatarLoader.HeadBone;
            if (headBone == null)
            {
                SetStatus("Head bone not found", true);
                return;
            }

            var camera = avatarLoader.MainCamera;
            var direction = camera.transform.position - headBone.position;
            if (direction.sqrMagnitude < 0.0001f) return;

            avatarLoader.FreezePose();

            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);

            var neckBone = avatarLoader.NeckBone;
            if (neckBone != null)
            {
                var remainingTurn = targetRotation * Quaternion.Inverse(headBone.rotation);
                neckBone.rotation = Quaternion.Slerp(Quaternion.identity, remainingTurn, NECK_LOOK_SHARE) *
                                     neckBone.rotation;
            }

            headBone.rotation = targetRotation;
            SetStatus("Head looking at camera");
        }

        // ---------------------------------------------------------------- Misc

        private void SetStatus(string message, bool error = false)
        {
            if (_statusLabel == null) return;
            _statusLabel.text = message;
            _statusLabel.style.color = error ? new Color(1f, 0.45f, 0.4f) : new Color(0.65f, 0.65f, 0.65f);
            if (error) Debug.LogWarning($"[OutfitStudio] {message}");
        }
    }
}
