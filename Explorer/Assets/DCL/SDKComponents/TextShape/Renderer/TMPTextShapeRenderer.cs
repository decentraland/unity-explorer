using DCL.ECSComponents;
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
        private static readonly int ID_OUTLINE_COLOR = Shader.PropertyToID("_OutlineColor");
        private static readonly int ID_OUTLINE_WIDTH = Shader.PropertyToID("_OutlineWidth");
        private static readonly int ID_SCALE_RATIO_A_USE_OUTLINE = Shader.PropertyToID("_ScaleRatioA");

        public TMPTextShapeRenderer(TMP_Text tmpText, MeshRenderer meshRenderer, MaterialPropertyBlock materialPropertyBlock)
        {
            this.tmpText = tmpText;
            this.meshRenderer = meshRenderer;
            this.materialPropertyBlock = materialPropertyBlock;
            UseOutline();
        }

        public void Apply(PBTextShape textShape)
        {
            tmpText.text = textShape.Text;

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

            meshRenderer.SetPropertyBlock(materialPropertyBlock);

            Debug.LogWarning("Applying is not finished");
            /*
                //TODO//
                //Frame
                //tmpText.font = textShape.Font;

                Width = 100,
                Height = 1,

                //Shadow
                ShadowColor = new Color3 { B = 1, G = 1, R = 1 },
                ShadowBlur = 10,
                ShadowOffsetX = 10,
                ShadowOffsetY = 10,

             */
        }

        public void Hide()
        {
            tmpText.enabled = false;
        }

        public void Show()
        {
            tmpText.enabled = true;
        }

        private void UseOutline()
        {
            materialPropertyBlock.SetFloat(ID_SCALE_RATIO_A_USE_OUTLINE, 1);
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
    }
}
