using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Fonts;
using ECS.Unity.ColorComponent;
using System;
using TMPro;
using UnityEngine;

namespace DCL.SDKComponents.TextShape
{
    public static class TMPProSdkExtensions
    {
        private static readonly int ID_OUTLINE_COLOR = Shader.PropertyToID("_OutlineColor");
        private static readonly int ID_OUTLINE_WIDTH = Shader.PropertyToID("_OutlineWidth");
        private static readonly int ID_UNDERLAY_COLOR = Shader.PropertyToID("_UnderlayColor");
        private static readonly int ID_UNDERLAY_SOFTNESS = Shader.PropertyToID("_UnderlaySoftness");
        private static readonly int ID_UNDERLAY_OFFSET_Y = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int ID_UNDERLAY_OFFSET_X = Shader.PropertyToID("_UnderlayOffsetX");

         public static void Apply(this TextMeshPro tmpText, PBTextShape textShape, IFontsStorage fontsStorage, MaterialPropertyBlock materialPropertyBlock)
        {
            tmpText.font = fontsStorage.Font(textShape.Font) ?? tmpText.font;

            // NOTE: previously width and height weren't working (setting sizeDelta before anchors and offset result in sizeDelta being reset to 0,0)
            tmpText.rectTransform.anchorMin = Vector2.zero;
            tmpText.rectTransform.anchorMax = Vector2.one;
            tmpText.rectTransform.offsetMin = Vector2.zero;
            tmpText.rectTransform.offsetMax = Vector2.zero;

            // to fix textWrapping and avoid backwards compatibility issues as result of the size being properly set (like text alignment) we only set it if textWrapping is enabled.
            float width = textShape.HasWidth ? textShape.Width : 1f;
            float height = textShape.HasHeight ? textShape.Height : 0.2f;
            tmpText.rectTransform.rect.Set(0, 0, width, height);

            tmpText.rectTransform.sizeDelta = textShape.TextWrapping ? new Vector2(width, height) : Vector2.zero;

            tmpText.text = textShape.Text;

            tmpText.color = textShape.TextColor?.ToUnityColor() ?? Color.white;
            tmpText.fontSize = textShape.HasFontSize? (int)textShape.FontSize : 10; // in unity-renderer the default font size is 100
            tmpText.richText = true;
            tmpText.overflowMode = TextOverflowModes.Overflow;
            tmpText.enableAutoSizing = textShape.HasFontAutoSize ? textShape.FontAutoSize : tmpText.fontSize == 0;

            tmpText.margin = new Vector4(
                (int)textShape.PaddingLeft,
                (int)textShape.PaddingTop,
                (int)textShape.PaddingRight,
                (int)textShape.PaddingBottom
            );

            tmpText.alignment = textShape.HasTextAlign ? TextAlignmentOptions(textShape.TextAlign) : TMPro.TextAlignmentOptions.Center;
            tmpText.lineSpacing = textShape.HasLineSpacing ? textShape.LineSpacing : 0f;

            tmpText.maxVisibleLines = textShape.HasLineCount && textShape.LineCount != 0 ? Mathf.Max(textShape.LineCount, 1) : int.MaxValue;
            tmpText.enableWordWrapping = textShape is { HasTextWrapping: true, TextWrapping: true } && !tmpText.enableAutoSizing;

            tmpText.renderer.SetPropertyBlock(
                materialPropertyBlock.Prepare(textShape));
        }

        private static MaterialPropertyBlock Prepare(this MaterialPropertyBlock materialPropertyBlock, PBTextShape textShape)
        {
            materialPropertyBlock.Clear();

            // TODO (Vit) : Disable outline when if (textShape.OutlineWidth <= 0)
            materialPropertyBlock.SetColor(ID_OUTLINE_COLOR, textShape.OutlineColor?.ToUnityColor() ?? Color.white);
            materialPropertyBlock.SetFloat(ID_OUTLINE_WIDTH, textShape.OutlineWidth);

            // TODO (Vit ): disable shadow when if (textShape.ShadowOffsetX == 0 || textShape.ShadowOffsetY == 0)
            materialPropertyBlock.SetColor(ID_UNDERLAY_COLOR, textShape.ShadowColor?.ToUnityColor() ?? Color.white);
            materialPropertyBlock.SetFloat(ID_UNDERLAY_SOFTNESS, textShape.ShadowBlur);
            materialPropertyBlock.SetFloat(ID_UNDERLAY_OFFSET_X, textShape.ShadowOffsetX);
            materialPropertyBlock.SetFloat(ID_UNDERLAY_OFFSET_Y, textShape.ShadowOffsetY);

            return materialPropertyBlock;
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
