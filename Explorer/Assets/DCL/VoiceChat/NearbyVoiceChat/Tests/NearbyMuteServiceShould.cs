using Cysharp.Threading.Tasks;
using DCL.VoiceChat.Nearby.MutePersistence;
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

namespace DCL.VoiceChat.Nearby.Tests
{
    [TestFixture]
    public class NearbyMuteServiceShould
    {
        private INearbyMuteCache cache;
        private INearbyMuteRepository repository;
        private NearbyMuteService service;

        [SetUp]
        public void SetUp()
        {
            cache = Substitute.For<INearbyMuteCache>();
            repository = Substitute.For<INearbyMuteRepository>();
            service = new NearbyMuteService(cache, repository);
        }

        [Test]
        public async Task LoadMutedUsersIntoCacheOnStartup()
        {
            var mutedList = new List<string> { "0xAAA", "0xBBB" };
            repository.GetAllMutedUsersAsync(Arg.Any<CancellationToken>())
                      .Returns(UniTask.FromResult(mutedList));

            await service.LoadAsync(CancellationToken.None);

            cache.Received(1).Reset(mutedList);
        }

        [Test]
        public async Task NotCrashWhenLoadFails()
        {
            repository.GetAllMutedUsersAsync(Arg.Any<CancellationToken>())
                      .Throws(new Exception("Network error"));

            await service.LoadAsync(CancellationToken.None);

            cache.DidNotReceive().Reset(Arg.Any<IEnumerable<string>>());
        }

        [Test]
        public async Task CallCacheThenRepositoryOnMute()
        {
            repository.MuteUserAsync("0xABC", Arg.Any<CancellationToken>())
                      .Returns(UniTask.CompletedTask);

            await service.SetMutedAsync("0xABC", true, CancellationToken.None);

            Received.InOrder(() =>
            {
                cache.SetMuted("0xABC", true);
                repository.MuteUserAsync("0xABC", Arg.Any<CancellationToken>());
            });
        }

        [Test]
        public async Task CallCacheThenRepositoryOnUnmute()
        {
            repository.UnmuteUserAsync("0xABC", Arg.Any<CancellationToken>())
                      .Returns(UniTask.CompletedTask);

            await service.SetMutedAsync("0xABC", false, CancellationToken.None);

            Received.InOrder(() =>
            {
                cache.SetMuted("0xABC", false);
                repository.UnmuteUserAsync("0xABC", Arg.Any<CancellationToken>());
            });
        }

        [Test]
        public async Task StillUpdateCacheWhenMuteApiFails()
        {
            // Graceful degradation: cache updated locally even if API fails
            repository.MuteUserAsync("0xABC", Arg.Any<CancellationToken>())
                      .Throws(new Exception("Server error"));
            LogAssert.Expect(LogType.Warning, new Regex("Failed to mute 0xABC"));

            await service.SetMutedAsync("0xABC", true, CancellationToken.None);

            cache.Received(1).SetMuted("0xABC", true);
        }

        [Test]
        public async Task StillUpdateCacheWhenUnmuteApiFails()
        {
            // Graceful degradation: cache updated locally even if API fails
            repository.UnmuteUserAsync("0xABC", Arg.Any<CancellationToken>())
                      .Throws(new Exception("Server error"));
            LogAssert.Expect(LogType.Warning, new Regex("Failed to unmute 0xABC"));

            await service.SetMutedAsync("0xABC", false, CancellationToken.None);

            cache.Received(1).SetMuted("0xABC", false);
        }

        [Test]
        public void DelegateIsMutedToCache()
        {
            cache.IsMuted("0xABC").Returns(true);

            Assert.That(service.IsMuted("0xABC"), Is.True);
            cache.Received(1).IsMuted("0xABC");
        }

        [Test]
        public async Task SkipLoadWhenNoRepository()
        {
            var localCache = new NearbyMuteCache();
            var localService = new NearbyMuteService(localCache);

            await localService.LoadAsync(CancellationToken.None);

            Assert.That(localCache.IsMuted("0xABC"), Is.False);
        }

        [Test]
        public async Task LogWarningWhenLoadFails()
        {
            repository.GetAllMutedUsersAsync(Arg.Any<CancellationToken>())
                      .Throws(new Exception("Connection refused"));
            LogAssert.Expect(LogType.Warning, new Regex("Failed to load muted users"));

            await service.LoadAsync(CancellationToken.None);
        }

        [Test]
        public async Task LetEveryoneBeHeardWhenLoadFails()
        {
            var realCache = new NearbyMuteCache();
            var failingRepo = Substitute.For<INearbyMuteRepository>();
            failingRepo.GetAllMutedUsersAsync(Arg.Any<CancellationToken>())
                       .Throws(new Exception("Service unavailable"));

            var svc = new NearbyMuteService(realCache, failingRepo);

            await svc.LoadAsync(CancellationToken.None);

            Assert.That(svc.IsMuted("0xANYONE"), Is.False);
        }

        [Test]
        public async Task NotUpdateCacheWhenLoadIsCancelled()
        {
            repository.GetAllMutedUsersAsync(Arg.Any<CancellationToken>())
                      .Throws(new OperationCanceledException());

            await service.LoadAsync(CancellationToken.None);

            cache.DidNotReceive().Reset(Arg.Any<IEnumerable<string>>());
        }

        [Test]
        public async Task UpdateCacheEvenWhenMuteIsCancelled()
        {
            // Cache is updated before the API call, so cancellation of the API does not roll it back
            repository.MuteUserAsync("0xABC", Arg.Any<CancellationToken>())
                      .Throws(new OperationCanceledException());

            await service.SetMutedAsync("0xABC", true, CancellationToken.None);

            cache.Received(1).SetMuted("0xABC", true);
        }

        [Test]
        public async Task UpdateCacheBeforeCallingApiOnMute()
        {
            repository.MuteUserAsync("0xABC", Arg.Any<CancellationToken>())
                      .Returns(_ =>
                      {
                          cache.Received(1).SetMuted("0xABC", true);
                          return UniTask.CompletedTask;
                      });

            await service.SetMutedAsync("0xABC", true, CancellationToken.None);

            await repository.Received(1).MuteUserAsync("0xABC", Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task UpdateCacheDirectlyWhenAsyncMuteHasNoRepository()
        {
            var localCache = new NearbyMuteCache();
            var localService = new NearbyMuteService(localCache);

            await localService.SetMutedAsync("0xABC", true, CancellationToken.None);

            Assert.That(localService.IsMuted("0xABC"), Is.True);
        }
    }
}
