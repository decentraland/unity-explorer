using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Character.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using UnityEngine;

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

        public MCPSceneCreationSystem(World world, World globalWorld, Entity globalPlayerEntity, IECSToCRDTWriter ecsToCRDTWriter, IComponentPool<SDKTransform> sdkTransformPool,
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
    }
}
