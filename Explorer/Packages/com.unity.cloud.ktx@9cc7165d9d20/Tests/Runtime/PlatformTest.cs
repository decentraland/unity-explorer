// SPDX-FileCopyrightText: 2024 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using UnityEngine;

namespace KtxUnity.Tests
{
    class PlatformTest
    {
        [Test]
        public void CertifySupportedPlatform()
        {
            KtxNativeInstance.CertifySupportedPlatform();
        }
    }
}
