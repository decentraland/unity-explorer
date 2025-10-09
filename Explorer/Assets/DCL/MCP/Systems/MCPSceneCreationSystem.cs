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
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly Dictionary<CRDTEntity, Entity> entitiesMap;

        private bool hasJumped;

        private readonly MCPSceneEntitiesBuilder builder;

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
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            entitiesMap = EntitiesMap;

            builder = new MCPSceneEntitiesBuilder(ecsToCRDTWriter, sdkTransformPool, EntitiesMap);
            builder.ClearReservedEntities();
        }

        protected override void Update(float t)
        {
            JumpDebug();

            // builder.ProcessMeshRendererRequests(World, meshRendererPool);
            // builder.ProcessTextShapeRequests(World, textShapePool);
            // builder.ProcessMeshColliderRequests(World, colliderPool);
            // builder.ProcessSetTransformRequests(World);
        }

        private void JumpDebug()
        {
            if (!globalWorld.Has<JumpInputComponent>(globalPlayerEntity))
                return;

            ref JumpInputComponent jumpInput = ref globalWorld.Get<JumpInputComponent>(globalPlayerEntity);

            if (jumpInput.IsPressed && hasJumped && !hasJumpedTwice)
            {
                hasJumpedTwice = true;
                ReportHub.Log(ReportCategory.DEBUG, "[MCP] Player jumped twice");

                // Тест: сдвигаем все entity на 1м вперёд (по Z) через PutMessage без создания новых CRDT
                DebugNudgeAllEntitiesForward();

                // TryInjectLocalJsOnUpdate("try { console.log('[MCP-LOCAL] onUpdate dt=', dt); } catch (_) {}");
            }
        }

        private bool hasJumpedTwice;

        private void DebugNudgeAllEntitiesForward()
        {
            foreach (KeyValuePair<CRDTEntity, Entity> kvp in entitiesMap)
            {
                CRDTEntity crdtEntity = kvp.Key;
                Entity e = kvp.Value;

                if (!World.Has<SDKTransform>(e))
                    continue;

                ref SDKTransform sdkTransform = ref World.Get<SDKTransform>(e);

                Vector3 newPos = sdkTransform.Position.Value + new Vector3(1, 1, 1);
                var newScale = new Vector3(5, 5, 5);
                Quaternion newRot = sdkTransform.Rotation.Value;

                // 1) Обновляем мир (сразу визуально)
                sdkTransform.Position.Value = newPos;
                sdkTransform.Scale = newScale;
                sdkTransform.Rotation.Value = newRot;

                // 2) Шлём CRDT с теми же значениями (pos + scale + rot)
                ecsToCRDTWriter.PutMessage<SDKTransform, (Vector3 Pos, Vector3 Scale, Quaternion Rot)>(static (sdk, data) =>
                {
                    sdk.Position.Value = data.Pos;
                    sdk.Scale = data.Scale;
                    sdk.Rotation.Value = data.Rot;
                }, crdtEntity, (newPos, newScale, newRot));

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Debug] moved {e.Id} crdt {crdtEntity.Id}");
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
