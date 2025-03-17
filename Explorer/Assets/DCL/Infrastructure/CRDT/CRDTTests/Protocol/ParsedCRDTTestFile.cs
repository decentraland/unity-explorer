using Collections.Pooled;
using CRDT.Memory;
using CRDT.Protocol;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CRDT.CRDTTests.Protocol
{
    public class ParsedCRDTTestFile
    {
        public enum InstructionType
        {
            MESSAGE = 0,
            FINAL_STATE = 1,
        }

        public string fileName;
        public List<TestFileInstruction> fileInstructions = new ();

        public static (CRDTMessage, CRDTReconciliationResult?) InstructionToMessage(TestFileInstruction instruction, ICRDTMemoryAllocator memoryAllocator)
        {
            CRDTTestMessage msg = null;
            CRDTReconciliationResult? reconciliationResult = null;

            try
            {
                string[] parts = instruction.instructionValue.Split("=>");

                if (parts.Length == 2)
                {
                    msg = JsonUtility.FromJson<CRDTTestMessage>(parts[0]);
                    reconciliationResult = JsonUtility.FromJson<CRDTTestMessageResult>(parts[1]).ToCRDTReconciliationResult();
                }
                else { msg = JsonUtility.FromJson<CRDTTestMessage>(instruction.instructionValue); }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing line for msg (ln: {instruction.lineNumber}) " +
                               $"{instruction.instructionValue} for file {instruction.fileName}, {e}");
            }

            // move from crdt message type from crdt library to crdt protocol
            var crdtLibType = (int)msg.type;

            if (crdtLibType == 1) { msg.type = CRDTMessageType.PUT_COMPONENT; }
            else if (crdtLibType == 2) { msg.type = CRDTMessageType.DELETE_ENTITY; }

            return (msg.ToCRDTMessage(memoryAllocator), reconciliationResult);
        }

        internal static IEnumerable<CRDTMessage> InstructionToFinalStateMessages(TestFileInstruction instruction, CRDTPooledMemoryAllocator crdtPooledMemoryAllocator)
        {
            CrdtJsonState finalState = null;

            try { finalState = JsonUtility.FromJson<CrdtJsonState>(instruction.instructionValue); }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing line for state (ln: {instruction.lineNumber}) " +
                               $"{instruction.instructionValue} for file {instruction.fileName}, {e}");
            }

            foreach (CrdtTestEntityComponentData entityComponentData in finalState.components)
            {
                int entityId = entityComponentData.entityId;
                int componentId = entityComponentData.componentId;

                yield return new CRDTMessage(CRDTMessageType.PUT_COMPONENT, entityId, componentId, entityComponentData.timestamp, crdtPooledMemoryAllocator.GetMemoryBuffer(entityComponentData.GetBytes()));
            }

            foreach (CrdtTestEntity entity in finalState.deletedEntities)
                yield return new CRDTMessage(CRDTMessageType.DELETE_ENTITY, CRDTEntity.Create(entity.entityNumber, entity.entityVersion), 0, 0, EmptyMemoryOwner<byte>.EMPTY);
        }

        internal static CRDTProtocol.State InstructionToFinalState(TestFileInstruction instruction)
        {
            CrdtJsonState finalState = null;

            try { finalState = JsonUtility.FromJson<CrdtJsonState>(instruction.instructionValue); }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing line for state (ln: {instruction.lineNumber}) " +
                               $"{instruction.instructionValue} for file {instruction.fileName}, {e}");
            }

            var state = new CRDTProtocol.State(
                new PooledDictionary<int, int>(),
                new PooledDictionary<int, PooledDictionary<CRDTEntity, CRDTProtocol.EntityComponentData>>(),
                new PooledDictionary<int, PooledDictionary<CRDTEntity, PooledList<CRDTProtocol.EntityComponentData>>>());

            foreach (CrdtTestEntityComponentData entityComponentData in finalState.components)
            {
                int entityId = entityComponentData.entityId;
                int componentId = entityComponentData.componentId;

                var realData = entityComponentData.ToEntityComponentData();

                if (!state.lwwComponents.ContainsKey(componentId))
                    state.lwwComponents.Add(componentId, new PooledDictionary<CRDTEntity, CRDTProtocol.EntityComponentData>());

                state.lwwComponents[componentId].Add(new CRDTEntity(entityId), realData);
            }

            foreach (CrdtTestEntity entity in finalState.deletedEntities)
                state.deletedEntities.Add(entity.entityNumber, entity.entityVersion);

            return state;
        }

        [Serializable]
        public class TestFileInstruction
        {
            public InstructionType instructionType;
            public string instructionValue;
            public int lineNumber;
            public string fileName;
            public string testSpect;
        }
    }
}
