using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.PoolsProviders;
using DCL.Character.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using System.Collections.Concurrent;
using ECS.Abstract;
using ECS.Groups;
using ECS.SceneLifeCycle;
using SceneRunner;
using SceneRunner.Scene;
using SceneRuntime.Apis.Modules.EngineApi;
using System.Collections.Generic;
using Font = DCL.ECSComponents.Font;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace DCL.MCP.Systems
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.DEBUG)]
    public partial class MCPSceneCreationSystem : BaseUnityLoopSystem
    {
        private readonly World globalWorld;
        private readonly Arch.Core.Entity globalPlayerEntity;
        private readonly IScenesCache scenesCache;
        private readonly IComponentPool<PBTextShape> textShapePool;
        private readonly IComponentPool<PBMeshRenderer> meshRendererPool;
        private readonly IComponentPool<PBMeshCollider> colliderPool;

        private bool hasJumped;

        private readonly MCPSceneEntitiesBuilder builder;

        private static readonly ConcurrentQueue<SpawnTextRequest> pendingTextRequests = new ();

        public readonly struct SpawnTextRequest
        {
            public readonly string Text;
            public readonly Vector3 Position;
            public readonly float FontSize;

            public SpawnTextRequest(string text, Vector3 position, float fontSize)
            {
                Text = text;
                Position = position;
                FontSize = fontSize;
            }
        }

        public MCPSceneCreationSystem(World world, World globalWorld, Entity globalPlayerEntity, IScenesCache scenesCache,
            IECSToCRDTWriter ecsToCRDTWriter,
            Dictionary<CRDTEntity, Entity> EntitiesMap,
            IComponentPool<SDKTransform> sdkTransformPool,
            IComponentPool<PBTextShape> textShapePool
          , IComponentPool<PBMeshRenderer> meshRendererPool
          , IComponentPool<PBMeshCollider> colliderPool) : base(world)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            this.scenesCache = scenesCache;
            this.textShapePool = textShapePool;
            this.meshRendererPool = meshRendererPool;
            this.colliderPool = colliderPool;

            builder = new MCPSceneEntitiesBuilder(ecsToCRDTWriter, sdkTransformPool, EntitiesMap);
            builder.ClearReservedEntities();
        }

        protected override void Update(float t)
        {
            JumpDebug();

            // Обрабатываем запросы MCP на создание TextShape через билдер
            ProcessPendingTextRequests();
            // builder.ProcessMeshRendererRequests(World, meshRendererPool);
            // Пример: builder.ProcessMeshColliderRequests(World, colliderPool);
        }

        private void ProcessPendingTextRequests()
        {
            while (pendingTextRequests.TryDequeue(out SpawnTextRequest req))
            {
                builder.Begin(World, req.Position, new Vector3(1, 1, 1))
                       .AddTextShape(World, textShapePool, new MCPSceneEntitiesBuilder.MCPCreateTextShapeRequest
                        {
                            Text = req.Text,
                            FontSize = (int)req.FontSize,
                        });
            }
        }

        public static void EnqueueSpawnText(string text, Vector3 position, float fontSize = 5f)
        {
            pendingTextRequests.Enqueue(new SpawnTextRequest(text, position, fontSize));
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

                // 1) Визуальная метка
                builder.Begin(World, new Vector3(8, 4, 8), new Vector3(1, 1, 1))
                       .AddTextShape(World, textShapePool, new MCPSceneEntitiesBuilder.MCPCreateTextShapeRequest { Text = "JUMP", FontSize = 5 });

                // 2) Прямая инъекция в JS onUpdate (локальный тест без MCP): добавить лог на каждый тик
                TryInjectLocalJsOnUpdate("try { console.log('[MCP-LOCAL] onUpdate dt=', dt); } catch (_) {}");
            }
        }

        private void TryInjectLocalJsOnUpdate(string jsSnippet)
        {
            // Прямой вызов API рантайма: инжект кода в onUpdate (до оригинального апдейта)
            ISceneFacade? scene = scenesCache.CurrentScene;
            scene?.InjectOnUpdate(jsSnippet);
        }
    }
}
