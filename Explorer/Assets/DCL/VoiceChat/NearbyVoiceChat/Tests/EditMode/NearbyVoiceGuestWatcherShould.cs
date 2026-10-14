using DCL.VoiceChat.Nearby;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System;

namespace DCL.VoiceChat.NearbyVoiceChat.Tests.EditMode
{
    public class NearbyVoiceGuestWatcherShould
    {
        private IWeb3IdentityCache identityCache = null!;
        private NearbyVoiceChatStateModel stateModel = null!;

        [SetUp]
        public void SetUp()
        {
            identityCache = Substitute.For<IWeb3IdentityCache>();
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.Idle);
        }

        [TearDown]
        public void TearDown()
        {
            stateModel.Dispose();
        }

        [Test]
        public void SuppressWhenTheSessionStartsAsGuest()
        {
            // Arrange
            IWeb3Identity guest = IdentityWith(LoginMethod.GUEST);
            identityCache.Identity.Returns(guest);

            // Act
            using var watcher = new NearbyVoiceGuestWatcher(identityCache, stateModel);

            // Assert
            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.Suppressed));
            Assert.That(stateModel.ActiveSuppression.Value, Is.EqualTo(SuppressionReason.Guest));
            Assert.That(stateModel.IsListeningDisabled, Is.True);
        }

        [Test]
        public void SuppressAnEphemeralGuestToo()
        {
            // Arrange
            IWeb3Identity ephemeralGuest = IdentityWith(LoginMethod.EPHEMERAL_GUEST);
            identityCache.Identity.Returns(ephemeralGuest);

            // Act
            using var watcher = new NearbyVoiceGuestWatcher(identityCache, stateModel);

            // Assert
            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.Suppressed));
        }

        [Test]
        public void LeaveAFullAccountUntouched()
        {
            // Arrange
            IWeb3Identity fullAccount = IdentityWith(LoginMethod.EMAIL_OTP);
            identityCache.Identity.Returns(fullAccount);

            // Act
            using var watcher = new NearbyVoiceGuestWatcher(identityCache, stateModel);

            // Assert
            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.Idle));
            Assert.That(stateModel.ActiveSuppression.Value, Is.Null);
        }

        [Test]
        public void ResumeOnceTheGuestUpgrades()
        {
            // Arrange
            IWeb3Identity guest = IdentityWith(LoginMethod.GUEST);
            IWeb3Identity upgraded = IdentityWith(LoginMethod.EMAIL_OTP);
            identityCache.Identity.Returns(guest);
            using var watcher = new NearbyVoiceGuestWatcher(identityCache, stateModel);

            // Act
            identityCache.Identity.Returns(upgraded);
            identityCache.OnIdentityChanged += Raise.Event<Action>();

            // Assert
            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.Idle));
            Assert.That(stateModel.ActiveSuppression.Value, Is.Null);
        }

        [Test]
        public void KeepSuppressingWhileAnotherReasonRemains()
        {
            // Arrange
            IWeb3Identity guest = IdentityWith(LoginMethod.GUEST);
            IWeb3Identity upgraded = IdentityWith(LoginMethod.EMAIL_OTP);
            identityCache.Identity.Returns(guest);
            using var watcher = new NearbyVoiceGuestWatcher(identityCache, stateModel);
            stateModel.Suppress(SuppressionReason.Call);

            // Act
            identityCache.Identity.Returns(upgraded);
            identityCache.OnIdentityChanged += Raise.Event<Action>();

            // Assert
            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.Suppressed));
            Assert.That(stateModel.ActiveSuppression.Value, Is.EqualTo(SuppressionReason.Call));
        }

        private static IWeb3Identity IdentityWith(LoginMethod method)
        {
            var identity = Substitute.For<IWeb3Identity>();
            identity.Method.Returns(method);
            return identity;
        }
    }
}
