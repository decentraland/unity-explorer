using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Fonts;
using DCL.Utilities.Extensions;
using ECS.Unity.ColorComponent;
using System;
using TMPro;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.Renderer
{
    public class TMPTextShapeRenderer : ITextShapeRenderer
    {
        private static readonly int ID_OUTLINE_COLOR = Shader.PropertyToID("_OutlineColor");
        private static readonly int ID_OUTLINE_WIDTH = Shader.PropertyToID("_OutlineWidth");
        private static readonly int ID_UNDERLAY_COLOR = Shader.PropertyToID("_UnderlayColor");
        private static readonly int ID_UNDERLAY_SOFTNESS = Shader.PropertyToID("_UnderlaySoftness");
        private static readonly int ID_UNDERLAY_OFFSET_Y = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int ID_UNDERLAY_OFFSET_X = Shader.PropertyToID("_UnderlayOffsetX");
        private readonly TMP_Text tmpText;
        private readonly MeshRenderer meshRenderer;
        private readonly MaterialPropertyBlock materialPropertyBlock;
        private readonly RectTransform rectTransform;
        private readonly IFontsStorage fontsStorage;

        public TMPTextShapeRenderer(TMP_Text tmp, IFontsStorage fontsStorage) : this(
            tmp,
            tmp.GetComponent<MeshRenderer>().EnsureNotNull(),
            new MaterialPropertyBlock(),
            tmp.GetComponent<RectTransform>().EnsureNotNull(),
            fontsStorage
        ) { }

        public TMPTextShapeRenderer(
            TMP_Text tmpText,
            MeshRenderer meshRenderer,
            MaterialPropertyBlock materialPropertyBlock,
            RectTransform rectTransform,
            IFontsStorage fontsStorage
        )
        {
            this.tmpText = tmpText;
            this.meshRenderer = meshRenderer;
            this.materialPropertyBlock = materialPropertyBlock;
            this.rectTransform = rectTransform;
            this.fontsStorage = fontsStorage;
        }

        public void Apply(PBTextShape textShape)
        {
            tmpText.font = fontsStorage.Font(textShape.Font) ?? tmpText.font;

            // NOTE: previously width and height weren't working (setting sizeDelta before anchors and offset result in sizeDelta being reset to 0,0)
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            // to fix textWrapping and avoid backwards compatibility issues as result of the size being properly set (like text alignment) we only set it if textWrapping is enabled.
            float width = textShape.HasWidth ? textShape.Width : 1f;
            float height = textShape.HasHeight ? textShape.Height : 0.2f;
            rectTransform.rect.Set(0, 0, width, height);

            rectTransform.sizeDelta = textShape.TextWrapping ? new Vector2(width, height) : Vector2.zero;

            tmpText.text = textShape.Text;

            tmpText.color = textShape.TextColor?.ToUnityColor() ?? Color.white;
            if (textShape.HasFontSize) tmpText.fontSize = (int)textShape.FontSize; // in unity-renderer the default font size is 100
            tmpText.richText = true;
            tmpText.overflowMode = TextOverflowModes.Overflow;
            tmpText.enableAutoSizing = textShape.HasFontSize ? textShape.FontAutoSize : tmpText.fontSize == 0;

            tmpText.margin = new Vector4(
                (int)textShape.PaddingLeft,
                (int)textShape.PaddingTop,
                (int)textShape.PaddingRight,
                (int)textShape.PaddingBottom
            );

            tmpText.alignment = textShape.HasTextAlign ? TextAlignmentOptions(textShape.TextAlign) : TMPro.TextAlignmentOptions.BottomLeft;
            tmpText.lineSpacing = textShape.HasLineSpacing ? textShape.LineSpacing : 0f;

            tmpText.maxVisibleLines = textShape.HasLineCount && textShape.LineCount != 0 ? Mathf.Max(textShape.LineCount, 1) : int.MaxValue;
            tmpText.enableWordWrapping = textShape is { HasTextWrapping: true, TextWrapping: true } && !tmpText.enableAutoSizing;

            // TODO (Vit) : Disable outline when if (textShape.OutlineWidth <= 0)
            materialPropertyBlock.SetColor(ID_OUTLINE_COLOR, textShape.OutlineColor?.ToUnityColor() ?? Color.white);
            materialPropertyBlock.SetFloat(ID_OUTLINE_WIDTH, textShape.OutlineWidth);

            // TODO (Vit ): disable shadow when if (textShape.ShadowOffsetX == 0 || textShape.ShadowOffsetY == 0)
            materialPropertyBlock.SetColor(ID_UNDERLAY_COLOR, textShape.ShadowColor?.ToUnityColor() ?? Color.white);
            materialPropertyBlock.SetFloat(ID_UNDERLAY_SOFTNESS, textShape.ShadowBlur);
            materialPropertyBlock.SetFloat(ID_UNDERLAY_OFFSET_X, textShape.ShadowOffsetX);
            materialPropertyBlock.SetFloat(ID_UNDERLAY_OFFSET_Y, textShape.ShadowOffsetY);

            meshRenderer.SetPropertyBlock(materialPropertyBlock);
        }

        public void Show()
        {
            tmpText.enabled = true;
        }

        public void Hide()
        {
            tmpText.enabled = false;
        }

        private static TextAlignmentOptions TextAlignmentOptions(TextAlignMode mode) =>
            mode switch
            {
                TextAlignMode.TamTopLeft => TMPro.TextAlignmentOptions.TopLeft,
                TextAlignMode.TamTopCenter => TMPro.TextAlignmentOptions.Top,
                TextAlignMode.TamTopRight => TMPro.TextAlignmentOptions.TopRight,
                TextAlignMode.TamMiddleLeft => TMPro.TextAlignmentOptions.Left,
                TextAlignMode.TamMiddleCenter => TMPro.TextAlignmentOptions.Center,
                TextAlignMode.TamMiddleRight => TMPro.TextAlignmentOptions.Right,
                TextAlignMode.TamBottomLeft => TMPro.TextAlignmentOptions.BottomLeft,
                TextAlignMode.TamBottomCenter => TMPro.TextAlignmentOptions.Bottom,
                TextAlignMode.TamBottomRight => TMPro.TextAlignmentOptions.BottomRight,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, $"Mode {mode} is not supported"),
            };

        public override string ToString() =>
            $"{nameof(TMPTextShapeRenderer)} on {tmpText.gameObject.name}";
    }
}
