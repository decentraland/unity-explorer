using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DCL.Web3;

namespace DCL.PrivateWorlds.Tests.EditMode
{
    [TestFixture]
    public class WorldPermissionsServiceShould
    {
        private IWeb3IdentityCache identityCache = null!;
        private ICommunityMembershipChecker communityChecker = null!;
        private TestableWorldPermissionsService service = null!;

        /// <summary>
        /// Subclass that overrides the HTTP-based GetWorldPermissionsAsync
        /// so tests can feed controlled WorldAccessInfo data.
        /// </summary>
        private class TestableWorldPermissionsService : WorldPermissionsService
        {
            private WorldAccessInfo? stubbedPermissions;

            public TestableWorldPermissionsService(
                IWebRequestController webRequestController,
                IDecentralandUrlsSource urlsSource,
                IWeb3IdentityCache web3IdentityCache,
                ICommunityMembershipChecker? communityMembershipChecker = null)
                : base(webRequestController, urlsSource, web3IdentityCache, communityMembershipChecker) { }

            public void StubPermissions(WorldAccessInfo? info) => stubbedPermissions = info;

            public override UniTask<WorldAccessInfo?> GetWorldPermissionsAsync(string worldName, CancellationToken ct)
                => UniTask.FromResult(stubbedPermissions);
        }

        [SetUp]
        public void SetUp()
        {
            identityCache = Substitute.For<IWeb3IdentityCache>();
            communityChecker = Substitute.For<ICommunityMembershipChecker>();

            service = new TestableWorldPermissionsService(
                Substitute.For<IWebRequestController>(),
                Substitute.For<IDecentralandUrlsSource>(),
                identityCache,
                communityChecker);
        }

        [Test]
        public async Task ReturnsAllowed_WhenUnrestricted()
        {
            service.StubPermissions(new WorldAccessInfo { AccessType = WorldAccessType.Unrestricted });

            var ctx = await service.CheckWorldAccessAsync("world", CancellationToken.None);

            Assert.AreEqual(WorldAccessCheckResult.Allowed, ctx.Result);
        }

        [Test]
        public async Task ReturnsPasswordRequired_WhenSharedSecret()
        {
            service.StubPermissions(new WorldAccessInfo { AccessType = WorldAccessType.SharedSecret });

            var ctx = await service.CheckWorldAccessAsync("world", CancellationToken.None);

            Assert.AreEqual(WorldAccessCheckResult.PasswordRequired, ctx.Result);
        }

        [Test]
        public async Task ReturnsCheckFailed_WhenPermissionsAreNull()
        {
            service.StubPermissions(null);

            var ctx = await service.CheckWorldAccessAsync("world", CancellationToken.None);

            Assert.AreEqual(WorldAccessCheckResult.CheckFailed, ctx.Result);
            Assert.IsNotNull(ctx.ErrorMessage);
        }

        [Test]
        public async Task AllowList_ReturnsAllowed_WhenWalletIsInList()
        {
            // Arrange
            var identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address("0xAlice"));
            identityCache.Identity.Returns(identity);

            service.StubPermissions(new WorldAccessInfo
            {
                AccessType = WorldAccessType.AllowList,
                OwnerAddress = "0xOwner",
                AllowedWallets = new List<string> { "0xalice", "0xBob" }
            });

            // Act
            var ctx = await service.CheckWorldAccessAsync("world", CancellationToken.None);

            // Assert
            Assert.AreEqual(WorldAccessCheckResult.Allowed, ctx.Result);
        }

        [Test]
        public async Task AllowList_ReturnsAllowed_WhenWalletIsOwner()
        {
            // Arrange
            var identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address("0xOwner"));
            identityCache.Identity.Returns(identity);

            service.StubPermissions(new WorldAccessInfo
            {
                AccessType = WorldAccessType.AllowList,
                OwnerAddress = "0xowner", // different case
                AllowedWallets = new List<string>() // wallet not in list
            });

            // Act
            var ctx = await service.CheckWorldAccessAsync("world", CancellationToken.None);

            // Assert
            Assert.AreEqual(WorldAccessCheckResult.Allowed, ctx.Result);
        }

        [Test]
        public async Task AllowList_ReturnsDenied_WhenWalletNotInListAndNotOwner()
        {
            // Arrange
            var identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address("0xStranger"));
            identityCache.Identity.Returns(identity);

            service.StubPermissions(new WorldAccessInfo
            {
                AccessType = WorldAccessType.AllowList,
                OwnerAddress = "0xOwner",
                AllowedWallets = new List<string> { "0xAlice" }
            });

            // Act
            var ctx = await service.CheckWorldAccessAsync("world", CancellationToken.None);

            // Assert
            Assert.AreEqual(WorldAccessCheckResult.AccessDenied, ctx.Result);
        }

        [Test]
        public async Task AllowList_ReturnsDenied_WhenIdentityIsNull()
        {
            // Arrange — no logged-in user
            identityCache.Identity.Returns((IWeb3Identity?)null);

            service.StubPermissions(new WorldAccessInfo
            {
                AccessType = WorldAccessType.AllowList,
                OwnerAddress = "0xOwner",
                AllowedWallets = new List<string> { "0xAlice" }
            });

            // Act
            var ctx = await service.CheckWorldAccessAsync("world", CancellationToken.None);

            // Assert
            Assert.AreEqual(WorldAccessCheckResult.AccessDenied, ctx.Result);
        }

        [Test]
        public async Task AllowList_ReturnsAllowed_WhenUserIsCommunityMember()
        {
            // Arrange — wallet not in list, but user is a member of an allowed community
            var identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address("0xStranger"));
            identityCache.Identity.Returns(identity);

            communityChecker.IsMemberOfCommunityAsync("community-1", Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(true));

            service.StubPermissions(new WorldAccessInfo
            {
                AccessType = WorldAccessType.AllowList,
                OwnerAddress = "0xOwner",
                AllowedWallets = new List<string>(),
                AllowedCommunities = new List<string> { "community-1" }
            });

            // Act
            var ctx = await service.CheckWorldAccessAsync("world", CancellationToken.None);

            // Assert
            Assert.AreEqual(WorldAccessCheckResult.Allowed, ctx.Result);
        }

        [Test]
        public async Task AllowList_ReturnsAllowed_WhenSecondCommunityMatches()
        {
            // Arrange — first community check returns false, second returns true
            var identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address("0xStranger"));
            identityCache.Identity.Returns(identity);

            communityChecker.IsMemberOfCommunityAsync("community-1", Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(false));
            communityChecker.IsMemberOfCommunityAsync("community-2", Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(true));

            service.StubPermissions(new WorldAccessInfo
            {
                AccessType = WorldAccessType.AllowList,
                OwnerAddress = "0xOwner",
                AllowedWallets = new List<string>(),
                AllowedCommunities = new List<string> { "community-1", "community-2" }
            });

            // Act
            var ctx = await service.CheckWorldAccessAsync("world", CancellationToken.None);

            // Assert
            Assert.AreEqual(WorldAccessCheckResult.Allowed, ctx.Result);
        }

        [Test]
        public async Task AllowList_ReturnsDenied_WhenNoCommunityMatches()
        {
            // Arrange
            var identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address("0xStranger"));
            identityCache.Identity.Returns(identity);

            communityChecker.IsMemberOfCommunityAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(false));

            service.StubPermissions(new WorldAccessInfo
            {
                AccessType = WorldAccessType.AllowList,
                OwnerAddress = "0xOwner",
                AllowedWallets = new List<string>(),
                AllowedCommunities = new List<string> { "community-1", "community-2" }
            });

            // Act
            var ctx = await service.CheckWorldAccessAsync("world", CancellationToken.None);

            // Assert
            Assert.AreEqual(WorldAccessCheckResult.AccessDenied, ctx.Result);
        }

        [Test]
        public async Task AllowList_ContinuesChecking_WhenOneCommunityCheckThrows()
        {
            // Arrange — first community throws, second succeeds
            var identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address("0xStranger"));
            identityCache.Identity.Returns(identity);

            communityChecker.IsMemberOfCommunityAsync("bad-community", Arg.Any<CancellationToken>())
                .Returns(_ => UniTask.FromException<bool>(new Exception("network error")));
            communityChecker.IsMemberOfCommunityAsync("good-community", Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(true));

            service.StubPermissions(new WorldAccessInfo
            {
                AccessType = WorldAccessType.AllowList,
                OwnerAddress = "0xOwner",
                AllowedWallets = new List<string>(),
                AllowedCommunities = new List<string> { "bad-community", "good-community" }
            });

            // Act
            var ctx = await service.CheckWorldAccessAsync("world", CancellationToken.None);

            // Assert — should still allow because second community succeeded
            Assert.AreEqual(WorldAccessCheckResult.Allowed, ctx.Result);
        }

        [Test]
        public async Task AllowList_SkipsCommunityCheck_WhenCheckerIsNull()
        {
            // Arrange — service created without community checker
            var serviceWithoutCommunity = new TestableWorldPermissionsService(
                Substitute.For<IWebRequestController>(),
                Substitute.For<IDecentralandUrlsSource>(),
                identityCache,
                communityMembershipChecker: null);

            var identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address("0xStranger"));
            identityCache.Identity.Returns(identity);

            serviceWithoutCommunity.StubPermissions(new WorldAccessInfo
            {
                AccessType = WorldAccessType.AllowList,
                OwnerAddress = "0xOwner",
                AllowedWallets = new List<string>(),
                AllowedCommunities = new List<string> { "community-1" }
            });

            // Act
            var ctx = await serviceWithoutCommunity.CheckWorldAccessAsync("world", CancellationToken.None);

            // Assert — denied because community checker is null, can't verify membership
            Assert.AreEqual(WorldAccessCheckResult.AccessDenied, ctx.Result);
        }

        [Test]
        public async Task AllowList_SkipsCommunityCheck_WhenNoCommunities()
        {
            // Arrange — allow-list with wallets only, no communities
            var identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address("0xStranger"));
            identityCache.Identity.Returns(identity);

            service.StubPermissions(new WorldAccessInfo
            {
                AccessType = WorldAccessType.AllowList,
                OwnerAddress = "0xOwner",
                AllowedWallets = new List<string>(),
                AllowedCommunities = new List<string>() // empty
            });

            // Act
            var ctx = await service.CheckWorldAccessAsync("world", CancellationToken.None);

            // Assert
            Assert.AreEqual(WorldAccessCheckResult.AccessDenied, ctx.Result);
            // Verify community checker was never called
            communityChecker.DidNotReceive().IsMemberOfCommunityAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }
}
