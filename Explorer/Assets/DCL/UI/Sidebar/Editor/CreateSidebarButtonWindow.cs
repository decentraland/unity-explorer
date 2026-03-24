using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Sidebar.Editor
{
    public class CreateSidebarButtonWindow : EditorWindow
    {
        private const string BOTTOM_LAYOUT_PATH = "Assets/DCL/UI/Sidebar/SidebarUI.BottomLayout.prefab";
        private const string SIDEBAR_BUTTON_PATH = "Assets/DCL/UI/Sidebar/SidebarButton.prefab";

        private string buttonName = "NewButton";
        private Sprite defaultSprite;
        private Sprite hoverSprite;
        private int siblingIndex;
        private bool addDivAfter = true;
        private Vector2 buttonSize = new (34, 34);
        private Vector2 iconSize = new (34, 34);

        [MenuItem("Decentraland/Create/Sidebar Button")]
        private static void ShowWindow()
        {
            var window = GetWindow<CreateSidebarButtonWindow>("Create Sidebar Button");
            window.minSize = new Vector2(350, 300);

            var bottomLayout = AssetDatabase.LoadAssetAtPath<GameObject>(BOTTOM_LAYOUT_PATH);

            if (bottomLayout != null)
                window.siblingIndex = bottomLayout.transform.childCount;
        }

        private void OnGUI()
        {
            GUILayout.Label("Create Sidebar Button", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            buttonName = EditorGUILayout.TextField("Button Name", buttonName);

            EditorGUILayout.Space(4);
            GUILayout.Label("Sprites", EditorStyles.boldLabel);
            defaultSprite = (Sprite)EditorGUILayout.ObjectField("Default (Unhover)", defaultSprite, typeof(Sprite), false);
            hoverSprite = (Sprite)EditorGUILayout.ObjectField("Hover", hoverSprite, typeof(Sprite), false);

            EditorGUILayout.Space(4);
            GUILayout.Label("Layout", EditorStyles.boldLabel);
            buttonSize = EditorGUILayout.Vector2Field("Button Size", buttonSize);
            iconSize = EditorGUILayout.Vector2Field("Icon Size", iconSize);
            siblingIndex = EditorGUILayout.IntField("Sibling Index", siblingIndex);
            addDivAfter = EditorGUILayout.Toggle("Add Divider After", addDivAfter);

            EditorGUILayout.Space(4);

            DrawPreview();

            EditorGUILayout.Space(8);

            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(buttonName) || defaultSprite == null || hoverSprite == null);

            if (GUILayout.Button("Create Button", GUILayout.Height(30)))
                CreateButton();

            EditorGUI.EndDisabledGroup();
        }

        private void DrawPreview()
        {
            var bottomLayout = AssetDatabase.LoadAssetAtPath<GameObject>(BOTTOM_LAYOUT_PATH);

            if (bottomLayout == null)
            {
                EditorGUILayout.HelpBox($"BottomLayout prefab not found at:\n{BOTTOM_LAYOUT_PATH}", MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox(
                $"Will insert '{buttonName}' at index {siblingIndex} in {bottomLayout.name}\n" +
                $"Current children: {bottomLayout.transform.childCount}",
                MessageType.Info);
        }

        private void CreateButton()
        {
            var bottomLayoutPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BOTTOM_LAYOUT_PATH);

            if (bottomLayoutPrefab == null)
            {
                Debug.LogError($"[CreateSidebarButton] BottomLayout prefab not found at {BOTTOM_LAYOUT_PATH}");
                return;
            }

            var sidebarButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SIDEBAR_BUTTON_PATH);

            if (sidebarButtonPrefab == null)
            {
                Debug.LogError($"[CreateSidebarButton] SidebarButton prefab not found at {SIDEBAR_BUTTON_PATH}");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(bottomLayoutPrefab);
            var contentsRoot = PrefabUtility.LoadPrefabContents(assetPath);

            try
            {
                // Check for duplicate name
                if (contentsRoot.transform.Find(buttonName) != null)
                {
                    Debug.LogError($"[CreateSidebarButton] Button '{buttonName}' already exists in {contentsRoot.name}");
                    return;
                }

                // Instantiate SidebarButton as prefab instance
                var buttonGO = (GameObject)PrefabUtility.InstantiatePrefab(sidebarButtonPrefab, contentsRoot.transform);
                buttonGO.name = buttonName;

                ConfigureRootButton(buttonGO);
                ConfigureChildImages(buttonGO);

                int clampedIndex = Mathf.Clamp(siblingIndex, 0, contentsRoot.transform.childCount - 1);
                buttonGO.transform.SetSiblingIndex(clampedIndex);

                if (addDivAfter)
                    CreateDivider(contentsRoot.transform, clampedIndex + 1);

                PrefabUtility.SaveAsPrefabAsset(contentsRoot, assetPath);
                Debug.Log($"[CreateSidebarButton] Created '{buttonName}' at index {clampedIndex} in {contentsRoot.name}");
            }
            finally { PrefabUtility.UnloadPrefabContents(contentsRoot); }

            AssetDatabase.Refresh();
        }

        private void ConfigureRootButton(GameObject buttonGO)
        {
            // RectTransform
            var rt = buttonGO.GetComponent<RectTransform>();
            rt.sizeDelta = buttonSize;

            // Button — match other sidebar buttons
            var button = buttonGO.GetComponent<Button>();

            if (button != null)
            {
                var colors = button.colors;
                colors.normalColor = new Color(1f, 1f, 1f, 0f);
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.031f);
                colors.pressedColor = new Color(1f, 1f, 1f, 0f);
                colors.selectedColor = new Color(1f, 1f, 1f, 0.031f);
                colors.disabledColor = new Color(0.784f, 0.784f, 0.784f, 0.502f);
                colors.fadeDuration = 0f;
                colors.colorMultiplier = 1f;
                button.colors = colors;

                var selBg = buttonGO.transform.Find("SelectedBackground");

                if (selBg != null)
                    button.targetGraphic = selBg.GetComponent<Image>();
            }
        }

        private void ConfigureChildImages(GameObject buttonGO)
        {
            var snowColor = new Color(0.988f, 0.988f, 0.988f, 1f);

            SetChildImage(buttonGO, "SelectedBackground", null, Color.white, buttonSize);
            SetChildImage(buttonGO, "UnselectedImage", defaultSprite, snowColor, iconSize);
            SetChildImage(buttonGO, "SelectedImage", hoverSprite, Color.white, iconSize);
            SetChildImage(buttonGO, "HoverSprites", hoverSprite, Color.white, iconSize);
            SetChildImage(buttonGO, "UnhoverSprites", defaultSprite, Color.white, iconSize);

            // Tooltip — inactive by default
            var tooltip = buttonGO.transform.Find("Tooltip");

            if (tooltip != null)
                tooltip.gameObject.SetActive(false);
        }

        private static void SetChildImage(GameObject parent, string childName, Sprite sprite, Color color, Vector2 size)
        {
            var child = parent.transform.Find(childName);

            if (child == null)
                return;

            var rt = child.GetComponent<RectTransform>();

            if (rt != null)
                rt.sizeDelta = size;

            var img = child.GetComponent<Image>();

            if (img == null)
                return;

            if (sprite != null)
                img.sprite = sprite;

            img.color = color;
        }

        private static void CreateDivider(Transform parent, int index)
        {
            // Find existing Div to clone style from
            Transform existingDiv = null;

            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == "Div")
                {
                    existingDiv = parent.GetChild(i);
                    break;
                }
            }

            if (existingDiv != null)
            {
                var newDiv = Object.Instantiate(existingDiv.gameObject, parent);
                newDiv.name = "Div";
                newDiv.transform.SetSiblingIndex(index);
            }
            else
            {
                // Create from scratch if no Div exists
                var divGO = new GameObject("Div", typeof(RectTransform), typeof(Image));
                divGO.transform.SetParent(parent, false);
                divGO.layer = LayerMask.NameToLayer("UI");

                var rt = divGO.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(40f, 1f);

                var img = divGO.GetComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.051f);
                img.raycastTarget = false;

                divGO.transform.SetSiblingIndex(index);
            }
        }
    }
}
