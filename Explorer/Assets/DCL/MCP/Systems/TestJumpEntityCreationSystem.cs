using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Character.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using Decentraland.Common;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace DCL.MCP.Systems
{
    public struct TestSceneCRDTEntity : IDirtyMarker
    {
        public readonly CRDTEntity CRDTEntity { get; }
        public bool IsDirty { get; set; }

        public TestSceneCRDTEntity(CRDTEntity crdtEntity)
        {
            CRDTEntity = crdtEntity;
            IsDirty = true;
        }
    }

    /// <summary>
    ///     Тестовая система для проверки создания entity из C# и отправки в JS сцену.
    ///     При прыжке игрока создаёт новую entity с PBSkyboxTime в текущей сцене.
    ///     Работает в Scene World, проверяет прыжок через Global World.
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.DEBUG)]
    public partial class TestJumpEntityCreationSystem : BaseUnityLoopSystem
    {
        private const int TEST_ENTITIES_FROM = 2000; // Начало диапазона для тестовых entity
        private const int TEST_ENTITIES_TO = 3100; // Конец диапазона (100 тестовых entity)

        private readonly World globalWorld;
        private readonly Arch.Core.Entity globalPlayerEntity;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IComponentPool<SDKTransform> sdkTransformPool;
        private readonly IComponentPool<PBTextShape> textShapePool;
        private readonly bool[] reservedEntities = new bool[TEST_ENTITIES_TO - TEST_ENTITIES_FROM];
        private int currentReservedEntitiesCount;
        private bool hasJumped;

        public TestJumpEntityCreationSystem(World world, World globalWorld, Arch.Core.Entity globalPlayerEntity, IECSToCRDTWriter ecsToCRDTWriter, IComponentPool<SDKTransform> sdkTransformPool,
            IComponentPool<PBTextShape> textShapePool) : base(world)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sdkTransformPool = sdkTransformPool;
            this.textShapePool = textShapePool;
            ClearReservedEntities();
        }

        protected override void Update(float t)
        {
            if (!globalWorld.Has<JumpInputComponent>(globalPlayerEntity))
                return;

            ref JumpInputComponent jumpInput = ref globalWorld.Get<JumpInputComponent>(globalPlayerEntity);

            if (jumpInput.IsPressed && !hasJumped)
            {
                hasJumped = true;

                OnPlayerJumped();
                CreateTestEntityComponentsQuery(World);
            }
        }

        private void OnPlayerJumped()
        {
            ReportHub.Log(ReportCategory.DEBUG, "[TestJumpEntityCreation] Player jumped");

            var testCRDTEntity = new CRDTEntity(ReserveNextFreeEntity());

            SDKTransform? sdkTransform = sdkTransformPool.Get();
            sdkTransform!.Position.Value = new Vector3(8, 4, 8);
            sdkTransform.Rotation.Value = Quaternion.identity;
            sdkTransform.Scale = new Vector3(1, 1, 1);
            sdkTransform.ParentId = 0;
            sdkTransform.IsDirty = true;

            ecsToCRDTWriter.PutMessage<SDKTransform, (Vector3 Position, Vector3 Scale)>(static (sdkTransform, data) =>
            {
                sdkTransform.Position.Value = data.Position;
                sdkTransform.Scale = data.Scale;
            }, testCRDTEntity, (new Vector3(8, 4, 8), new Vector3(1, 1, 1)));

            PBTextShape? textShape = textShapePool.Get();
            textShape!.Text = "Created from C#! Test Entity";
            textShape.FontSize = 5;
            textShape.FontAutoSize = false;
            textShape.Height = 2;
            textShape.Width = 4;
            textShape.OutlineWidth = 0.1f;
            textShape.OutlineColor = new Color3 { R = 0, G = 0, B = 1 };
            textShape.TextColor = new Color4 { R = 1, G = 0, B = 0, A = 1 };
            textShape.IsDirty = true;

            ecsToCRDTWriter.PutMessage<PBTextShape, (string Text, int FontSize)>(static (pbText, data) =>
            {
                pbText.Text = data.Text;
                pbText.FontSize = data.FontSize;
            }, testCRDTEntity, ("Created from C#! Test Entity", 5));

            World.Create(testCRDTEntity, sdkTransform, textShape);

            ReportHub.Log(ReportCategory.DEBUG,
                $"[TestJumpEntityCreation] Added SDKTransform + PBTextShape to entity {testCRDTEntity.Id}");
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CreateTestEntityComponents(in Arch.Core.Entity entity, PBTextShape pbTextShape)
        {
            // Debug.Log($"MCP: entity {entity.Id} with {pbTextShape.Text}");
        }

        private int ReserveNextFreeEntity()
        {
            // Все слоты заняты
            if (currentReservedEntitiesCount == reservedEntities.Length)
                return -1;

            for (var i = 0; i < reservedEntities.Length; i++)
            {
                if (!reservedEntities[i])
                {
                    reservedEntities[i] = true;
                    currentReservedEntitiesCount++;
                    return TEST_ENTITIES_FROM + i;
                }
            }

            return -1;
        }

        private void FreeReservedEntity(int entityId)
        {
            entityId -= TEST_ENTITIES_FROM;
            if (entityId >= reservedEntities.Length || entityId < 0) return;

            reservedEntities[entityId] = false;
            currentReservedEntitiesCount--;
        }

        private void ClearReservedEntities()
        {
            for (var i = 0; i < reservedEntities.Length; i++) { reservedEntities[i] = false; }

            currentReservedEntitiesCount = 0;
        }
    }
}
