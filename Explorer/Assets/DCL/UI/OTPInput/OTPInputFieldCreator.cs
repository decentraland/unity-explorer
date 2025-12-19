#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
///     Editor utility to quickly create a fully configured OTP Input Field.
///     Access via: GameObject > UI > OTP Input Field (6 digits)
/// </summary>
public static class OTPInputFieldCreator
{
    [MenuItem("GameObject/UI/OTP Input Field (6 digits)", false, 10)]
    private static void CreateOTPInputField(MenuCommand menuCommand)
    {
        // Ensure we have a Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            var canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Create root container
        var root = new GameObject("OTPInputField");
        GameObjectUtility.SetParentAndAlign(root, canvas.gameObject);

        RectTransform rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(400, 70);
        rootRT.anchoredPosition = Vector2.zero;

        // Add HorizontalLayoutGroup for automatic slot arrangement
        HorizontalLayoutGroup hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Add OTPInputField component
        OTPInputField otpComponent = root.AddComponent<OTPInputField>();

        // Create hidden input field
        var hiddenInputGO = new GameObject("HiddenInput");
        hiddenInputGO.transform.SetParent(root.transform, false);

        RectTransform hiddenRT = hiddenInputGO.AddComponent<RectTransform>();
        hiddenRT.anchorMin = Vector2.zero;
        hiddenRT.anchorMax = Vector2.one;
        hiddenRT.offsetMin = Vector2.zero;
        hiddenRT.offsetMax = Vector2.zero;

        Image hiddenBg = hiddenInputGO.AddComponent<Image>();
        hiddenBg.color = Color.clear;
        hiddenBg.raycastTarget = true;

        // Create text area for hidden input
        var textAreaGO = new GameObject("Text Area");
        textAreaGO.transform.SetParent(hiddenInputGO.transform, false);

        RectTransform textAreaRT = textAreaGO.AddComponent<RectTransform>();
        textAreaRT.anchorMin = Vector2.zero;
        textAreaRT.anchorMax = Vector2.one;
        textAreaRT.offsetMin = Vector2.zero;
        textAreaRT.offsetMax = Vector2.zero;
        textAreaGO.AddComponent<RectMask2D>();

        // Create text component for hidden input
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(textAreaGO.transform, false);

        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TextMeshProUGUI hiddenText = textGO.AddComponent<TextMeshProUGUI>();
        hiddenText.color = Color.clear;
        hiddenText.fontSize = 24;

        TMP_InputField inputField = hiddenInputGO.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRT;
        inputField.textComponent = hiddenText;
        inputField.characterLimit = 6;
        inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        inputField.caretColor = Color.clear;
        inputField.selectionColor = Color.clear;

        // Create 6 slot containers
        var slotTexts = new TMP_Text[6];
        var slotBackgrounds = new Image[6];

        for (var i = 0; i < 6; i++)
        {
            var slotGO = new GameObject($"Slot_{i}");
            slotGO.transform.SetParent(root.transform, false);

            RectTransform slotRT = slotGO.AddComponent<RectTransform>();
            slotRT.sizeDelta = new Vector2(50, 60);

            // Background
            Image slotBg = slotGO.AddComponent<Image>();
            slotBg.color = new Color(0.15f, 0.15f, 0.18f, 1f);
            slotBg.raycastTarget = false;
            slotBackgrounds[i] = slotBg;

            // Add rounded corners via sprite if available, otherwise use default
            // For production, assign a rounded rect sprite here

            // Digit text
            var digitGO = new GameObject("Digit");
            digitGO.transform.SetParent(slotGO.transform, false);

            RectTransform digitRT = digitGO.AddComponent<RectTransform>();
            digitRT.anchorMin = Vector2.zero;
            digitRT.anchorMax = Vector2.one;
            digitRT.offsetMin = Vector2.zero;
            digitRT.offsetMax = Vector2.zero;

            TextMeshProUGUI digitText = digitGO.AddComponent<TextMeshProUGUI>();
            digitText.text = "";
            digitText.fontSize = 32;
            digitText.fontStyle = FontStyles.Bold;
            digitText.alignment = TextAlignmentOptions.Center;
            digitText.color = Color.white;
            digitText.raycastTarget = false;
            slotTexts[i] = digitText;
        }

        // Move hidden input to front for input capture
        hiddenInputGO.transform.SetAsLastSibling();

        // Assign references via SerializedObject
        var so = new SerializedObject(otpComponent);
        so.FindProperty("hiddenInput").objectReferenceValue = inputField;

        SerializedProperty slotTextsProp = so.FindProperty("slotTexts");
        slotTextsProp.arraySize = 6;

        for (var i = 0; i < 6; i++) { slotTextsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotTexts[i]; }

        SerializedProperty slotBgsProp = so.FindProperty("slotBackgrounds");
        slotBgsProp.arraySize = 6;

        for (var i = 0; i < 6; i++) { slotBgsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotBackgrounds[i]; }

        so.ApplyModifiedProperties();

        // Select the created object
        Selection.activeGameObject = root;
        Undo.RegisterCreatedObjectUndo(root, "Create OTP Input Field");

        Debug.Log("[OTPInputField] Created! Configure colors and events in the Inspector.");
    }

    [MenuItem("GameObject/UI/OTP Input Field (6 digits)", true)]
    private static bool ValidateCreateOTPInputField() =>
        true;
}
#endif
