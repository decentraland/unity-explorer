using CRDT.Protocol;
using CRDT.Protocol.Factory;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace CRDT.CRDTTests.Protocol
{
    [TestFixture]
    public class CRDTProtocolShould
    {
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

        private void AssertTestFile(ParsedCRDTTestFile parsedFile)
        {
            CRDTProtocol crdt = new CRDTProtocol();

            for (int i = 0; i < parsedFile.fileInstructions.Count; i++)
            {
                ParsedCRDTTestFile.TestFileInstruction instruction = parsedFile.fileInstructions[i];

                if (instruction.instructionType == ParsedCRDTTestFile.InstructionType.MESSAGE)
                {
                    var (msg, expectedResult) = ParsedCRDTTestFile.InstructionToMessage(instruction);
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

                    crdt = new CRDTProtocol();
                }
            }
        }

        private void AssertGetCurrentState(ParsedCRDTTestFile parsedFile)
        {
            CRDTProtocol crdt = new CRDTProtocol();

            for (int i = 0; i < parsedFile.fileInstructions.Count; i++)
            {
                ParsedCRDTTestFile.TestFileInstruction instruction = parsedFile.fileInstructions[i];

                if (instruction.instructionType == ParsedCRDTTestFile.InstructionType.MESSAGE)
                {
                    var (msg, _) = ParsedCRDTTestFile.InstructionToMessage(instruction);
                    crdt.ProcessMessage(msg);
                }
                else if (instruction.instructionType == ParsedCRDTTestFile.InstructionType.FINAL_STATE)
                {
                    // The order of messages is not important

                    var preallocatedArray = new ProcessedCRDTMessage[crdt.GetMessagesCount()];
                    crdt.CreateMessagesFromTheCurrentState(preallocatedArray);

                    var finalStateMessages = ParsedCRDTTestFile.InstructionToFinalStateMessages(instruction).ToArray();

                    CollectionAssert.AreEqual(finalStateMessages, preallocatedArray.Select(x => x.message).ToArray());

                    crdt = new CRDTProtocol();
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
