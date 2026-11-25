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
            using var firstVisit = new CancellationTokenSource();
            var view = Substitute.For<ICommunityFetchingView<int>>();
            using var section = new Section(view);
            section.Visit(firstVisit.Token);
            view.NewDataRequested += Raise.Event<Action>();
            section.Pending.TrySetResult(40);

            view.NewDataRequested += Raise.Event<Action>();
            firstVisit.Cancel();
            section.Visit(CancellationToken.None);
            view.ClearReceivedCalls();
            section.Pending.TrySetCanceled(firstVisit.Token);
            view.Received(1).SetLoadingMoreBadgeActive(false);

            view.NewDataRequested += Raise.Event<Action>();
            Assert.That(section.RequestedPages, Is.EqualTo(new[] { 1, 2, 2 }));
            section.Pending.TrySetResult(40);
        }

        [Test]
        public void KeepTheReplacementFetchBusyWhenThePreviousFetchFinishes()
        {
            using var firstVisit = new CancellationTokenSource();
            var view = Substitute.For<ICommunityFetchingView<int>>();
            using var section = new Section(view);
            section.Visit(firstVisit.Token);
            view.NewDataRequested += Raise.Event<Action>();
            UniTaskCompletionSource<int> previous = section.Pending;

            UniTask replacement = section.Fetch();
            view.ClearReceivedCalls();
            previous.TrySetResult(0);
            view.DidNotReceive().SetLoadingStateActive(false);
            Assert.That(section.Items, Is.Empty);
            view.DidNotReceive().RefreshGrid(Arg.Any<SectionFetchData<int>>(), Arg.Any<bool>());

            view.NewDataRequested += Raise.Event<Action>();
            Assert.That(section.RequestedPages, Is.EqualTo(new[] { 1, 1 }));
            section.Pending.TrySetResult(40);
            replacement.GetAwaiter().GetResult();
            view.Received(1).RefreshGrid(Arg.Any<SectionFetchData<int>>(), false);
        }

        [Test]
        public void IgnoreAPageThatFinishesAfterResetWithALiveVisitToken()
        {
            var view = Substitute.For<ICommunityFetchingView<int>>();
            using var section = new Section(view);
            UniTask pending = section.Fetch();
            section.Reset();
            view.ClearReceivedCalls();

            section.Pending.TrySetResult(40);
            pending.GetAwaiter().GetResult();

            Assert.That(section.Items, Is.Empty);
            view.DidNotReceive().RefreshGrid(Arg.Any<SectionFetchData<int>>(), Arg.Any<bool>());
        }

        private sealed class Section : CommunityFetchingControllerBase<int, ICommunityFetchingView<int>>
        {
            internal readonly List<int> RequestedPages = new ();
            internal IReadOnlyList<int> Items => currentSectionFetchData.Items;
            internal UniTaskCompletionSource<int> Pending { get; private set; } = new ();

            protected override SectionFetchData<int> currentSectionFetchData { get; } = new (20);

            internal Section(ICommunityFetchingView<int> view) : base(view, 20) { }

            internal void Visit(CancellationToken token) => cancellationToken = token;

            internal UniTask Fetch() => FetchNewDataAsync(cancellationToken);

            protected override async UniTask<int> FetchDataAsync(CancellationToken ct)
            {
                RequestedPages.Add(currentSectionFetchData.PageNumber);
                Pending = new UniTaskCompletionSource<int>();
                int total = await Pending.Task;
                if (!ct.IsCancellationRequested)
                    currentSectionFetchData.Items.Add(total);

                return total;
            }
        }
    }
}
