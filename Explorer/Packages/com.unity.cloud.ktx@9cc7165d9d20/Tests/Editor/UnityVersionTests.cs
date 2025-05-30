// SPDX-FileCopyrightText: 2024 Unity Technologies and the Draco for Unity authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KtxUnity.Editor.Tests
{
    class UnityVersionTests
    {
        [Test]
        public void ConstructorMajor()
        {
            var u = new UnityVersion("42");
            Assert.AreEqual(42, u.Major);
            Assert.AreEqual(0, u.Minor);
            Assert.AreEqual(0, u.Patch);
            Assert.AreEqual('f', u.Type);
            Assert.AreEqual(1, u.Sequence);
        }

        [Test]
        public void ConstructorMinor()
        {
            var u = new UnityVersion("2019.1");
            Assert.AreEqual(2019, u.Major);
            Assert.AreEqual(1, u.Minor);
            Assert.AreEqual(0, u.Patch);
            Assert.AreEqual('f', u.Type);
            Assert.AreEqual(1, u.Sequence);
        }

        [Test]
        public void ConstructorPatch()
        {
            var u = new UnityVersion("2020.12.9");
            Assert.AreEqual(2020, u.Major);
            Assert.AreEqual(12, u.Minor);
            Assert.AreEqual(9, u.Patch);
            Assert.AreEqual('f', u.Type);
            Assert.AreEqual(1, u.Sequence);
        }

        [Test]
        public void ConstructorType()
        {
            var u = new UnityVersion("2021.42.42a");
            Assert.AreEqual(2021, u.Major);
            Assert.AreEqual(42, u.Minor);
            Assert.AreEqual(42, u.Patch);
            Assert.AreEqual('a', u.Type);
            Assert.AreEqual(1, u.Sequence);
        }

        [Test]
        public void ConstructorFull()
        {
            var u = new UnityVersion("4.2.0f2");
            Assert.AreEqual(4, u.Major);
            Assert.AreEqual(2, u.Minor);
            Assert.AreEqual(0, u.Patch);
            Assert.AreEqual('f', u.Type);
            Assert.AreEqual(2, u.Sequence);
        }

        [Test]
        public void ConstructorFullSuffix()
        {
            // Some versions have additional suffixes.
            var u = new UnityVersion("6.6.6b3c1");
            Assert.AreEqual(6, u.Major);
            Assert.AreEqual(6, u.Minor);
            Assert.AreEqual(6, u.Patch);
            Assert.AreEqual('b', u.Type);
            Assert.AreEqual(3, u.Sequence);
        }

        [Test]
        public void ConstructorGarbage()
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                var u = new UnityVersion("garbage");
            });
            Assert.AreEqual("Failed to parse semantic version garbage", exception.Message);
        }

        [Test]
        public void IsWebAssemblyCompatible2020()
        {
            foreach (var lib in BuildPreProcessor.webAssemblyLibraries
                         .Where(lib => lib.Value == 2020))
            {
                Assert.IsTrue(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2019.1.0b3")));
                Assert.IsTrue(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2021.1.50")));
                Assert.IsFalse(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2021.2")));
            }
        }

        [Test]
        public void IsWebAssemblyCompatible2021()
        {
            foreach (var lib in BuildPreProcessor.webAssemblyLibraries
                         .Where(lib => lib.Value == 2021))
            {
                Assert.IsFalse(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2021.1.99f99")));
                Assert.IsTrue(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2021.2.0f1")));
                Assert.IsTrue(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2022.1.99f99")));
                Assert.IsFalse(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2022.2")));
            }
        }

        [Test]
        public void IsWebAssemblyCompatible2022()
        {
            foreach (var lib in BuildPreProcessor.webAssemblyLibraries
                         .Where(lib => lib.Value == 2022))
            {
                Assert.IsFalse(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2022.1.99f99")));
                Assert.IsTrue(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2022.2.0f1")));
                Assert.IsTrue(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2023.2.0a16")));
                Assert.IsFalse(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2023.2.0a17")));
            }
        }

        [Test]
        public void IsWebAssemblyCompatible2023()
        {
            foreach (var lib in BuildPreProcessor.webAssemblyLibraries
                         .Where(lib => lib.Value == 2023))
            {
                Assert.IsFalse(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2023.2.0a16")));
                Assert.IsTrue(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2023.2.0a17")));
                Assert.IsTrue(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2023.3")));
                Assert.IsTrue(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2024")));
                Assert.IsTrue(BuildPreProcessor.IsWebAssemblyCompatible(lib.Key, new UnityVersion("2025")));
            }
        }

        [Test]
        public void IsWebAssemblyCompatibleInvalid()
        {
            var invalidWasm = new GUID("42424242424242424242424242424242");
            Assert.Throws<InvalidDataException>(() =>
                Assert.IsFalse(BuildPreProcessor.IsWebAssemblyCompatible(invalidWasm, new UnityVersion("2023.2.0a16")))
                );
        }
    }
}
