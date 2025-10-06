using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using Decentraland.Common;
using DCL.Diagnostics;
using System.Collections.Concurrent;
using Font = DCL.ECSComponents.Font;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace DCL.MCP.Systems
{
    public partial class MCPSceneEntitiesBuilder
    {
        // ===================== TextShape: запросы и обработка =====================
        public struct MCPCreateTextShapeRequest
        {
            public string RequestId;

            // Transform
            public float X, Y, Z;
            public float SX, SY, SZ;
            public float Yaw, Pitch, Roll;
            public int ParentId;

            // Text content & style
            public string Text;
            public float FontSize;
            public string Font;
            public bool FontAutoSize;
            public string TextAlign;
            public float Width, Height;
            public float PaddingTop, PaddingRight, PaddingBottom, PaddingLeft;
            public float LineSpacing;
            public int LineCount;
            public bool TextWrapping;
            public float ShadowBlur, ShadowOffsetX, ShadowOffsetY;
            public float OutlineWidth;
            public Color3? ShadowColor;
            public Color3? OutlineColor;
            public Color4? TextColor;
        }

        private static readonly ConcurrentQueue<MCPCreateTextShapeRequest> textShapeRequests = new ();

        public static void EnqueueTextShape(in MCPCreateTextShapeRequest request) =>
            textShapeRequests.Enqueue(request);

        public void ProcessTextShapeRequests(World world, IComponentPool<PBTextShape> pool)
        {
            while (textShapeRequests.TryDequeue(out MCPCreateTextShapeRequest req))
            {
                try
                {
                    var position = new Vector3(req.X, req.Y, req.Z);
                    var scale = new Vector3(req.SX, req.SY, req.SZ);
                    var rotation = Quaternion.Euler(req.Pitch, req.Yaw, req.Roll);

                    Begin(position, scale, rotation, req.ParentId)
                       .AddTextShape(pool, req)
                       .Build(world);

                    ReportHub.Log(ReportCategory.DEBUG, $"[MCP TextShape] Created TextShape from request {req.RequestId}");
                }
                catch (System.Exception e) { ReportHub.LogError(ReportCategory.DEBUG, $"[MCP TextShape] Failed to process request {req.RequestId}: {e.Message}"); }
            }
        }

        private static TEnum EnumTryParse<TEnum>(string value, TEnum fallback) where TEnum: struct
        {
            if (!string.IsNullOrEmpty(value) && System.Enum.TryParse(value, out TEnum parsed))
                return parsed;

            return fallback;
        }

        public MCPSceneEntitiesBuilder AddTextShape(
            IComponentPool<PBTextShape> pool,
            MCPCreateTextShapeRequest req)
        {
            // Map enums from strings
            Font font = EnumTryParse(req.Font, Font.FSansSerif);
            TextAlignMode textAlign = EnumTryParse(req.TextAlign, TextAlignMode.TamTopLeft);

            // Resolve colors (defaults if not provided)
            Color3 resolvedShadowColor = req.ShadowColor ?? new Color3 { R = 1, G = 1, B = 1 };
            Color3 resolvedOutlineColor = req.OutlineColor ?? new Color3 { R = 0, G = 0, B = 1 };
            Color4 resolvedTextColor = req.TextColor ?? new Color4 { R = 1, G = 0, B = 0, A = 1 };

            PBTextShape textShape = pool.Get();
            textShape.Text = req.Text;
            textShape.Font = font;
            textShape.FontSize = req.FontSize;
            textShape.FontAutoSize = req.FontAutoSize;
            textShape.TextAlign = textAlign;
            textShape.Width = req.Width;
            textShape.Height = req.Height;
            textShape.PaddingTop = req.PaddingTop;
            textShape.PaddingRight = req.PaddingRight;
            textShape.PaddingBottom = req.PaddingBottom;
            textShape.PaddingLeft = req.PaddingLeft;
            textShape.LineSpacing = req.LineSpacing;
            textShape.LineCount = req.LineCount;
            textShape.TextWrapping = req.TextWrapping;
            textShape.ShadowBlur = req.ShadowBlur;
            textShape.ShadowOffsetX = req.ShadowOffsetX;
            textShape.ShadowOffsetY = req.ShadowOffsetY;
            textShape.OutlineWidth = req.OutlineWidth;
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
                req.Text,
                font,
                req.FontSize,
                req.FontAutoSize,
                textAlign,
                req.Width,
                req.Height,
                req.PaddingTop,
                req.PaddingRight,
                req.PaddingBottom,
                req.PaddingLeft,
                req.LineSpacing,
                req.LineCount,
                req.TextWrapping,
                req.ShadowBlur,
                req.ShadowOffsetX,
                req.ShadowOffsetY,
                req.OutlineWidth,
                resolvedShadowColor,
                resolvedOutlineColor,
                resolvedTextColor));

            collectedComponents.Add(textShape);
            return this;
        }
    }
}
