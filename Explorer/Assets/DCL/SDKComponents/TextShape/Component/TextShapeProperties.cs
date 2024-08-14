using DCL.ECSComponents;
using ECS.Unity.ColorComponent;
using System;
using UnityEngine;
using Font = DCL.ECSComponents.Font;

namespace DCL.SDKComponents.TextShape.Component
{
    [Serializable]
    public class TextShapeProperties
    {
        [Header("Font")]
        public Font font = Font.FSerif;
        public bool fontAutoSize;
        public float fontSize = 10;

        [Header("Frame")]
        public float width = 100;
        public float height = 1;

        [Header("Lines")]
        public int lineCount = 2;
        public float lineSpacing = 10;

        [Header("Outlines")]
        public Color outlineColor = Color.black;
        [Range(0, 1)]
        public float outlineWidth = 0.3f;

        [Header("Padding")]
        public float paddingTop = 1;
        public float paddingBottom = 1;
        public float paddingLeft = 1;
        public float paddingRight = 1;

        [Header("Shadows")]
        public Color shadowColor = Color.white;
        public float shadowBlur = 10;
        public float shadowOffsetX = 10;
        public float shadowOffsetY = 10;

        [Header("Text")]
        [Multiline]
        public string text = "Demo";
        public TextAlignMode textAlign = TextAlignMode.TamMiddleCenter;
        public bool textWrapping = true;
        public Color textColor = Color.white;
    }

    public static class TextShapePropertiesExtensions
    {
        public static void ApplyOn(this TextShapeProperties properties, PBTextShape textShape)
        {
            textShape.Font = properties.font;
            textShape.FontAutoSize = properties.fontAutoSize;
            textShape.FontSize = properties.fontSize;

            //TODO solve issue with cannot set 'Has' values to PBTextShape, due it readonly, but they are required due logic

            //Frame
            textShape.Width = properties.width;
            textShape.Height = properties.height;

            //Lines
            textShape.LineCount = properties.lineCount;
            textShape.LineSpacing = properties.lineSpacing;

            //Outline
            textShape.OutlineColor = properties.outlineColor.ToColor3();
            textShape.OutlineWidth = properties.outlineWidth;

            //Padding
            textShape.PaddingTop = properties.paddingTop;
            textShape.PaddingBottom = properties.paddingBottom;
            textShape.PaddingLeft = properties.paddingLeft;
            textShape.PaddingRight = properties.paddingRight;

            //Shadow
            textShape.ShadowColor = properties.shadowColor.ToColor3();
            textShape.ShadowBlur = properties.shadowBlur;
            textShape.ShadowOffsetX = properties.shadowOffsetX;
            textShape.ShadowOffsetY = properties.shadowOffsetY;

            //Text
            textShape.Text = properties.text;
            textShape.TextAlign = properties.textAlign;
            textShape.TextWrapping = properties.textWrapping;
            textShape.TextColor = properties.textColor.ToColor4();

            textShape.IsDirty = true;
        }
    }
}
