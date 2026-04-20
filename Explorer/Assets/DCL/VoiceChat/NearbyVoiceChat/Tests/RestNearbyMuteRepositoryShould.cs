using Cysharp.Threading.Tasks;
using DCL.VoiceChat.Nearby.MutePersistence;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.VoiceChat.Nearby.Tests
{
    [TestFixture]
    public class RestNearbyMuteRepositoryShould
    {
        private List<int> fetchedOffsets = null!;

        [SetUp]
        public void SetUp()
        {
            fetchedOffsets = new List<int>();
        }

        [Test]
        public async Task ReturnEmptyWhenFirstPageHasNullResults()
        {
            Func<int, CancellationToken, UniTask<GetMutesResponse>> fetcher = (offset, _) =>
            {
                fetchedOffsets.Add(offset);
                return UniTask.FromResult(Page(results: null, total: 0));
            };

            List<string> result = await RestNearbyMuteRepository.PaginateMutesAsync(fetcher, CancellationToken.None);

            Assert.That(result, Is.Empty);
            Assert.That(fetchedOffsets, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public async Task ReturnEmptyWhenFirstPageHasEmptyResults()
        {
            Func<int, CancellationToken, UniTask<GetMutesResponse>> fetcher = (offset, _) =>
            {
                fetchedOffsets.Add(offset);
                return UniTask.FromResult(Page(results: Array.Empty<GetMutesResponse.MutedUserEntry>(), total: 0));
            };

            List<string> result = await RestNearbyMuteRepository.PaginateMutesAsync(fetcher, CancellationToken.None);

            Assert.That(result, Is.Empty);
            Assert.That(fetchedOffsets, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public async Task CollectEntriesFromSinglePageSmallerThanPageSize()
        {
            Func<int, CancellationToken, UniTask<GetMutesResponse>> fetcher = (offset, _) =>
            {
                fetchedOffsets.Add(offset);
                return UniTask.FromResult(Page(Addresses("0xAAA", "0xBBB"), total: 2));
            };

            List<string> result = await RestNearbyMuteRepository.PaginateMutesAsync(fetcher, CancellationToken.None);

            Assert.That(result, Is.EqualTo(new[] { "0xAAA", "0xBBB" }));
            Assert.That(fetchedOffsets, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public async Task CollectAcrossMultiplePages()
        {
            GetMutesResponse[] pages =
            {
                Page(RepeatAddresses("0xA", RestNearbyMuteRepository.PAGE_SIZE), total: 250),
                Page(RepeatAddresses("0xB", RestNearbyMuteRepository.PAGE_SIZE), total: 250),
                Page(RepeatAddresses("0xC", 50), total: 250),
            };

            List<string> result = await PaginateAsync(pages);

            Assert.That(result.Count, Is.EqualTo(250));
            Assert.That(fetchedOffsets, Is.EqualTo(new[] { 0, RestNearbyMuteRepository.PAGE_SIZE, RestNearbyMuteRepository.PAGE_SIZE * 2 }));
        }

        [Test]
        public async Task AdvanceOffsetByActualResultsLengthNotFixedPageSize()
        {
            // Server returns 42 entries though PAGE_SIZE asked for 100; next offset must be 42, not 100.
            GetMutesResponse[] pages =
            {
                Page(RepeatAddresses("0xA", 42), total: 50),
                Page(RepeatAddresses("0xB", 8), total: 50),
            };

            List<string> result = await PaginateAsync(pages);

            Assert.That(result.Count, Is.EqualTo(50));
            Assert.That(fetchedOffsets, Is.EqualTo(new[] { 0, 42 }));
        }

        [Test]
        public async Task SkipEntriesWithEmptyAddress()
        {
            GetMutesResponse page = Page(
                new[]
                {
                    new GetMutesResponse.MutedUserEntry { Address = "0xAAA" },
                    new GetMutesResponse.MutedUserEntry { Address = "" },
                    new GetMutesResponse.MutedUserEntry { Address = null! },
                    new GetMutesResponse.MutedUserEntry { Address = "0xBBB" },
                },
                total: 4);

            List<string> result = await PaginateAsync(new[] { page });

            Assert.That(result, Is.EqualTo(new[] { "0xAAA", "0xBBB" }));
        }

        [Test]
        public async Task StopWhenApiLiesAboutTotalButReturnsEmptyPage()
        {
            // API says Total is int.MaxValue but second page comes empty → must terminate gracefully.
            GetMutesResponse[] pages =
            {
                Page(RepeatAddresses("0xA", RestNearbyMuteRepository.PAGE_SIZE), total: int.MaxValue),
                Page(results: Array.Empty<GetMutesResponse.MutedUserEntry>(), total: int.MaxValue),
            };

            List<string> result = await PaginateAsync(pages);

            Assert.That(result.Count, Is.EqualTo(RestNearbyMuteRepository.PAGE_SIZE));
            Assert.That(fetchedOffsets.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task HonorMaxPagesSafetyCapWhenTotalIsRunaway()
        {
            // Every page is full and Total claims there's always more — hard cap must kick in.
            Func<int, CancellationToken, UniTask<GetMutesResponse>> fetcher = (offset, _) =>
            {
                fetchedOffsets.Add(offset);
                return UniTask.FromResult(Page(RepeatAddresses("0xA", RestNearbyMuteRepository.PAGE_SIZE), total: int.MaxValue));
            };

            List<string> result = await RestNearbyMuteRepository.PaginateMutesAsync(fetcher, CancellationToken.None);

            Assert.That(fetchedOffsets.Count, Is.EqualTo(RestNearbyMuteRepository.MAX_PAGES));
            Assert.That(result.Count, Is.EqualTo(RestNearbyMuteRepository.MAX_PAGES * RestNearbyMuteRepository.PAGE_SIZE));
        }

        [Test]
        public async Task ReturnAlreadyCollectedWhenCancelledMidPagination()
        {
            var cts = new CancellationTokenSource();
            var pageIndex = 0;

            Func<int, CancellationToken, UniTask<GetMutesResponse>> fetcher = (offset, _) =>
            {
                fetchedOffsets.Add(offset);

                // Cancel after first page is fetched — loop check happens at next iteration.
                if (pageIndex == 0)
                {
                    pageIndex++;
                    cts.Cancel();
                    return UniTask.FromResult(Page(RepeatAddresses("0xA", RestNearbyMuteRepository.PAGE_SIZE), total: 500));
                }

                // Should not reach here.
                Assert.Fail("Pagination should have been cancelled before requesting a second page");
                return UniTask.FromResult(default(GetMutesResponse));
            };

            List<string> result = await RestNearbyMuteRepository.PaginateMutesAsync(fetcher, cts.Token);

            Assert.That(result.Count, Is.EqualTo(RestNearbyMuteRepository.PAGE_SIZE));
            Assert.That(fetchedOffsets.Count, Is.EqualTo(1));
        }

        private UniTask<List<string>> PaginateAsync(IReadOnlyList<GetMutesResponse> pages)
        {
            var pageIndex = 0;

            Func<int, CancellationToken, UniTask<GetMutesResponse>> fetcher = (offset, _) =>
            {
                fetchedOffsets.Add(offset);
                GetMutesResponse response = pages[pageIndex];
                pageIndex++;
                return UniTask.FromResult(response);
            };

            return RestNearbyMuteRepository.PaginateMutesAsync(fetcher, CancellationToken.None);
        }

        private static GetMutesResponse Page(GetMutesResponse.MutedUserEntry[]? results, int total) =>
            new ()
            {
                Data = new GetMutesResponse.MutesData
                {
                    Results = results!,
                    Total = total,
                },
            };

        private static GetMutesResponse.MutedUserEntry[] Addresses(params string[] addresses)
        {
            var entries = new GetMutesResponse.MutedUserEntry[addresses.Length];

            for (var i = 0; i < addresses.Length; i++)
                entries[i] = new GetMutesResponse.MutedUserEntry { Address = addresses[i] };

            return entries;
        }

        private static GetMutesResponse.MutedUserEntry[] RepeatAddresses(string prefix, int count)
        {
            var entries = new GetMutesResponse.MutedUserEntry[count];

            for (var i = 0; i < count; i++)
                entries[i] = new GetMutesResponse.MutedUserEntry { Address = prefix + i };

            return entries;
        }
    }
}
