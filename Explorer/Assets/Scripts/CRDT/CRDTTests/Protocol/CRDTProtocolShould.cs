using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CrdtEcsBridge.PoolsProviders;
using Instrumentation;
using NUnit.Framework;
using System;
using System.Linq;

namespace CRDT.CRDTTests.Protocol
{
    [TestFixture]
    public class CRDTProtocolShould
    {
        private ICRDTMemoryAllocator memoryAllocator;

        [SetUp]
        public void SetUp()
        {
            memoryAllocator = CRDTOriginalMemorySlicer.Create();
        }

        [Test]
        [TestCaseSource(typeof(CRDTTestsUtils), nameof(CRDTTestsUtils.GetTestFilesPath))]
        public void ProcessMessagesCorrectly(string testPath)
        {
            ParsedCRDTTestFile parsedFile = CRDTTestsUtils.ParseTestFile(testPath);
            AssertTestFile(parsedFile);
        }

        [Test]
        [TestCaseSource(typeof(CRDTTestsUtils), nameof(CRDTTestsUtils.GetTestFilesPath))]
        public void ProvideMessagesInAccordanceWithStateCorrectly(string testPath)
        {
            ParsedCRDTTestFile parsedFile = CRDTTestsUtils.ParseTestFile(testPath);
            AssertGetCurrentState(parsedFile);
        }

        [Test]
        public void NotAllocateIfPutMessageIsDropped()
        {
            var bytes = new byte[30];
            var r = new Random();
            r.NextBytes(bytes);

            var m1 = new CRDTMessage(CRDTMessageType.PUT_COMPONENT, 100, 20, 10, memoryAllocator.GetMemoryBuffer(bytes.AsMemory()));

            var crdt = new CRDTProtocol(InstancePoolsProvider.Create());

            // this message will allocate an internal storage
            MemoryStat.Debug.GC_ALLOCATED_IN_FRAME.Check(() => crdt.ProcessMessage(m1), value => Assert.GreaterOrEqual(value, 0));

            // This message is with older timestamp so no memory internally must be allocated
            var m2 = new CRDTMessage(CRDTMessageType.PUT_COMPONENT, 100, 20, 0, memoryAllocator.GetMemoryBuffer(bytes.AsMemory()));

            MemoryStat.Debug.GC_ALLOCATED_IN_FRAME.Check(() => crdt.ProcessMessage(m2), value => Assert.AreEqual(0, value));
        }

        [Test]
        public void AmortizeAllocationsForAppendMessage()
        {
            var bytes = new byte[45];
            var r = new Random();
            r.NextBytes(bytes);

            var m1 = new CRDTMessage(CRDTMessageType.APPEND_COMPONENT, 100, 20, 10, memoryAllocator.GetMemoryBuffer(bytes.AsMemory()));
            var m2 = new CRDTMessage(CRDTMessageType.APPEND_COMPONENT, 100, 20, 30, memoryAllocator.GetMemoryBuffer(bytes.AsMemory()));

            var crdt = new CRDTProtocol(InstancePoolsProvider.Create());
            MemoryStat.Debug.GC_ALLOCATED_IN_FRAME.Check(() => crdt.ProcessMessage(m1), value => Assert.GreaterOrEqual(value, 0));

            for (var i = 0; i < 50; i++)
            {
                int index = i;

                MemoryStat.Debug.GC_ALLOCATED_IN_FRAME.Check(() => crdt.ProcessMessage(m2), value =>
                {
                    if (index > 1) Assert.AreEqual(0, value);
                });
            }
        }

        [Test]
        public void ReuseBuffers([Values(4, 8, 16, 64, 256, 1024, 4096, 8192)] int componentsCount, [Range(0f, 1f, 0.1f)] float entitiesVariety)
        {
            int uniqueComponents = InstancePoolsProvider.CRDT_LWW_COMPONENTS_OUTER_CAPACITY;

            var messages = Enumerable.Range(0, componentsCount)
                                     .Select(i => new CRDTMessage(CRDTMessageType.PUT_COMPONENT, (int)((i + 1) * entitiesVariety), i % uniqueComponents, 10, EmptyMemoryOwner<byte>.EMPTY))
                                     .ToList();

            var poolsProvider = InstancePoolsProvider.Create();

            var crdt = new CRDTProtocol(poolsProvider);
            var firstPass = 0L;

            MemoryStat.Debug.GC_ALLOCATED_IN_FRAME.Check(() =>
            {
                foreach (CRDTMessage message in messages)
                    crdt.ProcessMessage(message);
            }, value => firstPass = value);

            crdt.Dispose();

            const int TOLERANCE = 512;

            crdt = new CRDTProtocol(poolsProvider);

            MemoryStat.Debug.GC_ALLOCATED_IN_FRAME.Check(() =>
            {
                foreach (CRDTMessage message in messages)
                    crdt.ProcessMessage(message);
            }, value => Assert.That(value, Is.LessThan(firstPass / 20f) & Is.LessThan(TOLERANCE)));
        }

        private void AssertTestFile(ParsedCRDTTestFile parsedFile)
        {
            var crdt = new CRDTProtocol(InstancePoolsProvider.Create());

            for (int i = 0; i < parsedFile.fileInstructions.Count; i++)
            {
                ParsedCRDTTestFile.TestFileInstruction instruction = parsedFile.fileInstructions[i];

                if (instruction.instructionType == ParsedCRDTTestFile.InstructionType.MESSAGE)
                {
                    (CRDTMessage msg, CRDTReconciliationResult? expectedResult) = ParsedCRDTTestFile.InstructionToMessage(instruction, memoryAllocator);
                    var result = crdt.ProcessMessage(msg);

                    if (expectedResult != null)
                    {
                        Assert.AreEqual(expectedResult.Value, result, $"Message reconciliation result mismatch {instruction.instructionValue} "
                                                                      + $"in line:{instruction.lineNumber} for file {instruction.fileName}. Expected: {expectedResult}, actual: {result}");
                    }
                }
                else if (instruction.instructionType == ParsedCRDTTestFile.InstructionType.FINAL_STATE)
                {
                    var finalState = ParsedCRDTTestFile.InstructionToFinalState(instruction);
                    bool sameState = AreStatesEqual(crdt.CRDTState, finalState, out string reason);

                    Assert.IsTrue(sameState, $"Final state mismatch {instruction.testSpect} " +
                                             $"in line:{instruction.lineNumber} for file {instruction.fileName}. Reason: {reason}");

                    crdt = new CRDTProtocol(InstancePoolsProvider.Create());
                }
            }
        }

        private void AssertGetCurrentState(ParsedCRDTTestFile parsedFile)
        {
            var crdt = new CRDTProtocol(InstancePoolsProvider.Create());

            for (int i = 0; i < parsedFile.fileInstructions.Count; i++)
            {
                ParsedCRDTTestFile.TestFileInstruction instruction = parsedFile.fileInstructions[i];

                if (instruction.instructionType == ParsedCRDTTestFile.InstructionType.MESSAGE)
                {
                    (CRDTMessage msg, _) = ParsedCRDTTestFile.InstructionToMessage(instruction, memoryAllocator);
                    crdt.ProcessMessage(msg);
                }
                else if (instruction.instructionType == ParsedCRDTTestFile.InstructionType.FINAL_STATE)
                {
                    // The order of messages is not important

                    var preallocatedArray = new ProcessedCRDTMessage[crdt.GetMessagesCount()];
                    crdt.CreateMessagesFromTheCurrentState(preallocatedArray);

                    CRDTMessage[] finalStateMessages = ParsedCRDTTestFile.InstructionToFinalStateMessages(instruction, memoryAllocator).ToArray();

                    CollectionAssert.AreEqual(finalStateMessages, preallocatedArray.Select(x => x.message).ToArray());

                    crdt = new CRDTProtocol(InstancePoolsProvider.Create());
                }
            }
        }

        private static bool AreStatesEqual(in CRDTProtocol.State stateA, in CRDTProtocol.State stateB, out string reason)
        {
            // different amount
            int componentDataAmountA = stateA.lwwComponents.Aggregate(0, (accum, current) => accum + current.Value.Count);
            int componentDataAmountB = stateB.lwwComponents.Aggregate(0, (accum, current) => accum + current.Value.Count);

            if (componentDataAmountA != componentDataAmountB)
            {
                reason = "There is a different amount of entity-component data";
                return false;
            }

            foreach (var componentA in stateA.lwwComponents)
            {
                // The component A is not in the state B
                if (!stateB.lwwComponents.TryGetValue(componentA.Key, out var componentB))
                {
                    if (stateB.lwwComponents.Count == 0)
                        continue;

                    reason = $"The component {componentA.Key} from stateA is not in stateB";
                    return false;
                }

                foreach (var entityComponent in componentA.Value)
                {
                    var entityId = entityComponent.Key;
                    var entityComponentDataA = entityComponent.Value;

                    // The entity is in the stateA, but not in stateB
                    if (!componentB.TryGetValue(entityId, out var entityComponentDataB))
                    {
                        reason = $"The entity {entityId} in the component {componentA.Key} from stateA is not in stateB.";
                        return false;
                    }

                    // All good! We know check the data and timestamp.
                    if (entityComponentDataA.Timestamp != entityComponentDataB.Timestamp)
                    {
                        reason = $"The entity {entityId} in the component {componentA.Key} from stateA has a different TIMESTAMP in the stateB.";
                        return false;
                    }

                    int diff = CRDTMessageComparer.CompareData(in entityComponentDataA.Data, in entityComponentDataB.Data);

                    if (diff != 0)
                    {
                        reason = $"The entity {entityId} in the component {componentA.Key} from stateA has a different DATA in the stateB (cmp(a,b) = {diff}).";
                        return false;
                    }
                }
            }

            if (stateA.deletedEntities.Count != stateB.deletedEntities.Count)
            {
                reason = "There is a different amount of deleted entities.";
                return false;
            }

            foreach (var entity in stateA.deletedEntities)
            {
                if (!stateB.deletedEntities.TryGetValue(entity.Key, out int entityVersion))
                {
                    reason = $"The entity-number {entity.Key} is deleted in stateA but not in stateB.";
                    return false;
                }

                if (entityVersion != entity.Value)
                {
                    reason = $"The entity-number {entity.Key} has a different deleted version in stateA({entity.Value}) but not in stateB({entityVersion}).";
                    return false;
                }
            }

            reason = "";
            return true;
        }
    }
}
