using System.IO;
using UnityEngine;

namespace CRDT.CRDTTests.Protocol
{
    public class CRDTTestsUtils
    {
        public static string[] GetTestFilesPath() =>
            Directory.GetFiles($"{Application.dataPath}/../TestResources/CRDT/", "*.test");

        public static ParsedCRDTTestFile ParseTestFile(string filePath)
        {
            var parsedFile = new ParsedCRDTTestFile
            {
                fileName = filePath,
            };

            string testSpecName = null;
            var nextLineIsState = false;
            var lineNumber = 0;

            foreach (string line in File.ReadLines(filePath))
            {
                lineNumber++;

                if (line == "#")
                    continue;

                if (line.StartsWith("#")) { testSpecName ??= line; }

                if (line == "# Final CRDT State")
                {
                    nextLineIsState = true;
                    continue;
                }

                if (!(line.StartsWith("{") && line.EndsWith("}")))
                    continue;

                if (nextLineIsState)
                {
                    parsedFile.fileInstructions.Add(new ParsedCRDTTestFile.TestFileInstruction
                    {
                        fileName = filePath,
                        instructionType = ParsedCRDTTestFile.InstructionType.FINAL_STATE,
                        instructionValue = line,
                        lineNumber = lineNumber,
                        testSpect = testSpecName,
                    });

                    testSpecName = null;
                    nextLineIsState = false;
                }
                else
                {
                    parsedFile.fileInstructions.Add(new ParsedCRDTTestFile.TestFileInstruction
                    {
                        fileName = filePath,
                        instructionType = ParsedCRDTTestFile.InstructionType.MESSAGE,
                        instructionValue = line,
                        lineNumber = lineNumber,
                        testSpect = testSpecName,
                    });
                }
            }

            return parsedFile;
        }
    }
}
