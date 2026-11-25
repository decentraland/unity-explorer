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
            section.Pending.TrySetCanceled(firstVisit.Token);

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

            firstVisit.Cancel();
            section.Visit(CancellationToken.None);
            UniTask replacement = section.Fetch();
            previous.TrySetResult(0);
            view.DidNotReceive().RefreshGrid(Arg.Any<SectionFetchData<int>>(), Arg.Any<bool>());

            view.NewDataRequested += Raise.Event<Action>();
            Assert.That(section.RequestedPages, Is.EqualTo(new[] { 1, 1 }));
            section.Pending.TrySetResult(40);
            replacement.GetAwaiter().GetResult();
            view.Received(1).RefreshGrid(Arg.Any<SectionFetchData<int>>(), false);
        }

        private sealed class Section : CommunityFetchingControllerBase<int, ICommunityFetchingView<int>>
        {
            internal readonly List<int> RequestedPages = new ();
            internal UniTaskCompletionSource<int> Pending { get; private set; } = null!;

            protected override SectionFetchData<int> currentSectionFetchData { get; } = new (20);

            internal Section(ICommunityFetchingView<int> view) : base(view, 20) { }

            internal void Visit(CancellationToken token) => cancellationToken = token;

            internal UniTask Fetch() => FetchNewDataAsync(cancellationToken);

            protected override UniTask<int> FetchDataAsync(CancellationToken ct)
            {
                RequestedPages.Add(currentSectionFetchData.PageNumber);
                Pending = new UniTaskCompletionSource<int>();
                return Pending.Task;
            }
        }
    }
}
