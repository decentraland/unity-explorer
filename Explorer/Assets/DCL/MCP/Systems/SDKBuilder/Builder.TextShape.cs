using DCL.ECSComponents;
using DCL.Optimization.Pools;
using Decentraland.Common;

namespace DCL.MCP.Systems
{
    public partial class MCPSceneEntitiesBuilder
    {
        public MCPSceneEntitiesBuilder AddTextShape(
            IComponentPool<PBTextShape> pool,
            string text,
            float fontSize = 5f,
            Font font = Font.FSansSerif,
            bool fontAutoSize = false,
            TextAlignMode textAlign = TextAlignMode.TamTopLeft,
            float width = 4f,
            float height = 2f,
            float paddingTop = 0f,
            float paddingRight = 0f,
            float paddingBottom = 0f,
            float paddingLeft = 0f,
            float lineSpacing = 0f,
            int lineCount = 0,
            bool textWrapping = false,
            float shadowBlur = 0f,
            float shadowOffsetX = 0f,
            float shadowOffsetY = 0f,
            float outlineWidth = 0.1f,
            Color3? shadowColor = null,
            Color3? outlineColor = null,
            Color4? textColor = null)
        {
            // Resolve default colors if not provided
            Color3 resolvedShadowColor = shadowColor ?? new Color3 { R = 1, G = 1, B = 1 };
            Color3 resolvedOutlineColor = outlineColor ?? new Color3 { R = 0, G = 0, B = 1 };
            Color4 resolvedTextColor = textColor ?? new Color4 { R = 1, G = 0, B = 0, A = 1 };

            PBTextShape textShape = pool.Get();
            textShape.Text = text;
            textShape.Font = font;
            textShape.FontSize = fontSize;
            textShape.FontAutoSize = fontAutoSize;
            textShape.TextAlign = textAlign;
            textShape.Width = width;
            textShape.Height = height;
            textShape.PaddingTop = paddingTop;
            textShape.PaddingRight = paddingRight;
            textShape.PaddingBottom = paddingBottom;
            textShape.PaddingLeft = paddingLeft;
            textShape.LineSpacing = lineSpacing;
            textShape.LineCount = lineCount;
            textShape.TextWrapping = textWrapping;
            textShape.ShadowBlur = shadowBlur;
            textShape.ShadowOffsetX = shadowOffsetX;
            textShape.ShadowOffsetY = shadowOffsetY;
            textShape.OutlineWidth = outlineWidth;
            textShape.ShadowColor = resolvedShadowColor;
            textShape.OutlineColor = resolvedOutlineColor;
            textShape.TextColor = resolvedTextColor;
            textShape.IsDirty = true;

            ecsToCRDTWriter.PutMessage<PBTextShape, (
                string Text,
                Font Font,
                float FontSize,
                bool FontAutoSize,
                TextAlignMode TextAlign,
                float Width,
                float Height,
                float PaddingTop,
                float PaddingRight,
                float PaddingBottom,
                float PaddingLeft,
                float LineSpacing,
                int LineCount,
                bool TextWrapping,
                float ShadowBlur,
                float ShadowOffsetX,
                float ShadowOffsetY,
                float OutlineWidth,
                Color3 ShadowColor,
                Color3 OutlineColor,
                Color4 TextColor)>(static (pbText, data) =>
            {
                pbText.Text = data.Text;
                pbText.Font = data.Font;
                pbText.FontSize = data.FontSize;
                pbText.FontAutoSize = data.FontAutoSize;
                pbText.TextAlign = data.TextAlign;
                pbText.Width = data.Width;
                pbText.Height = data.Height;
                pbText.PaddingTop = data.PaddingTop;
                pbText.PaddingRight = data.PaddingRight;
                pbText.PaddingBottom = data.PaddingBottom;
                pbText.PaddingLeft = data.PaddingLeft;
                pbText.LineSpacing = data.LineSpacing;
                pbText.LineCount = data.LineCount;
                pbText.TextWrapping = data.TextWrapping;
                pbText.ShadowBlur = data.ShadowBlur;
                pbText.ShadowOffsetX = data.ShadowOffsetX;
                pbText.ShadowOffsetY = data.ShadowOffsetY;
                pbText.OutlineWidth = data.OutlineWidth;
                pbText.ShadowColor = data.ShadowColor;
                pbText.OutlineColor = data.OutlineColor;
                pbText.TextColor = data.TextColor;
            }, currentCRDTEntity, (
                text,
                font,
                fontSize,
                fontAutoSize,
                textAlign,
                width,
                height,
                paddingTop,
                paddingRight,
                paddingBottom,
                paddingLeft,
                lineSpacing,
                lineCount,
                textWrapping,
                shadowBlur,
                shadowOffsetX,
                shadowOffsetY,
                outlineWidth,
                resolvedShadowColor,
                resolvedOutlineColor,
                resolvedTextColor));

            collectedComponents.Add(textShape);
            return this;
        }
    }
}
