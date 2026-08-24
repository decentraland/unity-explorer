using System;
using System.Collections.Generic;
using Configurator.Views;
using Data;
using Runtime.Wearables;
using UI.Elements;
using UI.Manipulators;
using UnityEngine;
using UnityEngine.UIElements;
using Utils;

namespace Configurator
{
    public class ConfiguratorUIPresenter : MonoBehaviour
    {
        private const string USS_CUSTOMIZE_CONTAINER_HIDDEN = "customize-container--hidden";
        private const string USS_CONFIRMATION_CONTAINER_HIDDEN = "confirmation-container--hidden";
        
        [SerializeField] private UIDocument uiDocument;

        private VisualElement _customizeContainer;
        private VisualElement _loader;
        private VisualElement _loaderIcon;

        private DCLButtonElement _backButton;
        private DCLButtonElement _secondaryButton;
        private DCLButtonElement _primaryButton;

        private Label _stageTitle;
        private Label _stageNumber;

        private VisualElement _confirmContainer;
        private Label _confirmTitle;

        private Label _fpsCounter;

        private string _username;

        // Views
        private WearablesView _headWearablesView;
        private WearablesView _bodyWearablesView;
        private PresetsView _presetsView;
        private BodyShapePopupView _bodyShapePopupView;
        private ColorPopupView _skinColorPopupView;
        private ColorPopupView _hairColorPopupView;

        private StageView[] _stages;
        private int _currentStageIndex;

        public event Action<Vector2> CharacterAreaCenterChanged;
        public event Action<float> CharacterAreaZoom;
        public event Action<Vector2, float> CharacterAreaDrag;

        public event Action<string> CategoryChanged;
        public event Action<Color> SkinColorSelected;
        public event Action<Color> HairColorSelected;
        public event Action<Color> EyeColorSelected;
        public event Action<BodyShape> BodyShapeSelected;
        public event Action<string, EntityDefinition> WearableSelected;
        public event Action<PresetDefinition> PresetSelected;
        public event Action<bool> Confirmed;
        public event Action<bool> JumpIn;
        public event Action<int> AvatarCustomizationStepChanged;

        private bool _confirmationOpen;
        private bool _usingMobile;
        private bool _showingFPS;

        private (object presets, object headWearables, object bodyWearables, object skinColorPopup, object
            bodyShapePopup)? _viewData;

        private void OnEnable()
        {
            var root = uiDocument.rootVisualElement;

            _customizeContainer = root.Q("CustomizeContainer");
            var characterArea = _customizeContainer.Q("CharacterArea");

            characterArea.RegisterCallback<GeometryChangedEvent, VisualElement>((_, area) =>
            {
                if (_confirmationOpen) return;

                var panel = area.panel;
                var layoutSize = panel.visualTree.layout; // This is in panel space

                var elementCenter = area.worldBound.center;
                var panelCenter = layoutSize.center;

                var offsetFromCenter = elementCenter - panelCenter;
                var normalized = new Vector2(
                    offsetFromCenter.x / layoutSize.width,
                    offsetFromCenter.y / layoutSize.height
                );

                CharacterAreaCenterChanged!(normalized);
            }, characterArea);
            characterArea.RegisterCallback<WheelEvent, ConfiguratorUIPresenter>(
                (evt, p) => p.CharacterAreaZoom!(evt.delta.y), this);
            characterArea.AddManipulator(new DragManipulator((d, dt) => CharacterAreaDrag!(d, dt)));

            _stageTitle = root.Q<Label>("StageTitle");
            _stageNumber = root.Q<Label>("StageNumber");

            _backButton = root.Q<DCLButtonElement>("BackButton");
            _secondaryButton = root.Q<DCLButtonElement>("SecondaryButton");
            _primaryButton = root.Q<DCLButtonElement>("PrimaryButton");

            _backButton.Clicked += OnBackClicked;
            _primaryButton.Clicked += OnPrimaryClicked;
            _secondaryButton.Clicked += OnSecondaryClicked;

            _loader = root.Q("Loader");
            _loaderIcon = _loader.Q("Icon");

            var presetsContainer = root.Q("Presets");
            _presetsView = new PresetsView(presetsContainer,
                "Choose {0}'s starting look",
                "CUSTOMIZE LATER",
                221,
                "CUSTOMIZE LATER",
                "START CUSTOMIZATION",
                "START");
            _presetsView.PresetSelected += preset => PresetSelected!(preset);

            // Dropdowns
            var bodyShapeDropdown = root.Q<DCLDropdownElement>("BodyTypeDropdown");
            _bodyShapePopupView = new BodyShapePopupView(bodyShapeDropdown, bodyShapeDropdown.Q("BodyTypePopup"));
            _bodyShapePopupView.BodyShapeSelected += bs => BodyShapeSelected!(bs);
            _bodyShapePopupView.SetAutoClose(true);

            var skinColorDropdown = root.Q<DCLDropdownElement>("SkinColorDropdown");
            _skinColorPopupView = new ColorPopupView(skinColorDropdown, skinColorDropdown.Q("ColorPopup"),
                skinColorDropdown.Icon);
            _skinColorPopupView.ColorSelected += skinColor => SkinColorSelected!(skinColor);

            _headWearablesView = new WearablesView(
                root.Q("HeadWearables"),
                "Customize {0}'s face",
                "CUSTOMIZE OUTFIT",
                209,
                "OUTFIT");
            _headWearablesView.WearableSelected += (c, ae) => WearableSelected!(c, ae);
            _headWearablesView.CategoryChanged += c => CategoryChanged!(c);
            _headWearablesView.ColorSelected += c =>
            {
                switch (_headWearablesView.SelectedCategory)
                {
                    case WearableCategories.Categories.EYES:
                        EyeColorSelected!(c);
                        break;
                    case WearableCategories.Categories.HAIR:
                        HairColorSelected!(c);
                        break;
                }
            };

            _bodyWearablesView = new WearablesView(
                root.Q("BodyWearables"),
                "Customize {0}'s outfit",
                "FINISH",
                123,
                "FINISH");
            _bodyWearablesView.WearableSelected += (c, ae) => WearableSelected!(c, ae);
            _bodyWearablesView.CategoryChanged += c => CategoryChanged!(c);

            _stages = new StageView[]
            {
                _presetsView,
                _headWearablesView,
                _bodyWearablesView
            };

            // Confirm stage
            _confirmContainer = root.Q("ConfirmationContainer");
            _confirmTitle = _confirmContainer.Q<Label>("Title");
            _confirmContainer.Q<DCLButtonElement>("ConfirmationBackButton").Clicked += () => OpenConfirm(false);
            _confirmContainer.Q<DCLButtonElement>("JumpInButton").Clicked += () => JumpIn!(false);

            // Debug FPS Counter
            _fpsCounter = root.Q<Label>("FPSCounter");
            _fpsCounter.SetDisplay(_showingFPS = AangConfiguration.Instance.ShowFPS);

            // To allow live reload during runtime in the editor
            if (_viewData.HasValue)
            {
                _presetsView.SetData(_viewData.Value.presets);
                _headWearablesView.SetData(_viewData.Value.headWearables);
                _bodyWearablesView.SetData(_viewData.Value.bodyWearables);
                _skinColorPopupView.SetData(_viewData.Value.skinColorPopup);
                _bodyShapePopupView.SetData(_viewData.Value.bodyShapePopup);

                for (var i = 0; i < _stages.Length; i++)
                {
                    var stage = _stages[i];
                    stage.Show();

                    if (i < _currentStageIndex)
                    {
                        stage.HideLeft();
                    }
                    else if (i > _currentStageIndex)
                    {
                        stage.HideRight();
                    }
                }

                if (_confirmationOpen)
                {
                    OpenConfirm(true);
                }
            }

            RefreshCurrentStage();
        }

        private void OnDisable()
        {
            if (!Application.isEditor) return;

            _viewData = (
                presets: _presetsView.GetData(),
                headWearables: _headWearablesView.GetData(),
                bodyWearables: _bodyWearablesView.GetData(),
                skinColorPopup: _skinColorPopupView.GetData(),
                bodyShapePopup: _bodyShapePopupView.GetData()
            );
        }

        private void Start()
        {
            _customizeContainer.SetVisibility(false);
            _loader.SetDisplay(true);
        }

        private void OnBackClicked()
        {
            if (_currentStageIndex == 0) return;

            _stages[_currentStageIndex].HideRight();
            _stages[--_currentStageIndex].Show();

            RefreshCurrentStage();
            AvatarCustomizationStepChanged?.Invoke(_currentStageIndex);
        }

        private void OnPrimaryClicked()
        {
            // The next button on the first stage (presets) functions as "skip"
            if (_currentStageIndex == 0)
            {
                JumpIn!(true);
                return;
            }

            if (_currentStageIndex == _stages.Length - 1)
            {
                JumpIn!(false);
                return;
            }

            AdvanceStage();
        }

        private void OnSecondaryClicked()
        {
            // The skip button on the first stage (presets) functions as "next"
            if (_currentStageIndex == 0)
            {
                AdvanceStage();
                return;
            }

            JumpIn!(true);
        }

        private void AdvanceStage()
        {
            _stages[_currentStageIndex].HideLeft();
            _stages[++_currentStageIndex].Show();

            RefreshCurrentStage();
            AvatarCustomizationStepChanged?.Invoke(_currentStageIndex);
        }

        private void OpenConfirm(bool open)
        {
            _confirmationOpen = open;
            Confirmed!(open);
            
            _customizeContainer.EnableInClassList(USS_CUSTOMIZE_CONTAINER_HIDDEN, open);
            _confirmContainer.EnableInClassList(USS_CONFIRMATION_CONTAINER_HIDDEN, !open);
        }

        private void RefreshCurrentStage()
        {
            if (_confirmationOpen) return;
            
            var stage = _stages[_currentStageIndex];
            _stageTitle.text = string.Format(stage.Title, _username);
            _stageNumber.text = $"{_currentStageIndex + 1}.";
            _primaryButton.Text = _usingMobile ? stage.PrimaryButtonTextMobile : stage.PrimaryButtonText;

            stage.SetUsingMobileMode(_usingMobile);

            // No animations on mobile
            if (_usingMobile)
            {
                _primaryButton.style.width = StyleKeyword.Auto;
            }
            else
            {
                _primaryButton.style.width = stage.PrimaryButtonWidth;
            }

            _primaryButton.ButtonIcon = _currentStageIndex == _stages.Length - 1
                ? DCLButtonElement.Icon.Check
                : DCLButtonElement.Icon.Forward;

            var secondaryText = _usingMobile ? stage.SecondaryButtonTextMobile : stage.SecondaryButtonText;
            var showSecondary = secondaryText != null;
            _secondaryButton.EnableInClassList("dcl-button--hidden-down", !showSecondary);
            if(secondaryText != null) _secondaryButton.Text = secondaryText;
            _secondaryButton.pickingMode = showSecondary ? PickingMode.Position : PickingMode.Ignore; 
            
            _backButton.EnableInClassList("dcl-button--hidden-down", _currentStageIndex == 0);

            // TODO: Change?
            CategoryChanged?.Invoke(stage.SelectedCategory);
        }

        private void Update()
        {
            // Rotate the loader icon
            _loaderIcon.RotateBy(360f * Time.deltaTime);

            // FPS
            if (_showingFPS)
            {
                _fpsCounter.text = ((int)(1f / Time.unscaledDeltaTime)).ToString();
            }
        }

        public void LoadCompleted()
        {
            RefreshCurrentStage();
            _customizeContainer.SetVisibility(true);
            _loader.SetDisplay(false);
            AvatarCustomizationStepChanged?.Invoke(_currentStageIndex);
        }

        public void SetUsingMobileMode(bool usingMobile)
        {
            // TODO
            _usingMobile = usingMobile;

            _skinColorPopupView.SetAutoClose(usingMobile);

            RefreshCurrentStage();
        }

        public void SetUsername(string username)
        {
            _username = username;
            _confirmTitle.text = $"<font-weight=600>{_username} is Ready to Jump In!";
        }

        public void SetAvatarPresets(PresetDefinition[] avatarPresets, int selectedAvatarPresetIndex)
        {
            _presetsView.SetPresets(avatarPresets, selectedAvatarPresetIndex);
        }

        public void SetColorPresets(Color[] skinColorPresets, Color[] hairColorPresets, Color[] eyeColorPresets)
        {
            _skinColorPopupView.SetColors(skinColorPresets);
            _headWearablesView.SetColorPresets(hairColorPresets, eyeColorPresets);
        }

        public void SetCollection(List<CategoryDefinition> faceCollection, List<CategoryDefinition> bodyCollection)
        {
            _bodyWearablesView.SetCollection(bodyCollection);
            _headWearablesView.SetCollection(faceCollection);
        }

        public void SetBodyShape(BodyShape bodyShape)
        {
            _bodyShapePopupView.SetBodyShape(bodyShape);
        }

        public void SetSelectedItems(Dictionary<string, EntityDefinition> selectedItems)
        {
            _headWearablesView.SetSelectedItems(selectedItems);
            _bodyWearablesView.SetSelectedItems(selectedItems);
        }

        public void SetColors(Color skinColor, Color hairColor, Color eyeColor)
        {
            _skinColorPopupView.SetSelectedColor(skinColor);
            _headWearablesView.SetSelectedColors(hairColor, eyeColor);
        }

        public void ClearPresetSelection()
        {
            _presetsView.ClearSelection();
        }

        public void GetElementBounds(string elementName)
        {
            var root = uiDocument.rootVisualElement;
            var element = root.Q(elementName);

            if (element == null || element.resolvedStyle.display == DisplayStyle.None || element.worldBound.width <= 0)
            {
                JSBridge.NativeCalls.OnElementBounds(JsonUtility.ToJson(new ElementBoundsData { name = elementName }));
                return;
            }

            var bounds = element.worldBound;
            var panelLayout = root.panel.visualTree.layout;

            JSBridge.NativeCalls.OnElementBounds(JsonUtility.ToJson(new ElementBoundsData
            {
                name = elementName,
                found = true,
                x = bounds.x / panelLayout.width,
                y = bounds.y / panelLayout.height,
                width = bounds.width / panelLayout.width,
                height = bounds.height / panelLayout.height,
            }));
        }

        [Serializable]
        private struct ElementBoundsData
        {
            public string name;
            public bool found;
            public float x, y, width, height;
        }
    }
}