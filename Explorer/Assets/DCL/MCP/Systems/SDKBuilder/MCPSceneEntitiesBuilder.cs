using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Entity = Arch.Core.Entity;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace DCL.MCP.Systems
{
    public partial class MCPSceneEntitiesBuilder
    {
        private const int TEST_ENTITIES_FROM = 2000; // Начало диапазона для тестовых entity
        private const int TEST_ENTITIES_TO = 3100; // Конец диапазона (100 тестовых entity)

        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IComponentPool<SDKTransform> sdkTransformPool;
        private readonly Dictionary<CRDTEntity, Entity> entitiesMap;
        private readonly bool[] reservedEntities = new bool[TEST_ENTITIES_TO - TEST_ENTITIES_FROM];

        private int currentReservedEntitiesCount;

        private CRDTEntity currentCRDTEntity;
        private SDKTransform? currentSDKTransform;
        private Entity entity;

        public MCPSceneEntitiesBuilder(IECSToCRDTWriter ecsToCRDTWriter, IComponentPool<SDKTransform> sdkTransformPool, Dictionary<CRDTEntity, Entity> entitiesMap)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sdkTransformPool = sdkTransformPool;
            this.entitiesMap = entitiesMap;

            textShapeRequests.Clear();
        }

        public MCPSceneEntitiesBuilder Begin(World world, Vector3 position, Vector3 scale, Quaternion rotation = default, int parentId = 0)
        {
            currentCRDTEntity = new CRDTEntity(ReserveNextFreeEntity());
            entity = world.Create(currentCRDTEntity);
            entitiesMap[currentCRDTEntity] = entity;

            currentSDKTransform = sdkTransformPool.Get();
            world.Add(entity, currentSDKTransform);

            currentSDKTransform.Position.Value = position;
            currentSDKTransform.Rotation.Value = rotation;
            currentSDKTransform.Scale = scale;
            currentSDKTransform.ParentId = parentId;

            ecsToCRDTWriter.PutMessage<SDKTransform, (Vector3 Position, Vector3 Scale, Quaternion Rotation, int parentId)>
            (static (sdkTransform, data) =>
            {
                sdkTransform.Position.Value = data.Position;
                sdkTransform.Scale = data.Scale;
            }, currentCRDTEntity, (position, scale, rotation, parentId));

            return this;
        }

        public void Build(World world)
        {
            // Entity entity = world.Create(currentCRDTEntity);
            // foreach (object? component in collectedComponents)
            //     world.Add(entity, component);

            ReportHub.Log(ReportCategory.DEBUG,
                $"[TestJumpEntityCreation] Added SDKTransform + PBTextShape to entity {currentCRDTEntity.Id}");
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

        public void ClearReservedEntities()
        {
            for (var i = 0; i < reservedEntities.Length; i++)
                reservedEntities[i] = false;

            currentReservedEntitiesCount = 0;
        }
    }
}
