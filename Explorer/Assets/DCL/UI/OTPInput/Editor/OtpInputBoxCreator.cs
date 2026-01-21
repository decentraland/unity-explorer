#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
///     Создаёт готовый OtpInputBox через меню GameObject > UI > OTP Input Box.
///     Или через правый клик в Hierarchy.
/// </summary>
public static class OtpInputBoxCreator
{
    [MenuItem("GameObject/UI/OTP Input Box (6 digits)", false, 2100)]
    private static void CreateOtpInputBox(MenuCommand menuCommand)
    {
        // Находим или создаём Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            var canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
        }

        // Определяем родителя
        var parent = menuCommand.context as GameObject;

        if (parent == null)
            parent = canvas.gameObject;

        // Создаём корневой объект
        var root = new GameObject("OtpInputBox");
        GameObjectUtility.SetParentAndAlign(root, parent);
        Undo.RegisterCreatedObjectUndo(root, "Create OTP Input Box");

        // RectTransform корня
        RectTransform rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(380, 70);
        rootRT.anchoredPosition = Vector2.zero;

        // HorizontalLayoutGroup для автоматического выравнивания слотов
        HorizontalLayoutGroup hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.padding = new RectOffset(8, 8, 8, 8);

        // Массивы для ссылок
        var slotTexts = new TMP_Text[6];
        var slotBackgrounds = new Image[6];

        // Создаём 6 слотов
        for (var i = 0; i < 6; i++)
        {
            var slotGO = new GameObject($"Slot_{i}");
            slotGO.transform.SetParent(root.transform, false);

            // RectTransform слота
            RectTransform slotRT = slotGO.AddComponent<RectTransform>();
            slotRT.sizeDelta = new Vector2(52, 64);

            // Фон слота
            Image slotBg = slotGO.AddComponent<Image>();
            slotBg.color = new Color(0.15f, 0.15f, 0.18f, 1f);
            slotBg.raycastTarget = false; // Клик пойдёт через HiddenInput
            slotBackgrounds[i] = slotBg;

            // Текст цифры
            var digitGO = new GameObject("Digit");
            digitGO.transform.SetParent(slotGO.transform, false);

            RectTransform digitRT = digitGO.AddComponent<RectTransform>();
            digitRT.anchorMin = Vector2.zero;
            digitRT.anchorMax = Vector2.one;
            digitRT.offsetMin = Vector2.zero;
            digitRT.offsetMax = Vector2.zero;

            TextMeshProUGUI digitText = digitGO.AddComponent<TextMeshProUGUI>();
            digitText.text = "";
            digitText.fontSize = 36;
            digitText.fontStyle = FontStyles.Bold;
            digitText.alignment = TextAlignmentOptions.Center;
            digitText.color = Color.white;
            digitText.raycastTarget = false;
            slotTexts[i] = digitText;
        }

        // Создаём скрытый InputField (поверх всего для захвата кликов)
        var hiddenInputGO = new GameObject("HiddenInput");
        hiddenInputGO.transform.SetParent(root.transform, false);
        hiddenInputGO.transform.SetAsLastSibling(); // Поверх слотов

        // RectTransform — растягиваем на весь root
        RectTransform hiddenRT = hiddenInputGO.AddComponent<RectTransform>();
        hiddenRT.anchorMin = Vector2.zero;
        hiddenRT.anchorMax = Vector2.one;
        hiddenRT.offsetMin = Vector2.zero;
        hiddenRT.offsetMax = Vector2.zero;

        // Image для raycast (прозрачный)
        Image hiddenBg = hiddenInputGO.AddComponent<Image>();
        hiddenBg.color = Color.clear;
        hiddenBg.raycastTarget = true;

        // Text Area (нужен для TMP_InputField)
        var textAreaGO = new GameObject("Text Area");
        textAreaGO.transform.SetParent(hiddenInputGO.transform, false);

        RectTransform textAreaRT = textAreaGO.AddComponent<RectTransform>();
        textAreaRT.anchorMin = Vector2.zero;
        textAreaRT.anchorMax = Vector2.one;
        textAreaRT.offsetMin = Vector2.zero;
        textAreaRT.offsetMax = Vector2.zero;
        textAreaGO.AddComponent<RectMask2D>();

        // Text компонент (прозрачный)
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
        hiddenText.raycastTarget = false;

        // TMP_InputField
        TMP_InputField inputField = hiddenInputGO.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRT;
        inputField.textComponent = hiddenText;
        inputField.characterLimit = 6;
        inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        inputField.caretColor = Color.clear;
        inputField.selectionColor = Color.clear;

        // Добавляем OtpInputBox компонент
        OTPInputFieldView otpComponent = root.AddComponent<OTPInputFieldView>();

        // Присваиваем ссылки через SerializedObject
        var so = new SerializedObject(otpComponent);

        so.FindProperty("hiddenInput").objectReferenceValue = inputField;

        SerializedProperty slotTextsProp = so.FindProperty("slotTexts");
        slotTextsProp.arraySize = 6;

        for (var i = 0; i < 6; i++)
            slotTextsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotTexts[i];

        SerializedProperty slotBgsProp = so.FindProperty("slotBackgrounds");
        slotBgsProp.arraySize = 6;

        for (var i = 0; i < 6; i++)
            slotBgsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotBackgrounds[i];

        so.ApplyModifiedProperties();

        // Выделяем созданный объект
        Selection.activeGameObject = root;

        Debug.Log("[OtpInputBox] Создан! Настрой цвета в Inspector если нужно.");
    }
}
#endif

