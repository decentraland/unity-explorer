using DCL.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.BugReporting.UI.Editor
{
    /// <summary>
    ///     One-shot scaffold for the BugReportView prefab: builds a grey-box hierarchy with every
    ///     serialized field wired so the prefab only needs visual styling afterwards.
    /// </summary>
    public static class BugReportViewPrefabScaffolder
    {
        private const string PREFAB_PATH = "Assets/DCL/BugReporting/UI/BugReportView.prefab";
        private const string PROMPT_PREFAB_PATH = "Assets/DCL/BugReporting/UI/PerformanceIssuePrompt.prefab";

        private static readonly Color PANEL_COLOR = new (0.13f, 0.13f, 0.16f, 1f);
        private static readonly Color BACKDROP_COLOR = new (0f, 0f, 0f, 0.6f);
        private static readonly Color LABEL_COLOR = new (0.92f, 0.92f, 0.95f, 1f);
        private static readonly Color PLACEHOLDER_COLOR = new (0.5f, 0.5f, 0.55f, 1f);
        private static readonly Color BUTTON_LABEL_COLOR = new (0.1f, 0.1f, 0.1f, 1f);

        [MenuItem("Decentraland/UI/Scaffold BugReportView Prefab")]
        private static void Scaffold()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) != null &&
                !EditorUtility.DisplayDialog("Scaffold BugReportView", $"{PREFAB_PATH} already exists. Overwrite it?", "Overwrite", "Cancel"))
                return;

            TMP_DefaultControls.Resources resources = BuiltinResources();

            GameObject root = new ("BugReportView", typeof(RectTransform));

            try
            {
                // An own overlay canvas (mirroring BlockedScreen) so MVCManager's SetDrawOrder can drive the sorting directly on the view.
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);

                var raycaster = root.AddComponent<GraphicRaycaster>();

                var view = root.AddComponent<BugReportView>();

                Image backdrop = AddImage(CreateChild(root.transform, "Backdrop"), null, BACKDROP_COLOR);
                Stretch(backdrop.rectTransform);

                Image panel = AddImage(CreateChild(root.transform, "Panel"), resources.background, PANEL_COLOR);
                panel.rectTransform.sizeDelta = new Vector2(520f, 680f);

                Button closeButton = CreateButton(panel.transform, "CloseButton", "X", resources.standard);
                var closeRect = (RectTransform)closeButton.transform;
                closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
                closeRect.anchoredPosition = new Vector2(-12f, -12f);
                closeRect.sizeDelta = new Vector2(32f, 32f);

                GameObject formPanel = CreateStatePanel(panel.transform, "FormPanel");
                CreateText(formPanel.transform, "Title", "Report a Bug", 24f, FontStyles.Bold, TextAlignmentOptions.Left, 32f);

                TMP_Dropdown issueTypeDropdown = CreateIssueTypeDropdown(formPanel.transform, resources);
                TMP_InputField descriptionInput = CreateDescriptionInput(formPanel.transform, resources);
                TextMeshProUGUI descriptionCharCounter = CreateCharCounter(descriptionInput);

                GameObject screenshotSection = CreateChild(formPanel.transform, "ScreenshotSection");
                AddVerticalLayout(screenshotSection, padding: 0, topPadding: 0, spacing: 8f);
                var screenshotPreview = CreateChild(screenshotSection.transform, "ScreenshotPreview").AddComponent<RawImage>();
                SetPreferredHeight(screenshotPreview.gameObject, 120f);
                screenshotPreview.gameObject.SetActive(false);
                Button attachScreenshotButton = CreateButton(screenshotSection.transform, "AttachScreenshotButton", "Attach Screenshot", resources.standard, 40f);
                Button removeScreenshotButton = CreateButton(screenshotSection.transform, "RemoveScreenshotButton", "Remove Screenshot", resources.standard, 40f);
                removeScreenshotButton.gameObject.SetActive(false);

                Toggle shareLogsToggle = CreateToggle(formPanel.transform, "ShareLogsToggle", "Share logs with this report", resources);

                GameObject buttonsRow = CreateChild(formPanel.transform, "ButtonsRow");
                var row = buttonsRow.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 12f;
                row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = true;
                row.childForceExpandHeight = false;
                SetPreferredHeight(buttonsRow, 44f);
                Button cancelButton = CreateButton(buttonsRow.transform, "CancelButton", "Cancel", resources.standard, 44f);
                Button submitButton = CreateButton(buttonsRow.transform, "SubmitButton", "Submit", resources.standard, 44f);

                GameObject successPanel = CreateStatePanel(panel.transform, "SuccessPanel");
                CreateText(successPanel.transform, "SuccessTitle", "Bug Report Submitted", 24f, FontStyles.Bold, TextAlignmentOptions.Center, 32f);
                CreateText(successPanel.transform, "SuccessMessage", "Thanks for helping us improve Decentraland.", 16f, FontStyles.Normal, TextAlignmentOptions.Center, 48f);
                Button successDoneButton = CreateButton(successPanel.transform, "SuccessDoneButton", "Done", resources.standard, 44f);
                successPanel.SetActive(false);

                WireSerializedFields(view, canvas, raycaster, issueTypeDropdown, descriptionInput, descriptionCharCounter, shareLogsToggle, submitButton, cancelButton, closeButton,
                    screenshotSection, screenshotPreview, attachScreenshotButton, removeScreenshotButton,
                    formPanel, successPanel, successDoneButton);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
                Selection.activeObject = prefab;
                Debug.Log($"BugReportView prefab scaffolded at {PREFAB_PATH}. Style it and mark it Addressable for BugReportPlugin settings.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [MenuItem("Decentraland/UI/Scaffold PerformanceIssuePrompt Prefab")]
        private static void ScaffoldPerformanceIssuePrompt()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PROMPT_PREFAB_PATH) != null &&
                !EditorUtility.DisplayDialog("Scaffold PerformanceIssuePrompt", $"{PROMPT_PREFAB_PATH} already exists. Overwrite it?", "Overwrite", "Cancel"))
                return;

            TMP_DefaultControls.Resources resources = BuiltinResources();

            GameObject root = new ("PerformanceIssuePrompt", typeof(RectTransform));

            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);

                var raycaster = root.AddComponent<GraphicRaycaster>();

                var view = root.AddComponent<PerformanceIssuePromptView>();

                Image backdrop = AddImage(CreateChild(root.transform, "Backdrop"), null, BACKDROP_COLOR);
                Stretch(backdrop.rectTransform);

                Image panel = AddImage(CreateChild(root.transform, "Panel"), resources.background, PANEL_COLOR);
                panel.rectTransform.sizeDelta = new Vector2(480f, 300f);
                AddVerticalLayout(panel.gameObject, padding: 24, topPadding: 24, spacing: 16f);

                CreateText(panel.transform, "Title", "Performance issue detected", 24f, FontStyles.Bold, TextAlignmentOptions.Center, 32f);
                CreateText(panel.transform, "Message",
                    "We noticed a drop in performance while you were exploring. Would you like to send us a report to help improve the experience?",
                    16f, FontStyles.Normal, TextAlignmentOptions.Center, 72f);
                Toggle dontShowAgainToggle = CreateToggle(panel.transform, "DontShowAgainToggle", "Don't show this again", resources);

                GameObject buttonsRow = CreateChild(panel.transform, "ButtonsRow");
                var row = buttonsRow.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 12f;
                row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = true;
                row.childForceExpandHeight = false;
                SetPreferredHeight(buttonsRow, 44f);
                Button closeButton = CreateButton(buttonsRow.transform, "CloseButton", "Close", resources.standard, 44f);
                Button reportBugButton = CreateButton(buttonsRow.transform, "ReportBugButton", "Report Bug", resources.standard, 44f);

                var serializedView = new SerializedObject(view);
                SetReference(serializedView, "canvas", canvas);
                SetReference(serializedView, "raycaster", raycaster);
                SetReference(serializedView, nameof(PerformanceIssuePromptView.DontShowAgainToggle), dontShowAgainToggle);
                SetReference(serializedView, nameof(PerformanceIssuePromptView.CloseButton), closeButton);
                SetReference(serializedView, nameof(PerformanceIssuePromptView.ReportBugButton), reportBugButton);
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PROMPT_PREFAB_PATH);
                Selection.activeObject = prefab;
                Debug.Log($"PerformanceIssuePrompt prefab scaffolded at {PROMPT_PREFAB_PATH}. Style it, mark it Addressable and assign it to BugReportPlugin settings.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static TMP_DefaultControls.Resources BuiltinResources() =>
            new ()
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
            };

        private static GameObject CreateStatePanel(Transform parent, string name)
        {
            GameObject statePanel = CreateChild(parent, name);
            Stretch((RectTransform)statePanel.transform);

            // The extra top padding keeps content clear of the close button anchored to the panel corner.
            AddVerticalLayout(statePanel, padding: 24, topPadding: 56, spacing: 16f);
            return statePanel;
        }

        private static TMP_Dropdown CreateIssueTypeDropdown(Transform parent, TMP_DefaultControls.Resources resources)
        {
            GameObject dropdownGo = TMP_DefaultControls.CreateDropdown(resources);
            dropdownGo.name = "IssueTypeDropdown";
            dropdownGo.transform.SetParent(parent, false);
            SetPreferredHeight(dropdownGo, 44f);

            var dropdown = dropdownGo.GetComponent<TMP_Dropdown>();
            dropdown.options.Clear();
            dropdown.captionText.text = string.Empty;
            dropdown.SetValueWithoutNotify(-1);

            // Shown while value is -1 ("no selection"), which the controller sets on every open.
            var placeholder = CreateText(dropdownGo.transform, "Placeholder", "Select an issue type", 14f, FontStyles.Normal, TextAlignmentOptions.Left, 0f, withLayout: false);
            placeholder.color = PLACEHOLDER_COLOR;
            var placeholderRect = placeholder.rectTransform;
            Stretch(placeholderRect);
            placeholderRect.offsetMin = new Vector2(10f, 6f);
            placeholderRect.offsetMax = new Vector2(-25f, -7f);
            dropdown.placeholder = placeholder;

            return dropdown;
        }

        // Overlaid on the field's bottom-right corner, outside any layout group.
        private static TextMeshProUGUI CreateCharCounter(TMP_InputField input)
        {
            var counter = CreateText(input.transform, "CharCounter", "0/0", 12f, FontStyles.Normal, TextAlignmentOptions.BottomRight, 0f, withLayout: false);
            counter.color = PLACEHOLDER_COLOR;

            var rect = counter.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-10f, 6f);
            rect.sizeDelta = new Vector2(100f, 16f);

            return counter;
        }

        private static TMP_InputField CreateDescriptionInput(Transform parent, TMP_DefaultControls.Resources resources)
        {
            GameObject inputGo = TMP_DefaultControls.CreateInputField(resources);
            inputGo.name = "DescriptionInput";
            inputGo.transform.SetParent(parent, false);

            var layout = inputGo.AddComponent<LayoutElement>();
            layout.preferredHeight = 160f;
            layout.flexibleHeight = 1f;

            TMP_InputField input = ReplaceWithMultilineInputField(inputGo.GetComponent<TMP_InputField>());
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.textComponent.alignment = TextAlignmentOptions.TopLeft;

            var placeholder = (TMP_Text)input.placeholder;
            placeholder.text = "Describe the bug: what happened and what you expected.";
            placeholder.alignment = TextAlignmentOptions.TopLeft;

            return input;
        }

        // TMP_DefaultControls can only build the stock component, so the wired field is rebuilt as a MultilineInputField.
        private static MultilineInputField ReplaceWithMultilineInputField(TMP_InputField stock)
        {
            GameObject go = stock.gameObject;
            RectTransform viewport = stock.textViewport;
            TMP_Text text = stock.textComponent;
            Graphic placeholder = stock.placeholder;
            Graphic targetGraphic = stock.targetGraphic;
            Object.DestroyImmediate(stock);

            var input = go.AddComponent<MultilineInputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = targetGraphic;
            return input;
        }

        private static Toggle CreateToggle(Transform parent, string name, string label, TMP_DefaultControls.Resources resources)
        {
            GameObject toggleGo = CreateChild(parent, name);
            SetPreferredHeight(toggleGo, 24f);
            var toggle = toggleGo.AddComponent<Toggle>();

            Image background = AddImage(CreateChild(toggleGo.transform, "Background"), resources.standard, Color.white);
            var backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = backgroundRect.anchorMax = backgroundRect.pivot = new Vector2(0f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(20f, 20f);

            Image checkmark = AddImage(CreateChild(background.transform, "Checkmark"), resources.checkmark, Color.black);
            var checkmarkRect = checkmark.rectTransform;
            checkmarkRect.anchorMin = checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkmarkRect.sizeDelta = new Vector2(20f, 20f);

            var labelText = CreateText(toggleGo.transform, "Label", label, 14f, FontStyles.Normal, TextAlignmentOptions.Left, 0f, withLayout: false);
            var labelRect = labelText.rectTransform;
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(28f, 0f);

            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.isOn = true;

            return toggle;
        }

        private static Button CreateButton(Transform parent, string name, string label, Sprite sprite, float preferredHeight = 0f)
        {
            GameObject buttonGo = CreateChild(parent, name);
            Image image = AddImage(buttonGo, sprite, Color.white);
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;

            if (preferredHeight > 0f)
                SetPreferredHeight(buttonGo, preferredHeight);

            var text = CreateText(buttonGo.transform, "Label", label, 16f, FontStyles.Normal, TextAlignmentOptions.Center, 0f, withLayout: false);
            text.color = BUTTON_LABEL_COLOR;
            Stretch(text.rectTransform);

            return button;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string content, float fontSize, FontStyles style, TextAlignmentOptions alignment, float preferredHeight, bool withLayout = true)
        {
            var text = CreateChild(parent, name).AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = LABEL_COLOR;

            if (withLayout)
                SetPreferredHeight(text.gameObject, preferredHeight);

            return text;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            GameObject child = new (name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static Image AddImage(GameObject target, Sprite? sprite, Color color)
        {
            var image = target.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private static void AddVerticalLayout(GameObject target, int padding, int topPadding, float spacing)
        {
            var layout = target.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, topPadding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void SetPreferredHeight(GameObject target, float height) =>
            target.AddComponent<LayoutElement>().preferredHeight = height;

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void WireSerializedFields(BugReportView view, Canvas canvas, GraphicRaycaster raycaster,
            TMP_Dropdown issueTypeDropdown, TMP_InputField descriptionInput, TextMeshProUGUI descriptionCharCounter,
            Toggle shareLogsToggle, Button submitButton, Button cancelButton, Button closeButton, GameObject screenshotSection,
            RawImage screenshotPreview, Button attachScreenshotButton, Button removeScreenshotButton, GameObject formPanel,
            GameObject successPanel, Button successDoneButton)
        {
            var serializedView = new SerializedObject(view);

            // ViewBase's optional fields are protected, hence not reachable by nameof from here.
            SetReference(serializedView, "canvas", canvas);
            SetReference(serializedView, "raycaster", raycaster);

            SetReference(serializedView, nameof(BugReportView.IssueTypeDropdown), issueTypeDropdown);
            SetReference(serializedView, nameof(BugReportView.DescriptionInput), descriptionInput);
            SetReference(serializedView, nameof(BugReportView.DescriptionCharCounter), descriptionCharCounter);
            SetReference(serializedView, nameof(BugReportView.ShareLogsToggle), shareLogsToggle);
            SetReference(serializedView, nameof(BugReportView.SubmitButton), submitButton);
            SetReference(serializedView, nameof(BugReportView.CancelButton), cancelButton);
            SetReference(serializedView, nameof(BugReportView.CloseButton), closeButton);
            SetReference(serializedView, nameof(BugReportView.ScreenshotSection), screenshotSection);
            SetReference(serializedView, nameof(BugReportView.ScreenshotPreview), screenshotPreview);
            SetReference(serializedView, nameof(BugReportView.AttachScreenshotButton), attachScreenshotButton);
            SetReference(serializedView, nameof(BugReportView.RemoveScreenshotButton), removeScreenshotButton);
            SetReference(serializedView, nameof(BugReportView.FormPanel), formPanel);
            SetReference(serializedView, nameof(BugReportView.SuccessPanel), successPanel);
            SetReference(serializedView, nameof(BugReportView.SuccessDoneButton), successDoneButton);
            serializedView.ApplyModifiedPropertiesWithoutUndo();
        }

        // The view's fields are auto-properties, so their serialized names are compiler-generated backing fields.
        private static void SetReference(SerializedObject serializedView, string propertyName, Object value) =>
            serializedView.FindProperty($"<{propertyName}>k__BackingField").objectReferenceValue = value;
    }
}
