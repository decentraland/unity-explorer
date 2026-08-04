using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UUAV.Example
{
    /// <summary>
    /// Stress-test harness for UUAV: spawns any number of players against a URL and
    /// shows each one as a cell in a scrollable 2D grid with per-player
    /// Stop / Mute / Remove controls. All UI is built in code; the scene only needs
    /// a Canvas, an EventSystem and this component.
    /// </summary>
    public sealed class UUAVStressTestController : MonoBehaviour
    {
        private const float TOP_BAR_HEIGHT = 64f;
        private static readonly Color PANEL_COLOR = new Color(0.10f, 0.10f, 0.12f, 0.95f);
        private static readonly Color BUTTON_COLOR = new Color(0.25f, 0.27f, 0.32f, 1f);
        private static readonly Color CELL_COLOR = new Color(0.14f, 0.14f, 0.17f, 1f);

        [SerializeField] private string defaultUrl = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4";

        private readonly List<PlayerCell> cells = new List<PlayerCell>();
        private int nextId;

        private TMP_InputField urlInput;
        private TMP_Text infoLabel;
        private RectTransform gridContent;

        private void Awake()
        {
            var canvas = FindAnyObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "UUAVStressTestController requires a Canvas in the scene");

            BuildTopBar(canvas.transform);
            BuildGrid(canvas.transform);
            RefreshInfoLabel();
        }

        private void Update()
        {
            for (int i = 0; i < cells.Count; i++)
                cells[i].Refresh();
        }

        private void OnDestroy()
        {
            RemoveAll();
        }

        private void AddPlayer()
        {
            string url = urlInput.text;

            if (string.IsNullOrWhiteSpace(url))
            {
                infoLabel.text = "Enter a URL first";
                return;
            }

            var player = UUAVPlayer.New();
            player.OpenMedia(url);
            player.Play(); // play intent is latched natively, safe right after the async open

            cells.Add(new PlayerCell(nextId++, player, gridContent, RemovePlayer));
            RefreshInfoLabel();
        }

        private void AddPlayers(int count)
        {
            for (int i = 0; i < count; i++)
                AddPlayer();
        }

        private void RemovePlayer(PlayerCell cell)
        {
            cell.Dispose();
            cells.Remove(cell);
            RefreshInfoLabel();
        }

        private void RemoveAll()
        {
            for (int i = cells.Count - 1; i >= 0; i--)
                RemovePlayer(cells[i]);
        }

        private void RefreshInfoLabel()
        {
            infoLabel.text = $"Players: {cells.Count}";
        }

        private void BuildTopBar(Transform canvasRoot)
        {
            RectTransform bar = CreatePanel("TopBar", canvasRoot, PANEL_COLOR);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(0f, TOP_BAR_HEIGHT);

            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            urlInput = CreateUrlInput(bar);
            urlInput.text = defaultUrl;

            CreateButton(bar, "Add Player", 120f, AddPlayer);
            CreateButton(bar, "Add 5", 80f, () => AddPlayers(5));
            CreateButton(bar, "Remove All", 120f, RemoveAll);

            infoLabel = CreateText(bar, "Players: 0", 18f);
            infoLabel.alignment = TextAlignmentOptions.MidlineRight;
            var infoElement = infoLabel.gameObject.AddComponent<LayoutElement>();
            infoElement.minWidth = 180f;
        }

        private void BuildGrid(Transform canvasRoot)
        {
            RectTransform scrollArea = CreatePanel("PlayerGrid", canvasRoot, new Color(0.05f, 0.05f, 0.06f, 1f));
            scrollArea.anchorMin = Vector2.zero;
            scrollArea.anchorMax = Vector2.one;
            scrollArea.offsetMin = Vector2.zero;
            scrollArea.offsetMax = new Vector2(0f, -TOP_BAR_HEIGHT);

            var scrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.SetParent(scrollArea, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var content = new GameObject("Content", typeof(RectTransform));
            gridContent = (RectTransform)content.transform;
            gridContent.SetParent(viewportRect, false);
            gridContent.anchorMin = new Vector2(0f, 1f);
            gridContent.anchorMax = new Vector2(1f, 1f);
            gridContent.pivot = new Vector2(0.5f, 1f);
            gridContent.anchoredPosition = Vector2.zero;
            gridContent.sizeDelta = Vector2.zero;

            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(320f, 230f);
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.childAlignment = TextAnchor.UpperLeft;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = gridContent;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;
        }

        private static RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return rect;
        }

        private static TMP_Text CreateText(Transform parent, string text, float fontSize)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private static void CreateButton(Transform parent, string label, float minWidth, UnityAction onClick)
            => CreateButton(parent, label, minWidth, onClick, out TMP_Text _);

        private static void CreateButton(Transform parent, string label, float minWidth, UnityAction onClick, out TMP_Text labelText)
        {
            var go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = BUTTON_COLOR;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var element = go.AddComponent<LayoutElement>();
            element.minWidth = minWidth;
            element.minHeight = 28f;

            labelText = CreateText(go.transform, label, 16f);
            labelText.alignment = TextAlignmentOptions.Center;
            var textRect = (RectTransform)labelText.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private static TMP_InputField CreateUrlInput(Transform parent)
        {
            GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = "UrlInput";
            go.transform.SetParent(parent, false);

            var element = go.AddComponent<LayoutElement>();
            element.minWidth = 320f;
            element.flexibleWidth = 1f;

            var input = go.GetComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;
            ((TMP_Text)input.placeholder).text = "Media URL (http/https)";
            return input;
        }

        /// <summary>
        /// One grid cell owning one <see cref="UUAVPlayer"/>. Plain class driven by the
        /// controller's Update; the id shown on the cell is the controller-assigned player id.
        /// </summary>
        private sealed class PlayerCell
        {
            public readonly int Id;

            private readonly UUAVPlayer player;
            private readonly GameObject root;
            private readonly RawImage surface;
            private readonly AspectRatioFitter aspectFitter;
            private readonly TMP_Text statusLabel;
            private readonly TMP_Text muteLabel;

            private bool muted;
            private UUAVState lastState = UUAVState.Unknown;
            private int lastTime = -1;
            private int lastDuration = -1;
            private int lastTexWidth;
            private int lastTexHeight;

            public PlayerCell(int id, UUAVPlayer player, RectTransform parent, Action<PlayerCell> onRemove)
            {
                Id = id;
                this.player = player;

                root = new GameObject($"PlayerCell_{id}", typeof(RectTransform), typeof(Image));
                root.transform.SetParent(parent, false);
                root.GetComponent<Image>().color = CELL_COLOR;

                var layout = root.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(4, 4, 4, 4);
                layout.spacing = 4f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                var videoFrame = new GameObject("VideoFrame", typeof(RectTransform), typeof(Image));
                videoFrame.transform.SetParent(root.transform, false);
                videoFrame.GetComponent<Image>().color = Color.black;
                var frameElement = videoFrame.AddComponent<LayoutElement>();
                frameElement.preferredHeight = 180f;
                frameElement.flexibleHeight = 1f;

                var surfaceGo = new GameObject("Surface", typeof(RectTransform));
                surfaceGo.transform.SetParent(videoFrame.transform, false);
                surface = surfaceGo.AddComponent<RawImage>();
                surface.raycastTarget = false;
                surface.enabled = false; // a RawImage with no texture renders as a white rect
                aspectFitter = surfaceGo.AddComponent<AspectRatioFitter>();
                aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspectFitter.aspectRatio = 16f / 9f;

                statusLabel = CreateText(root.transform, $"#{id}  Opening", 16f);
                var statusElement = statusLabel.gameObject.AddComponent<LayoutElement>();
                statusElement.minHeight = 20f;

                var buttonRow = new GameObject("Buttons", typeof(RectTransform));
                buttonRow.transform.SetParent(root.transform, false);
                var rowLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 4f;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = true;
                rowLayout.childForceExpandHeight = true;
                var rowElement = buttonRow.AddComponent<LayoutElement>();
                rowElement.minHeight = 28f;

                CreateButton(buttonRow.transform, "Stop", 0f, Stop);
                CreateButton(buttonRow.transform, "Mute", 0f, ToggleMute, out muteLabel);
                CreateButton(buttonRow.transform, "Remove", 0f, () => onRemove(this));
            }

            public void Refresh()
            {
                RenderTexture texture = player.CurrentTexture;
                surface.texture = texture;
                surface.enabled = texture != null;

                if (texture != null && (texture.width != lastTexWidth || texture.height != lastTexHeight))
                {
                    lastTexWidth = texture.width;
                    lastTexHeight = texture.height;
                    aspectFitter.aspectRatio = (float)texture.width / texture.height;
                }

                UUAVState state = player.State;
                int time = (int)player.CurrentTime;
                int duration = (int)player.Duration;

                if (state == lastState && time == lastTime && duration == lastDuration)
                    return;

                lastState = state;
                lastTime = time;
                lastDuration = duration;

                statusLabel.text = $"#{Id}  {state.ToStringNoAlloc()}  {time}s / {duration}s";

                statusLabel.color = state switch
                {
                    UUAVState.Error => new Color(1f, 0.35f, 0.35f, 1f),
                    UUAVState.Opening => Color.gray,
                    _ => Color.white,
                };
            }

            public void Dispose()
            {
                // the player's OnDestroy releases the RenderTexture behind CurrentTexture;
                // drop our reference first so the RawImage never samples a dead texture
                surface.texture = null;
                Destroy(player.gameObject);
                Destroy(root);
            }

            private void Stop()
            {
                player.Pause();
                player.Seek(0);
            }

            private void ToggleMute()
            {
                muted = !muted;
                player.AudioSource.mute = muted;
                muteLabel.text = muted ? "Unmute" : "Mute";
            }
        }
    }
}
