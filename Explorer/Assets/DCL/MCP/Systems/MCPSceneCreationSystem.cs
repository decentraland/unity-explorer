using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.PoolsProviders;
using DCL.Character.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using SceneRuntime.Apis.Modules.EngineApi;
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
        private readonly IComponentPool<PBMeshRenderer> meshRendererPool;

        private bool hasJumped;

        private readonly MCPSceneEntitiesBuilder builder;

        public MCPSceneCreationSystem(World world, World globalWorld, Arch.Core.Entity globalPlayerEntity, IECSToCRDTWriter ecsToCRDTWriter, IComponentPool<SDKTransform> sdkTransformPool,
            IComponentPool<PBTextShape> textShapePool, IComponentPool<PBMeshRenderer> meshRendererPool) : base(world)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            this.textShapePool = textShapePool;
            this.meshRendererPool = meshRendererPool;

            builder = new MCPSceneEntitiesBuilder(ecsToCRDTWriter, sdkTransformPool);
            builder.ClearReservedEntities();
        }

        protected override void Update(float t)
        {
            // JumpDebug();

            // Обрабатываем запросы MCP на создание TextShape через билдер
            builder.ProcessTextShapeRequests(World, textShapePool);
            builder.ProcessMeshRendererRequests(World, meshRendererPool);
        }

        private void JumpDebug()
        {
            if (!globalWorld.Has<JumpInputComponent>(globalPlayerEntity))
                return;

            ref JumpInputComponent jumpInput = ref globalWorld.Get<JumpInputComponent>(globalPlayerEntity);

            if (jumpInput.IsPressed && !hasJumped)
            {
                hasJumped = true;
                ReportHub.Log(ReportCategory.DEBUG, "[MCP] Player jumped");

                // TODO (Cursor): GetCrdtState
                if (EngineApiLocator.TryGet(sceneInfo, out IEngineApi engineApi))
                {
                    PoolableByteArray data = engineApi.CrdtGetState();

                    try
                    {
                        string json = SceneStateJsonExporter.ExportStateToJson(data);
                        ReportHub.Log(ReportCategory.DEBUG, json);
                    }
                    finally { data.Dispose(); }
                }
                else { ReportHub.LogWarning(ReportCategory.DEBUG, "[MCP] EngineApi not available for current scene; cannot get CRDT state"); }

                // ReportHub.Log(ReportCategory.DEBUG, "[MCP] Player jumped");
                // builder.Begin(new Vector3(8, 4, 8), new Vector3(1, 1, 1))
                //        .AddTextShape(textShapePool, new MCPSceneEntitiesBuilder.MCPCreateTextShapeRequest { Text = "TEST FROM ECS", FontSize = 5 })
                //        .Build(World);
            }
        }
    }
}
