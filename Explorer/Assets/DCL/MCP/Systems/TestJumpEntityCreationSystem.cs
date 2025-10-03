using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Character.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;

namespace DCL.MCP.Systems
{
    public struct TestSceneCRDTEntity
    {
        public readonly CRDTEntity CRDTEntity;
        public bool IsDirty;

        public TestSceneCRDTEntity(CRDTEntity crdtEntity, bool isDirty)
        {
            CRDTEntity = crdtEntity;
            IsDirty = isDirty;
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
        private const int TEST_ENTITIES_FROM = 1000; // Начало диапазона для тестовых entity
        private const int TEST_ENTITIES_TO = 1100; // Конец диапазона (100 тестовых entity)

        private readonly World globalWorld;
        private readonly Entity globalPlayerEntity;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly bool[] reservedEntities = new bool[TEST_ENTITIES_TO - TEST_ENTITIES_FROM];
        private int currentReservedEntitiesCount;
        private bool hasJumped;

        public TestJumpEntityCreationSystem(World world, World globalWorld, Entity globalPlayerEntity, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            ClearReservedEntities();
        }

        protected override void Update(float t)
        {
            // Проверяем есть ли у игрока JumpInputComponent в Global World
            if (!globalWorld.Has<JumpInputComponent>(globalPlayerEntity))
                return;

            ref JumpInputComponent jumpInput = ref globalWorld.Get<JumpInputComponent>(globalPlayerEntity);

            // Проверяем нажата ли кнопка прыжка (аналогично PlayerMovementNetSendSystem)
            if (jumpInput.IsPressed && !hasJumped)
            {
                hasJumped = true;
                OnPlayerJumped();
            }

            // Сбрасываем флаг когда кнопка отпущена
            if (!jumpInput.IsPressed) { hasJumped = false; }

            // Обрабатываем создание/обновление компонентов для зарезервированных entity
            CreateTestEntityComponentsQuery(World);
        }

        private void OnPlayerJumped()
        {
            ReportHub.Log(ReportCategory.DEBUG, "[TestJumpEntityCreation] Player jumped! Creating test entity in scene...");

            // Резервируем ID для новой entity
            int crdtEntityId = ReserveNextFreeEntity();

            if (crdtEntityId == -1)
            {
                ReportHub.LogWarning(ReportCategory.DEBUG, "[TestJumpEntityCreation] All test entity slots are taken!");
                return;
            }

            var testCRDTEntity = new CRDTEntity(crdtEntityId);

            ReportHub.Log(ReportCategory.DEBUG, $"[TestJumpEntityCreation] Creating entity with ID: {testCRDTEntity.Id}");

            // Создаём entity в Scene World с маркер-компонентом
            Entity sceneWorldEntity = this.World.Create();
            World.Add(sceneWorldEntity, new TestSceneCRDTEntity(testCRDTEntity, true));
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CreateTestEntityComponents(TestSceneCRDTEntity testEntity)
        {
            if (!testEntity.IsDirty) return;

            // Создаём PBSkyboxTime компонент с FixedTime = 0 и отправляем в JS сцену через CRDT
            ecsToCRDTWriter.PutMessage<PBSkyboxTime, uint>
            (
                static (pbSkybox, fixedTime) => { pbSkybox.FixedTime = fixedTime; },
                testEntity.CRDTEntity, 0
            );

            ReportHub.Log(ReportCategory.DEBUG,
                $"[TestJumpEntityCreation] Successfully sent PBSkyboxTime (FixedTime=0) for entity {testEntity.CRDTEntity.Id}");

            // Сбрасываем флаг dirty после отправки
            testEntity.IsDirty = false;
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
