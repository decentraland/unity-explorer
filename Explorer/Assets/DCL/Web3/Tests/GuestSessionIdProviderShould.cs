using DCL.Web3.Authenticators;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Web3.Tests
{
    [TestFixture]
    public class GuestSessionIdProviderShould
    {
        private const string DEVICE_ID = "device-abc-123";

        [Test]
        public void PreferOverrideOverDeviceId()
        {
            // Arrange
            const string OVERRIDE = "creator-session-1";

            // Act
            string? resolved = GuestSessionIdProvider.Resolve(OVERRIDE, DEVICE_ID);

            // Assert
            Assert.That(resolved, Is.EqualTo(OVERRIDE));
        }

        [Test]
        public void ResolveSameIdForSameDeviceId()
        {
            // Act
            string? first = GuestSessionIdProvider.Resolve(null, DEVICE_ID);
            string? second = GuestSessionIdProvider.Resolve(null, DEVICE_ID);

            // Assert
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ResolveDifferentIdsForDifferentDeviceIds()
        {
            // Act
            string? first = GuestSessionIdProvider.Resolve(null, DEVICE_ID);
            string? second = GuestSessionIdProvider.Resolve(null, "device-xyz-789");

            // Assert
            Assert.That(first, Is.Not.EqualTo(second));
        }

        [TestCase("  DEVICE-ABC-123  ", TestName = "padded and uppercased")]
        [TestCase("Device-Abc-123", TestName = "mixed case")]
        public void IgnoreCaseAndSurroundingWhitespace(string variant)
        {
            // Act
            string? canonical = GuestSessionIdProvider.Resolve(null, DEVICE_ID);
            string? resolved = GuestSessionIdProvider.Resolve(null, variant);

            // Assert
            Assert.That(resolved, Is.EqualTo(canonical));
        }

        [Test]
        public void NeverExposeRawDeviceId()
        {
            // Act
            string? resolved = GuestSessionIdProvider.Resolve(null, DEVICE_ID);

            // Assert
            Assert.That(resolved, Does.Not.Contain(DEVICE_ID));
            Assert.That(resolved!.Length, Is.EqualTo(64));
        }

        [TestCase("", TestName = "empty device id")]
        [TestCase(null, TestName = "null device id")]
        public void ResolveNullWhenDeviceIdIsMissing(string? rawDeviceId)
        {
            // Act
            string? resolved = GuestSessionIdProvider.Resolve(null, rawDeviceId!);

            // Assert
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void ResolveNullWhenDeviceIdIsUnsupported()
        {
            // Act
            string? resolved = GuestSessionIdProvider.Resolve(null, SystemInfo.unsupportedIdentifier);

            // Assert
            Assert.That(resolved, Is.Null);
        }
    }
}