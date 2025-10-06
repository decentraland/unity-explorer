using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Character.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using Decentraland.Common;
using ECS.Abstract;
using ECS.Groups;
using Font = DCL.ECSComponents.Font;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace DCL.MCP.Systems
{
    /// <summary>
    ///     Тестовая система для проверки создания entity из C# и отправки в JS сцену.
    ///     При прыжке игрока создаёт новую entity с PBSkyboxTime в текущей сцене.
    ///     Работает в Scene World, проверяет прыжок через Global World.
    /// </summary>
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.DEBUG)]
    public partial class MCPSceneCreationSystem : BaseUnityLoopSystem
    {
        private readonly World globalWorld;
        private readonly Arch.Core.Entity globalPlayerEntity;
        private readonly IComponentPool<PBTextShape> textShapePool;

        private bool hasJumped;

        private readonly MCPSceneEntitiesBuilder builder;

        public MCPSceneCreationSystem(World world, World globalWorld, Arch.Core.Entity globalPlayerEntity, IECSToCRDTWriter ecsToCRDTWriter, IComponentPool<SDKTransform> sdkTransformPool,
            IComponentPool<PBTextShape> textShapePool) : base(world)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            this.textShapePool = textShapePool;

            builder = new MCPSceneEntitiesBuilder(ecsToCRDTWriter, sdkTransformPool);
            builder.ClearReservedEntities();
        }

        protected override void Update(float t)
        {
            JumpDebug();

            // Обрабатываем запросы MCP на создание TextShape
            while (MCPRequestsQueue.TryDequeueTextShape(out MCPCreateTextShapeRequest req))
                try { ProcessTextShapeRequest(req); }
                catch (System.Exception e) { ReportHub.LogError(ReportCategory.DEBUG, $"[MCP TextShape] Failed to process request {req.RequestId}: {e.Message}"); }
        }

        private void JumpDebug()
        {
            if (!globalWorld.Has<JumpInputComponent>(globalPlayerEntity))
                return;

            ref JumpInputComponent jumpInput = ref globalWorld.Get<JumpInputComponent>(globalPlayerEntity);

            if (jumpInput.IsPressed && !hasJumped)
            {
                hasJumped = true;
                ReportHub.Log(ReportCategory.DEBUG, "[TestJumpEntityCreation] Player jumped");

                builder.Begin(new Vector3(8, 4, 8), new Vector3(1, 1, 1))
                       .AddTextShape(textShapePool, "Created from C#! Test Entity", 5)
                       .Build(World);
            }
        }

        private void ProcessTextShapeRequest(in MCPCreateTextShapeRequest req)
        {
            var position = new Vector3(req.X, req.Y, req.Z);
            var scale = new Vector3(req.SX, req.SY, req.SZ);
            var rotation = Quaternion.Euler(req.Pitch, req.Yaw, req.Roll);

            // Map string enums into SDK enums
            Font font = EnumTryParse(req.Font, Font.FSansSerif);
            TextAlignMode textAlign = EnumTryParse(req.TextAlign, TextAlignMode.TamTopLeft);

            Color3? shadow = req.ShadowColor.HasValue ? new Color3 { R = req.ShadowColor.Value.r, G = req.ShadowColor.Value.g, B = req.ShadowColor.Value.b } : null;
            Color3? outline = req.OutlineColor.HasValue ? new Color3 { R = req.OutlineColor.Value.r, G = req.OutlineColor.Value.g, B = req.OutlineColor.Value.b } : null;
            Color4? color = req.TextColor.HasValue ? new Color4 { R = req.TextColor.Value.r, G = req.TextColor.Value.g, B = req.TextColor.Value.b, A = req.TextColor.Value.a } : null;

            MCPSceneEntitiesBuilder builderLocal = builder;

            builderLocal.Begin(position, scale, rotation, req.ParentId)
                        .AddTextShape(
                             textShapePool,
                             req.Text,
                             req.FontSize,
                             font,
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
                             shadow,
                             outline,
                             color)
                        .Build(World);

            ReportHub.Log(ReportCategory.DEBUG, $"[MCP TextShape] Created TextShape from request {req.RequestId}");
        }

        private static TEnum EnumTryParse<TEnum>(string value, TEnum fallback) where TEnum: struct
        {
            if (!string.IsNullOrEmpty(value) && System.Enum.TryParse<TEnum>(value, out TEnum parsed))
                return parsed;

            return fallback;
        }
    }
}
