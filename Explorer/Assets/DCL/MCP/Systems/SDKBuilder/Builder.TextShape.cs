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

                    Begin(world, position, scale, rotation, req.ParentId)
                       .AddTextShape(world, pool, req)
                        ;

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

        public MCPSceneEntitiesBuilder AddTextShape(World world,
            IComponentPool<PBTextShape> pool,
            MCPCreateTextShapeRequest req)
        {
            PBTextShape textShape = pool.Get();
            world.Add(entity, textShape);
            textShape.Text = req.Text;
            textShape.FontSize = req.FontSize;
            textShape.IsDirty = true;

            ecsToCRDTWriter.PutMessage<PBTextShape, (string Text, float FontSize)>
            (static (pbText, data) =>
            {
                pbText.Text = data.Text;
                pbText.FontSize = data.FontSize;
            }, currentCRDTEntity, (req.Text, req.FontSize));

            // collectedComponents.Add(textShape);
            return this;
        }
    }
}
