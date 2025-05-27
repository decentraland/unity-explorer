// SPDX-FileCopyrightText: 2024 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;
using UnityEngine;

namespace KtxUnity.Tests
{

    public class KtxTestCaseAttribute : UnityEngine.TestTools.UnityTestAttribute, ITestBuilder
    {

        readonly IReadOnlyList<string> m_FileNames;

        readonly NUnitTestCaseBuilder m_Builder = new NUnitTestCaseBuilder();

        public KtxTestCaseAttribute(string[] fileNames)
        {

            m_FileNames = fileNames;
        }

        IEnumerable<TestMethod> ITestBuilder.BuildFrom(IMethodInfo method, Test suite)
        {
            List<TestMethod> results = new List<TestMethod>();
            var nameCounts = new Dictionary<string, int>();

            if (m_FileNames == null)
            {
                throw new InvalidDataException("Test file names not set");
            }

            try
            {
                foreach (var fileName in m_FileNames)
                {
                    var data = new TestCaseData(new object[] { fileName });

                    var origName = Path.GetFileName(fileName);
                    string name;
                    if (nameCounts.TryGetValue(origName, out var count))
                    {
                        name = $"{method.Name}-{origName}-{count}";
                        nameCounts[origName] = count + 1;
                    }
                    else
                    {
                        name = $"{method.Name}-{origName}";
                        nameCounts[origName] = 1;
                    }

                    data.SetName(name);
                    data.ExpectedResult = new UnityEngine.Object();
                    data.HasExpectedResult = true;

                    var test = this.m_Builder.BuildTestMethod(method, suite, data);
                    if (test.parms != null)
                        test.parms.HasExpectedResult = false;

                    test.Name = name;

                    results.Add(test);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to generate glTF testcases!");
                Debug.LogException(ex);
                throw;
            }

            Console.WriteLine("Generated {0} glTF test cases.", results.Count);
            return results;
        }
    }
}
