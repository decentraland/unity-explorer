using DCL.Profiles;
using ECS.TestSuite;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace DCL.Friends.Tests
{
    public class BlockedProfileShould
    {
        private const string USER_ID = "0x0000000000000000000000000000000000000001";
        private const string USER_ID_CHECKSUMMED = "0x000000000000000000000000000000000000000A";
        private const string USER_ID_LOWERCASE = "0x000000000000000000000000000000000000000a";
        private const string OTHER_USER_ID = "0x0000000000000000000000000000000000000002";

        private static readonly DateTime BLOCKED_AT = new (2026, 1, 1);
        private static readonly DateTime LATER_BLOCKED_AT = new (2026, 2, 1);

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // The editor domain may still hold an initialized registry from a play-mode session
            EcsTestsUtils.TearDownFeaturesRegistry();
            EcsTestsUtils.SetUpFeaturesRegistry();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [Test]
        public void BeRemovedFromListByAnotherInstanceWithTheSameAddress()
        {
            // Arrange
            var listed = new List<BlockedProfile> { CreateProfile(USER_ID, "Listed", BLOCKED_AT) };
            BlockedProfile unblocked = CreateProfile(USER_ID, "Renamed", LATER_BLOCKED_AT);

            // Act
            bool removed = listed.Remove(unblocked);

            // Assert
            Assert.That(removed, Is.True);
            Assert.That(listed, Is.Empty);
        }

        [Test]
        public void BeEqualWhenAddressesDifferOnlyInCase()
        {
            // Arrange
            BlockedProfile checksummed = CreateProfile(USER_ID_CHECKSUMMED, "User", BLOCKED_AT);
            BlockedProfile lowercase = CreateProfile(USER_ID_LOWERCASE, "User", BLOCKED_AT);

            // Act
            bool equal = checksummed.Equals(lowercase);

            // Assert
            Assert.That(equal, Is.True);
            Assert.That(checksummed.GetHashCode(), Is.EqualTo(lowercase.GetHashCode()));
        }

        [Test]
        public void NotBeEqualWhenAddressesDiffer()
        {
            // Arrange
            BlockedProfile profile = CreateProfile(USER_ID, "User", BLOCKED_AT);
            BlockedProfile other = CreateProfile(OTHER_USER_ID, "User", BLOCKED_AT);

            // Act
            bool equal = profile.Equals(other);

            // Assert
            Assert.That(equal, Is.False);
        }

        private static BlockedProfile CreateProfile(string userId, string name, DateTime blockedAt) =>
            new (UserId.New(userId).Unwrap(), name, false, string.Empty, blockedAt);
    }
}
