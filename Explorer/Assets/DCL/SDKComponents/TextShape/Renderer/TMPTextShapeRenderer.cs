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
        private readonly TMP_Text tmpText;
        private readonly MeshRenderer meshRenderer;
        private readonly MaterialPropertyBlock materialPropertyBlock;
        private readonly RectTransform rectTransform;
        private readonly IFontsStorage fontsStorage;
        private static readonly int ID_OUTLINE_COLOR = Shader.PropertyToID("_OutlineColor");
        private static readonly int ID_OUTLINE_WIDTH = Shader.PropertyToID("_OutlineWidth");
        private static readonly int ID_UNDERLAY_COLOR = Shader.PropertyToID("_UnderlayColor");
        private static readonly int ID_UNDERLAY_SOFTNESS = Shader.PropertyToID("_UnderlaySoftness");
        private static readonly int ID_UNDERLAY_OFFSET_Y = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int ID_UNDERLAY_OFFSET_X = Shader.PropertyToID("_UnderlayOffsetX");

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
            tmpText.text = textShape.Text;
            tmpText.font = fontsStorage.Font(textShape.Font) ?? tmpText.font;

            if (textShape.HasFontSize)
                tmpText.enableAutoSizing = textShape.FontAutoSize;

            if (textShape.HasFontSize)
                tmpText.fontSize = textShape.FontSize;

            if (textShape.HasLineSpacing)
                tmpText.lineSpacing = textShape.LineSpacing;

            if (textShape.HasLineCount)
                tmpText.maxVisibleLines = textShape.LineCount;

            if (textShape.OutlineColor is not null)
                materialPropertyBlock.SetColor(ID_OUTLINE_COLOR, textShape.OutlineColor.ToUnityColor());

            materialPropertyBlock.SetFloat(ID_OUTLINE_WIDTH, textShape.OutlineWidth);

            if (textShape.HasTextAlign)
                tmpText.alignment = TextAlignmentOptions(textShape.TextAlign);

            if (textShape.HasTextWrapping)
                tmpText.enableWordWrapping = textShape.TextWrapping;

            if (textShape.TextColor is not null)
                tmpText.color = textShape.TextColor.ToUnityColor();

            tmpText.margin = new Vector4(
                textShape.PaddingLeft,
                textShape.PaddingTop,
                textShape.PaddingRight,
                textShape.PaddingBottom
            );

            if (textShape.ShadowColor is not null)
                materialPropertyBlock.SetColor(ID_UNDERLAY_COLOR, textShape.ShadowColor.ToUnityColor());

            materialPropertyBlock.SetFloat(ID_UNDERLAY_SOFTNESS, textShape.ShadowBlur);
            materialPropertyBlock.SetFloat(ID_UNDERLAY_OFFSET_X, textShape.ShadowOffsetX);
            materialPropertyBlock.SetFloat(ID_UNDERLAY_OFFSET_Y, textShape.ShadowOffsetY);

            meshRenderer.SetPropertyBlock(materialPropertyBlock);

            rectTransform.sizeDelta = new Vector2(textShape.Width, textShape.Height);
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
