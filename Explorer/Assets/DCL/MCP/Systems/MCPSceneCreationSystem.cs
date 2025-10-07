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
        private readonly IComponentPool<PBMeshCollider> colliderPool;

        private bool hasJumped;

        private readonly MCPSceneEntitiesBuilder builder;

        public MCPSceneCreationSystem(World world, World globalWorld, Arch.Core.Entity globalPlayerEntity, IECSToCRDTWriter ecsToCRDTWriter, IComponentPool<SDKTransform> sdkTransformPool,
            IComponentPool<PBTextShape> textShapePool
          , IComponentPool<PBMeshRenderer> meshRendererPool
          , IComponentPool<PBMeshCollider> colliderPool) : base(world)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            this.textShapePool = textShapePool;
            this.meshRendererPool = meshRendererPool;
            this.colliderPool = colliderPool;

            builder = new MCPSceneEntitiesBuilder(ecsToCRDTWriter, sdkTransformPool);
            builder.ClearReservedEntities();
        }

        protected override void Update(float t)
        {
            JumpDebug();

            // Обрабатываем запросы MCP на создание TextShape через билдер
            // builder.ProcessTextShapeRequests(World, textShapePool);
            // builder.ProcessMeshRendererRequests(World, meshRendererPool);
            // Пример: builder.ProcessMeshColliderRequests(World, colliderPool);
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

                // Создаём MeshRenderer + MeshCollider (Box) для проверки
                var meshReq = new MCPSceneEntitiesBuilder.MCPCreateMeshRendererRequest
                {
                    RequestId = System.Guid.NewGuid().ToString("N"),
                    X = 8, Y = 1, Z = 8,
                    SX = 1, SY = 1, SZ = 1,
                    Yaw = 0, Pitch = 0, Roll = 0,
                    ParentId = 0,
                    MeshType = "Box",
                };

                var colReq = new MCPSceneEntitiesBuilder.MCPCreateMeshColliderRequest
                {
                    RequestId = System.Guid.NewGuid().ToString("N"),
                    ColliderType = "Box",
                    CollisionMask = 1u | 2u, // CL_POINTER | CL_PHYSICS
                };

                builder.Begin(new Vector3(meshReq.X, meshReq.Y, meshReq.Z), new Vector3(meshReq.SX, meshReq.SY, meshReq.SZ),
                            Quaternion.Euler(meshReq.Pitch, meshReq.Yaw, meshReq.Roll), meshReq.ParentId)
                       .AddMeshRenderer(meshRendererPool, meshReq)
                       .AddMeshCollider(colliderPool, colReq)
                       .Build(World);
            }
        }
    }
}
