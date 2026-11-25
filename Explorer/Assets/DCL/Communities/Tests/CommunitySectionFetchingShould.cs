using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesCard;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Communities.Tests
{
    public class CommunitySectionFetchingShould
    {
        [Test]
        public void RetryTheInterruptedPageWhenScrollingAfterReentry()
        {
            // Arrange
            using var firstVisit = new CancellationTokenSource();
            var view = Substitute.For<ICommunityFetchingView<int>>();
            using var section = new Section(view);
            section.Visit(firstVisit.Token);
            view.NewDataRequested += Raise.Event<Action>();
            section.Pending.TrySetResult(40);

            // Act
            view.NewDataRequested += Raise.Event<Action>();
            firstVisit.Cancel();
            section.Visit(CancellationToken.None);
            view.ClearReceivedCalls();
            section.Pending.TrySetResult(40);
            view.NewDataRequested += Raise.Event<Action>();

            // Assert
            view.Received(1).SetLoadingMoreBadgeActive(false);
            Assert.That(section.RequestedPages, Is.EqualTo(new[] { 1, 2, 2 }));
            section.Pending.TrySetResult(40);
        }

        [Test]
        public void KeepTheReplacementFetchBusyWhenThePreviousFetchFinishes()
        {
            // Arrange
            using var firstVisit = new CancellationTokenSource();
            var view = Substitute.For<ICommunityFetchingView<int>>();
            using var section = new Section(view);
            section.Visit(firstVisit.Token);
            view.NewDataRequested += Raise.Event<Action>();
            UniTaskCompletionSource<int?> previous = section.Pending;

            // Act
            UniTask replacement = section.Fetch();
            view.ClearReceivedCalls();
            previous.TrySetResult(0);
            view.NewDataRequested += Raise.Event<Action>();

            // Assert
            view.DidNotReceive().SetLoadingStateActive(false);
            Assert.That(section.Items, Is.Empty);
            view.DidNotReceive().RefreshGrid(Arg.Any<SectionFetchData<int>>(), Arg.Any<bool>());

            Assert.That(section.RequestedPages, Is.EqualTo(new[] { 1, 1 }));

            // Act
            section.Pending.TrySetResult(40);
            replacement.GetAwaiter().GetResult();

            // Assert
            view.Received(1).RefreshGrid(Arg.Any<SectionFetchData<int>>(), false);
        }

        [Test]
        public void IgnoreAPageThatFinishesAfterResetWithALiveVisitToken()
        {
            // Arrange
            var view = Substitute.For<ICommunityFetchingView<int>>();
            using var section = new Section(view);
            UniTask pending = section.Fetch();
            section.Reset();
            view.ClearReceivedCalls();

            // Act
            section.Pending.TrySetResult(40);
            pending.GetAwaiter().GetResult();

            // Assert
            Assert.That(section.Items, Is.Empty);
            view.DidNotReceive().RefreshGrid(Arg.Any<SectionFetchData<int>>(), Arg.Any<bool>());
        }

        [Test]
        public void RetryAFailedPageWithoutDroppingEarlierResults()
        {
            // Arrange
            var view = Substitute.For<ICommunityFetchingView<int>>();
            using var section = new Section(view);
            view.NewDataRequested += Raise.Event<Action>();
            section.Pending.TrySetResult(40);

            // Act
            view.NewDataRequested += Raise.Event<Action>();
            section.Pending.TrySetResult(null);
            view.NewDataRequested += Raise.Event<Action>();
            section.Pending.TrySetResult(40);

            // Assert
            Assert.That(section.RequestedPages, Is.EqualTo(new[] { 1, 2, 2 }));
            Assert.That(section.Items, Is.EqualTo(new[] { 40, 40 }));
        }

        [TestCase(null, false)]
        [TestCase(0, true)]
        public void ShowTheEmptyStateOnlyForACompletedEmptyPage(int? total, bool showEmptyState)
        {
            // Arrange
            var view = Substitute.For<ICommunityFetchingView<int>>();
            using var section = new Section(view);
            UniTask page = section.Fetch();
            view.ClearReceivedCalls();

            // Act
            section.Pending.TrySetResult(total);
            page.GetAwaiter().GetResult();

            // Assert
            view.Received(1).SetEmptyStateActive(showEmptyState);
            view.DidNotReceive().SetEmptyStateActive(!showEmptyState);
        }

        private sealed class Section : CommunityFetchingControllerBase<int, ICommunityFetchingView<int>>
        {
            internal readonly List<int> RequestedPages = new ();
            internal IReadOnlyList<int> Items => currentSectionFetchData.Items;
            internal UniTaskCompletionSource<int?> Pending { get; private set; } = new ();

            protected override SectionFetchData<int> currentSectionFetchData { get; } = new (20);

            internal Section(ICommunityFetchingView<int> view) : base(view, 20) { }

            internal void Visit(CancellationToken token) => cancellationToken = token;

            internal UniTask Fetch() => FetchNewDataAsync(cancellationToken);

            protected override async UniTask<int?> FetchDataAsync(CancellationToken ct)
            {
                RequestedPages.Add(currentSectionFetchData.PageNumber);
                Pending = new UniTaskCompletionSource<int?>();
                int? total = await Pending.Task;
                if (total.HasValue && !ct.IsCancellationRequested)
                    currentSectionFetchData.Items.Add(total.Value);

                return total;
            }
        }
    }
}
