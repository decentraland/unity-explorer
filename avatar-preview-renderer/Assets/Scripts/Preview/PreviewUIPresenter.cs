using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Data;
using UI.Manipulators;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Utils;
using Random = UnityEngine.Random;

namespace Preview
{
    [RequireComponent(typeof(UIDocument))]
    public class PreviewUIPresenter : MonoBehaviour
    {
        private const string USS_SWITCHER_BUTTON_SELECTED = "switcher__button--selected";
        private const float LOADER_SPEED = 360f;
        private const string DEBUG_PASSPHRASE = "debugmesilly";

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private PreviewCameraController previewCameraController;

        public event Action ShowAvatarClicked;
        public event Action ShowWearableClicked;
        public event Action<bool> EmoteToggleClicked;
        public event Action<Vector2, float> ContainerDrag;

        private VisualElement _switcher;
        private VisualElement _wearableButton;
        private VisualElement _avatarButton;

        private VisualElement _zoomControls;
        private Button _zoomInButton;
        private Button _zoomOutButton;

        private VisualElement _emoteControls;
        private Button _playEmoteButton;
        private Label _playEmoteLabel;
        private Button _muteEmoteButton;

        private VisualElement _controls;
        private VisualElement _loader;
        private VisualElement _loaderIcon;

        private string _currentDebugInput = "";
        private bool _debugLoaded;
        private bool _zoomEnabled;
        private bool _animationPlaying = true;
        private SwitcherState _switcherState = SwitcherState.Wearable;
        private float _lastPlayPauseClickTime;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _switcher = root.Q("Switcher");
            _wearableButton = _switcher.Q("WearableButton");
            _avatarButton = _switcher.Q("AvatarButton");

            _zoomControls = root.Q("ZoomControls");
            _zoomInButton = _zoomControls.Q<Button>("ZoomInButton");
            _zoomOutButton = _zoomControls.Q<Button>("ZoomOutButton");
            _zoomInButton.clicked += previewCameraController.ZoomIn;
            _zoomOutButton.clicked += previewCameraController.ZoomOut;

            _emoteControls = root.Q("EmoteControls");
            _playEmoteButton = _emoteControls.Q<Button>("PlayStopButton");
            _playEmoteLabel = _playEmoteButton.Q<Label>("Title");
            _muteEmoteButton = _emoteControls.Q<Button>("MuteButton");
            _playEmoteButton.clicked += OnPlayPauseEmoteClicked;
            _muteEmoteButton.clicked += OnMuteEmoteClicked;

            _controls = root.Q("Controls");
            _loader = root.Q("Loader");
            _loaderIcon = _loader.Q("Icon");

            _controls.AddManipulator(new DragManipulator((d, dt) => ContainerDrag!(d, dt)));
            _controls.RegisterCallback<WheelEvent>(OnWheel);
            _wearableButton.AddManipulator(new Clickable(OnWearableButtonClicked));
            _avatarButton.AddManipulator(new Clickable(OnAvatarButtonClicked));

            if (Application.isEditor)
            {
                EnableDebug();
            }

            Keyboard.current.onTextInput += OnTextInput;

            ShowLoader(true);
        }

        private void Update()
        {
            // Rotate the loader icon
            _loaderIcon.RotateBy(LOADER_SPEED * Time.deltaTime);
        }

        public void EnableSwitcher(bool enable)
        {
            _switcher.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void EnableZoom(bool enable)
        {
            _zoomEnabled = enable;
            _zoomControls.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void EnableEmoteControls(bool enable)
        {
            _emoteControls.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void EnableAudioControls(bool enable)
        {
            _muteEmoteButton.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void EnableLoader(bool enable)
        {
            _loaderIcon.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ShowLoader(bool enable)
        {
            _loader.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
            _controls.style.display = enable ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void SetSwitcherState(SwitcherState state, BodyShape? currentBodyShape = null)
        {
            _switcherState = state;

            if (currentBodyShape.HasValue)
            {
                _switcher.tooltip =
                    state == SwitcherState.WearableLocked
                        ? $"This wearable does not have a <b>{currentBodyShape.Value.ToString()}</b>\nrepresentation"
                        : string.Empty;
            }

            _wearableButton.EnableInClassList(USS_SWITCHER_BUTTON_SELECTED,
                state is SwitcherState.Wearable or SwitcherState.WearableLocked);
            _avatarButton.EnableInClassList(USS_SWITCHER_BUTTON_SELECTED, state is SwitcherState.Avatar);
            _avatarButton.SetEnabled(state is not SwitcherState.WearableLocked);
        }

        public void SetAnimationPlaying(bool playing)
        {
            _animationPlaying = playing;

            _playEmoteButton.EnableInClassList("emote-controls__button-play--stopped", !_animationPlaying);
            _playEmoteLabel.text = _animationPlaying ? "STOP EMOTE" : "PLAY EMOTE";
        }

        private void OnAvatarButtonClicked()
        {
            if (_switcherState == SwitcherState.WearableLocked) return;

            SetSwitcherState(SwitcherState.Avatar);
            ShowAvatarClicked!();
        }

        private void OnWearableButtonClicked()
        {
            if (_switcherState == SwitcherState.WearableLocked) return;

            SetSwitcherState(SwitcherState.Wearable);
            ShowWearableClicked!();
        }

        private void OnMuteEmoteClicked()
        {
            audioSource.mute = !audioSource.mute;
            _muteEmoteButton.EnableInClassList("emote-controls__button-mute--muted", audioSource.mute);
        }

        private void OnPlayPauseEmoteClicked()
        {
            if (Time.time - _lastPlayPauseClickTime < 0.5f) return;

            _lastPlayPauseClickTime = Time.time;

            SetAnimationPlaying(!_animationPlaying);
            EmoteToggleClicked!(_animationPlaying);
        }

        private void OnWheel(WheelEvent evt)
        {
            if (!_zoomEnabled) return;

            previewCameraController.ZoomByWheelDelta(evt.delta.y);
            evt.StopPropagation();
        }

        private void OnTextInput(char c)
        {
            _currentDebugInput += c;

            if (!DEBUG_PASSPHRASE.StartsWith(_currentDebugInput))
            {
                _currentDebugInput = string.Empty;
                return;
            }

            if (_currentDebugInput.Equals(DEBUG_PASSPHRASE))
            {
                EnableDebug();
                _currentDebugInput = "";
            }
        }

        private void EnableDebug()
        {
            var debugPanel = GetComponent<UIDocument>().rootVisualElement.Q("DebugPanel");
            debugPanel.style.display = DisplayStyle.Flex;

            if (_debugLoaded) return;

            // @formatter:off
            var dropdownOptions = new List<(string name, string url)>
            {
                ("From URL", null),
                ("Market Wearable No Male Representation", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0x64e98a568822bf15e3f38618ba50420e38b15579:0&background=039dfc"),
                ("Blue Satin Dress", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0xa826e7769bf0c712bff7b1b9e9031bfc36ed7758:0"),
                ("Black Zodiac #32", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0x97b97836fd45c5d15811779331bba4804518c7a5:0"),
                ("Retro Rollerblade Doll #31", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0x8d6c8c4cb8758fcebf614642bcec2cfa00fff478:0"),
                ("Illusion Dream #22", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0x97b97836fd45c5d15811779331bba4804518c7a5:1"),
                ("Friendship Tiara #166", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0x5de3c1b8b1b7727c7b44566a94b48a0f8030d8cc:1"),
                ("Lottie Make Up #103", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0x41093bd0d31710b6fbd8233552239730ba4edc5f:0"),
                ("DogLibre Mascot #205", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0xb1593d619fa21e68ff9f33a45c4dcf901fca3067:2"),
                ("Throat Chakra Tiara & Wings #162", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0x86395307bdc415665fb8be962d226ff8701461c0:1"),
                ("Templa Winter Boot - Black #141", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0xeca0b1a945fed5d7d716ce615264498e21418a50:0"),
                ("Templa Winter Boot - White #112", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0xeca0b1a945fed5d7d716ce615264498e21418a50:1"),
                ("Caustics Body Paint MVFW 2025 #24", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0x9c66321b8d4a40ffd8103a466fd2dfacb98ff1dc:0"),
                ("Star Trails Top Hat #71", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0x297bb6afce2ee056e5c742b9fcc40aca2818d65e:0"),
                ("Heart Headband #33", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0xbebb268219a67a80fe85fc6af9f0ad0ec0dca98c:0"),
                ("Catrines Make Up #150", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0x4fde0297c458e7a0bc35f07c015f322ca31b459e:0"),
                ("Profile", "?mode=profile&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc"),
                ("Authentication", "?mode=authentication&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc"),
                ("Market Wearable 1", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0x1b4e20251ec5da51c749f96a4993f3cebf066853:0&background=039dfc"),
                ("Market Wearable 2", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0x86395307bdc415665fb8be962d226ff8701461c0:0&background=039dfc"),
                ("Market Wearable 3", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0x222deaa90399023e707abd3f81b268493bdc891a:1&background=039dfc"),
                ("Market Emote", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0xb5e24ada4096b86ce3cf7af5119f19ed6089a80b:0&background=039dfc"),
                ("Market Emote Prop", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0x97822560ec3e3522c1237f85817003211281eb79:0&background=039dfc"),
                ("Market Emote Audio", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0xb187264af67cf6d147521626203dedcfd901ceb3:4&background=039dfc"),
                ("Market Multiple Urns", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:off-chain:base-avatars:turtle_neck_sweater&urn=urn:decentraland:off-chain:base-avatars:kilt&urn=urn:decentraland:off-chain:base-avatars:sport_black_shoes"),
                ("Market Wearable Pinned To Avatar", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0xa826e7769bf0c712bff7b1b9e9031bfc36ed7758:0&type=avatar"),
                ("Market Wearable Pinned To Item, No Switcher", "?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc&urn=urn:decentraland:matic:collections-v2:0xa826e7769bf0c712bff7b1b9e9031bfc36ed7758:0&type=wearable&disableSwitcher=true"),
                ("Builder", "?mode=builder&bodyShape=urn:decentraland:off-chain:base-avatars:BaseMale&eyeColor=20B3F6&skinColor=FFE4C6&hairColor=8C2014&urn=urn:decentraland:off-chain:base-avatars:turtle_neck_sweater&urn=urn:decentraland:off-chain:base-avatars:kilt&background=4b4851"),
                ("Builder Base64 Emote", "?mode=builder&bodyShape=urn:decentraland:off-chain:base-avatars:BaseMale&eyeColor=20B3F6&skinColor=FFE4C6&hairColor=8C2014&urn=urn:decentraland:off-chain:base-avatars:turtle_neck_sweater&urn=urn:decentraland:off-chain:base-avatars:kilt&background=4b4851&base64=eyJpZCI6ImZjMTZhMjlmLTAxZjQtNDI5MC1iZTY5LThjNGQ1ZDFlZDZlZSIsIm5hbWUiOiJDaGVmZiBraXNzIiwidGh1bWJuYWlsIjoidGh1bWJuYWlsLnBuZyIsImltYWdlIjoidGh1bWJuYWlsLnBuZyIsImRlc2NyaXB0aW9uIjoiIiwiaTE4biI6W3siY29kZSI6ImVuIiwidGV4dCI6IkNoZWZmIGtpc3MifV0sImVtb3RlRGF0YUFEUjc0Ijp7ImNhdGVnb3J5Ijoic3R1bnQiLCJsb29wIjpmYWxzZSwidGFncyI6W10sInJlcHJlc2VudGF0aW9ucyI6W3siYm9keVNoYXBlcyI6WyJ1cm46ZGVjZW50cmFsYW5kOm9mZi1jaGFpbjpiYXNlLWF2YXRhcnM6QmFzZU1hbGUiXSwibWFpbkZpbGUiOiJtYWxlL2NoZWZmIGtpc3MuZ2xiIiwiY29udGVudHMiOlt7ImtleSI6Im1hbGUvY2hlZmYga2lzcy5nbGIiLCJ1cmwiOiJodHRwczovL2J1aWxkZXItYXBpLmRlY2VudHJhbGFuZC56b25lL3YxL3N0b3JhZ2UvY29udGVudHMvYmFma3JlaWV0enN2anZrcG9uNWV5d25uYmRtdGdlaHo2czVtYWNxeGd1eDVidWh6aGFhZnNiM3F0eW0ifV19LHsiYm9keVNoYXBlcyI6WyJ1cm46ZGVjZW50cmFsYW5kOm9mZi1jaGFpbjpiYXNlLWF2YXRhcnM6QmFzZUZlbWFsZSJdLCJtYWluRmlsZSI6ImZlbWFsZS9jaGVmZiBraXNzLmdsYiIsImNvbnRlbnRzIjpbeyJrZXkiOiJmZW1hbGUvY2hlZmYga2lzcy5nbGIiLCJ1cmwiOiJodHRwczovL2J1aWxkZXItYXBpLmRlY2VudHJhbGFuZC56b25lL3YxL3N0b3JhZ2UvY29udGVudHMvYmFma3JlaWV0enN2anZrcG9uNWV5d25uYmRtdGdlaHo2czVtYWNxeGd1eDVidWh6aGFhZnNiM3F0eW0ifV19XX19"),
                ("Builder Anim Ref", "?mode=builder&bodyShape=urn:decentraland:off-chain:base-avatars:BaseMale&eyeColor=20B3F6&skinColor=FFE4C6&hairColor=8C2014&urn=urn:decentraland:off-chain:base-avatars:turtle_neck_sweater&urn=urn:decentraland:off-chain:base-avatars:kilt&urn=urn:decentraland:off-chain:base-avatars:keanu_hair&background=4b4851&showAnimationReference=true"),
            };
            // @formatter:on

            var dropdown = debugPanel.Q<DropdownField>("URLDropdown");
            dropdown.choices = dropdownOptions.Select(o => o.name).ToList();
            dropdown.index = 0;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                var selected = dropdownOptions.Find(o => o.name == evt.newValue);
                GameObject.Find("JSBridge").SendMessage("ParseFromString", selected.url);
                GameObject.Find("JSBridge").SendMessage("Reload");
            });

            var methodNameDropdown = debugPanel.Q<DropdownField>("MethodNameDropdown");
            methodNameDropdown.choices = typeof(JSBridge)
                .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance).Select(m => m.Name)
                .ToList();
            methodNameDropdown.index = 0;

            var parameterField = debugPanel.Q<TextField>("Parameter");
            debugPanel.Q<Button>("InvokeButton").clicked += () =>
            {
                var methodName = methodNameDropdown.value;
                var parameter = parameterField.value;

                if (string.IsNullOrEmpty(parameter))
                {
                    GameObject.Find("JSBridge").SendMessage(methodName);
                }
                else
                {
                    GameObject.Find("JSBridge").SendMessage(methodName, parameter);
                }

                if (methodName != "Reload" && methodName != "TakeScreenshot" && methodName != "Cleanup")
                {
                    GameObject.Find("JSBridge").SendMessage("Reload");
                }
            };

            debugPanel.Q<Button>("HideButton").clicked += () => debugPanel.style.display = DisplayStyle.None;
            debugPanel.Q<Label>("VersionLabel").text = Application.version;
            debugPanel.Q<Button>("PrintConfigButton").clicked += () => Debug.Log(AangConfiguration.Instance.ToString());
            
            debugPanel.Q<Button>("RandomProfileButton").clicked += () =>
            {
                GameObject.Find("JSBridge").SendMessage("SetProfile", $"default{Random.Range(1, 160)}");
                GameObject.Find("JSBridge").SendMessage("Reload");
            };

            _debugLoaded = true;
        }

        public enum SwitcherState
        {
            Wearable,
            Avatar,
            WearableLocked
        }
    }
}