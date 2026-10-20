using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SocialService;
using NSubstitute;
using NSubstitute.Core;
using NUnit.Framework;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Friends.Tests
{
    public class FriendRequestsListingShould
    {
        public enum Listing
        {
            Received,
            Sent,
        }

        private const int PAGE_SIZE = 10;

        private SelfProfile selfProfile = null!;
        private IRPCSocialServices socialServices = null!;
        private RPCFriendsService service = null!;

        [SetUp]
        public void SetUp()
        {
            selfProfile = Substitute.For<SelfProfile>();
            socialServices = Substitute.For<IRPCSocialServices>();
            service = new RPCFriendsService(Substitute.For<IFriendsEventBus>(), new FriendsCache(), selfProfile, socialServices);
        }

        [TestCase(Listing.Received)]
        [TestCase(Listing.Sent)]
        public async Task ReturnAnEmptyPageWithoutAskingTheServerWhileTheOwnProfileIsNotDeployed(Listing listing)
        {
            // Arrange
            OwnProfileReads(ProfileReadResult.FromError(ProfileReadError.NotFound));

            // Act
            using PaginatedFriendRequestsResult page = await ListAsync(listing);

            // Assert
            Assert.That(page.TotalAmount, Is.EqualTo(0));
            Assert.That(page.Requests, Is.Empty);
            Assert.That(ConnectionAttempts(), Is.EqualTo(0));
        }

        [TestCase(Listing.Received)]
        [TestCase(Listing.Sent)]
        public void SurfaceAFailedOwnProfileReadInsteadOfAnEmptyPage(Listing listing)
        {
            // Arrange
            OwnProfileReads(ProfileReadResult.FromError(ProfileReadError.FetchFailed));

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(() => ListAsync(listing).AsTask());
            Assert.That(ConnectionAttempts(), Is.EqualTo(0));
        }

        [TestCase(Listing.Received)]
        [TestCase(Listing.Sent)]
        public void SurfaceACancelledOwnProfileReadInsteadOfAnEmptyPage(Listing listing)
        {
            // Arrange
            OwnProfileReads(ProfileReadResult.FromError(ProfileReadError.Cancelled));

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(() => ListAsync(listing).AsTask());
            Assert.That(ConnectionAttempts(), Is.EqualTo(0));
        }

        [TestCase(Listing.Received)]
        [TestCase(Listing.Sent)]
        public void AskTheServerWhenTheOwnProfileIsResolved(Listing listing)
        {
            // Arrange
            OwnProfileReads(ProfileReadResult.FromOk(Profile.NewRandomProfile()));
            var serverUnreachable = new IOException("No social service in this test");
            socialServices.EnsureRpcConnectionAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromException(serverUnreachable));

            // Act
            IOException thrown = Assert.ThrowsAsync<IOException>(() => ListAsync(listing).AsTask());

            // Assert
            Assert.That(thrown, Is.SameAs(serverUnreachable));
            Assert.That(ConnectionAttempts(), Is.EqualTo(1));
        }

        private void OwnProfileReads(ProfileReadResult read) =>
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(read));

        private UniTask<PaginatedFriendRequestsResult> ListAsync(Listing listing) =>
            listing == Listing.Received
                ? service.GetReceivedFriendRequestsAsync(0, PAGE_SIZE, CancellationToken.None)
                : service.GetSentFriendRequestsAsync(0, PAGE_SIZE, CancellationToken.None);

        /// <summary>Counts either overload, since the single-token one is an interface default method the proxy may or may not intercept.</summary>
        private int ConnectionAttempts()
        {
            var attempts = 0;

            foreach (ICall call in socialServices.ReceivedCalls())
                if (call.GetMethodInfo().Name == nameof(IRPCSocialServices.EnsureRpcConnectionAsync))
                    attempts++;

            return attempts;
        }
    }
}
