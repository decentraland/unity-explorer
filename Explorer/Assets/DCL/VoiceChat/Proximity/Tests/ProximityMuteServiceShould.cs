using Cysharp.Threading.Tasks;
using DCL.VoiceChat.MutePersistence;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.VoiceChat.Tests
{
    [TestFixture]
    public class ProximityMuteServiceShould
    {
        private IProximityMuteCache cache;
        private IProximityMuteRepository repository;
        private ProximityMuteService service;

        [SetUp]
        public void SetUp()
        {
            cache = Substitute.For<IProximityMuteCache>();
            repository = Substitute.For<IProximityMuteRepository>();
            service = new ProximityMuteService(cache, repository);
        }

        [Test]
        public async Task LoadMutedUsersIntoCacheOnStartup()
        {
            // Arrange
            var mutedList = new List<string> { "0xAAA", "0xBBB" };
            repository.GetAllMutedUsersAsync(Arg.Any<CancellationToken>())
                      .Returns(UniTask.FromResult(mutedList));

            // Act
            await service.LoadAsync(CancellationToken.None);

            // Assert
            cache.Received(1).Reset(mutedList);
        }

        [Test]
        public async Task NotCrashWhenLoadFails()
        {
            // Arrange
            repository.GetAllMutedUsersAsync(Arg.Any<CancellationToken>())
                      .Throws(new Exception("Network error"));

            // Act
            await service.LoadAsync(CancellationToken.None);

            // Assert
            cache.DidNotReceive().Reset(Arg.Any<IEnumerable<string>>());
        }

        [Test]
        public async Task CallRepositoryThenCacheOnMute()
        {
            // Arrange
            repository.MuteUserAsync("0xABC", Arg.Any<CancellationToken>())
                      .Returns(UniTask.CompletedTask);

            // Act
            await service.SetMutedAsync("0xABC", true, CancellationToken.None);

            // Assert
            await repository.Received(1).MuteUserAsync("0xABC", Arg.Any<CancellationToken>());
            cache.Received(1).SetMuted("0xABC", true);
        }

        [Test]
        public async Task CallRepositoryThenCacheOnUnmute()
        {
            // Arrange
            repository.UnmuteUserAsync("0xABC", Arg.Any<CancellationToken>())
                      .Returns(UniTask.CompletedTask);

            // Act
            await service.SetMutedAsync("0xABC", false, CancellationToken.None);

            // Assert
            await repository.Received(1).UnmuteUserAsync("0xABC", Arg.Any<CancellationToken>());
            cache.Received(1).SetMuted("0xABC", false);
        }

        [Test]
        public async Task NotUpdateCacheWhenMuteApiFails()
        {
            // Arrange
            repository.MuteUserAsync("0xABC", Arg.Any<CancellationToken>())
                      .Throws(new Exception("Server error"));
            LogAssert.Expect(LogType.Error, new Regex("Failed to mute 0xABC"));

            // Act
            await service.SetMutedAsync("0xABC", true, CancellationToken.None);

            // Assert
            cache.DidNotReceive().SetMuted(Arg.Any<string>(), Arg.Any<bool>());
        }

        [Test]
        public async Task NotUpdateCacheWhenUnmuteApiFails()
        {
            // Arrange
            repository.UnmuteUserAsync("0xABC", Arg.Any<CancellationToken>())
                      .Throws(new Exception("Server error"));
            LogAssert.Expect(LogType.Error, new Regex("Failed to unmute 0xABC"));

            // Act
            await service.SetMutedAsync("0xABC", false, CancellationToken.None);

            // Assert
            cache.DidNotReceive().SetMuted(Arg.Any<string>(), Arg.Any<bool>());
        }

        [Test]
        public void DelegateIsMutedToCache()
        {
            // Arrange
            cache.IsMuted("0xABC").Returns(true);

            // Act & Assert
            Assert.That(service.IsMuted("0xABC"), Is.True);
            cache.Received(1).IsMuted("0xABC");
        }

        [Test]
        public void WorkLocallyWithoutRepository()
        {
            // Arrange
            var localCache = new ProximityMuteCache();
            var localService = new ProximityMuteService(localCache);

            // Act
            localService.SetMuted("0xABC", true);

            // Assert
            Assert.That(localService.IsMuted("0xABC"), Is.True);
        }

        [Test]
        public async Task SkipLoadWhenNoRepository()
        {
            // Arrange
            var localCache = new ProximityMuteCache();
            var localService = new ProximityMuteService(localCache);

            // Act
            await localService.LoadAsync(CancellationToken.None);

            // Assert
            Assert.That(localCache.IsMuted("0xABC"), Is.False);
        }

        [Test]
        public async Task PreserveCacheWhenApiFailsAfterSuccessfulLoad()
        {
            // Arrange — load succeeds, then API goes down
            var realCache = new ProximityMuteCache();
            var failingRepo = Substitute.For<IProximityMuteRepository>();

            failingRepo.GetAllMutedUsersAsync(Arg.Any<CancellationToken>())
                       .Returns(UniTask.FromResult(new List<string> { "0xAAA" }));

            var svc = new ProximityMuteService(realCache, failingRepo);
            await svc.LoadAsync(CancellationToken.None);

            // API starts failing
            failingRepo.MuteUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                       .Throws(new Exception("Server down"));
            LogAssert.Expect(LogType.Error, new Regex("Failed to mute 0xBBB"));

            // Act — mute attempt fails
            await svc.SetMutedAsync("0xBBB", true, CancellationToken.None);

            // Assert — previously loaded mute is still intact, failed mute was not applied
            Assert.That(svc.IsMuted("0xAAA"), Is.True);
            Assert.That(svc.IsMuted("0xBBB"), Is.False);
        }

        [Test]
        public void ToggleMuteFromUnmutedToMuted()
        {
            // Arrange
            var realCache = new ProximityMuteCache();
            var localService = new ProximityMuteService(realCache);

            // Act
            localService.ToggleMute("0xABC");

            // Assert
            Assert.That(localService.IsMuted("0xABC"), Is.True);
        }

        [Test]
        public void ToggleMuteFromMutedToUnmuted()
        {
            // Arrange
            var realCache = new ProximityMuteCache();
            var localService = new ProximityMuteService(realCache);
            localService.SetMuted("0xABC", true);

            // Act
            localService.ToggleMute("0xABC");

            // Assert
            Assert.That(localService.IsMuted("0xABC"), Is.False);
        }

        [Test]
        public void PropagateEventsFromCacheToSubscribers()
        {
            // Arrange
            var realCache = new ProximityMuteCache();
            var localService = new ProximityMuteService(realCache);
            string receivedId = null;
            bool receivedMuted = false;
            localService.MuteStateChanged += (id, muted) =>
            {
                receivedId = id;
                receivedMuted = muted;
            };

            // Act
            localService.SetMuted("0xABC", true);

            // Assert
            Assert.That(receivedId, Is.EqualTo("0xABC"));
            Assert.That(receivedMuted, Is.True);
        }

        [Test]
        public async Task LogWarningWhenLoadFails()
        {
            // Arrange
            repository.GetAllMutedUsersAsync(Arg.Any<CancellationToken>())
                      .Throws(new Exception("Connection refused"));
            LogAssert.Expect(LogType.Warning, new Regex("Failed to load muted users"));

            // Act
            await service.LoadAsync(CancellationToken.None);
        }

        [Test]
        public async Task LetEveryoneBeHeardWhenLoadFails()
        {
            // Arrange — API is down at startup
            var realCache = new ProximityMuteCache();
            var failingRepo = Substitute.For<IProximityMuteRepository>();
            failingRepo.GetAllMutedUsersAsync(Arg.Any<CancellationToken>())
                       .Throws(new Exception("Service unavailable"));

            var svc = new ProximityMuteService(realCache, failingRepo);

            // Act
            await svc.LoadAsync(CancellationToken.None);

            // Assert — no one is muted, voice chat works for everyone
            Assert.That(svc.IsMuted("0xANYONE"), Is.False);
        }

        [Test]
        public async Task NotUpdateCacheWhenLoadIsCancelled()
        {
            // Arrange
            repository.GetAllMutedUsersAsync(Arg.Any<CancellationToken>())
                      .Throws(new OperationCanceledException());

            // Act
            await service.LoadAsync(CancellationToken.None);

            // Assert — cancellation is silently ignored, cache untouched
            cache.DidNotReceive().Reset(Arg.Any<IEnumerable<string>>());
        }

        [Test]
        public async Task NotUpdateCacheWhenMuteIsCancelled()
        {
            // Arrange
            repository.MuteUserAsync("0xABC", Arg.Any<CancellationToken>())
                      .Throws(new OperationCanceledException());

            // Act
            await service.SetMutedAsync("0xABC", true, CancellationToken.None);

            // Assert — cancellation prevents cache update (no partial state)
            cache.DidNotReceive().SetMuted(Arg.Any<string>(), Arg.Any<bool>());
        }

        [Test]
        public async Task CallApiBeforeUpdatingCacheOnMute()
        {
            // Arrange — verify cache is NOT updated during the API call
            repository.MuteUserAsync("0xABC", Arg.Any<CancellationToken>())
                      .Returns(_ =>
                      {
                          cache.DidNotReceive().SetMuted(Arg.Any<string>(), Arg.Any<bool>());
                          return UniTask.CompletedTask;
                      });

            // Act
            await service.SetMutedAsync("0xABC", true, CancellationToken.None);

            // Assert — after API succeeds, cache IS updated
            cache.Received(1).SetMuted("0xABC", true);
        }

        [Test]
        public async Task UpdateCacheDirectlyWhenAsyncMuteHasNoRepository()
        {
            // Arrange — local-only mode, no API backend
            var localCache = new ProximityMuteCache();
            var localService = new ProximityMuteService(localCache);

            // Act — async path skips API and updates cache directly
            await localService.SetMutedAsync("0xABC", true, CancellationToken.None);

            // Assert
            Assert.That(localService.IsMuted("0xABC"), Is.True);
        }
    }
}
