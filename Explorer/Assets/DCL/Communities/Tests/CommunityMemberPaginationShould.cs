using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesCard.Members;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using NUnit.Framework;
using System.Collections;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.Communities.Tests
{
    public class CommunityMemberPaginationShould
    {
        private const string MEMBERS = "Assets/DCL/Communities/CommunitiesCard/Prefabs/CommunityCardMembers.prefab";

        [Test]
        public void RetryAnInterruptedFirstPageAndReuseTheCompletedList()
        {
            // Arrange
            using var firstVisit = new CancellationTokenSource();
            using var secondVisit = new CancellationTokenSource();
            var community = new GetCommunityResponse.CommunityData { id = "community" };
            var view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MEMBERS)).GetComponent<MembersListView>();
            try
            {
                using var members = new Members(view);
                members.ShowMembersList(community, firstVisit.Token);
                var previous = members.Pending;

                // Act
                firstVisit.Cancel();
                members.ShowMembersList(community, secondVisit.Token);
                int requestsAfterReentry = members.Requests;
                members.Pending.TrySetResult(0);
                previous.TrySetCanceled(firstVisit.Token);

                secondVisit.Cancel();
                members.ShowMembersList(community, CancellationToken.None);

                // Assert
                Assert.That(requestsAfterReentry, Is.EqualTo(2));
                Assert.That(members.Requests, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        [UnityTest]
        public IEnumerator DiscardAQueuedSubsectionSelectionAfterReset()
        {
            // Arrange
            var community = new GetCommunityResponse.CommunityData { id = "community", role = CommunityMemberRole.moderator };
            var view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MEMBERS)).GetComponent<MembersListView>();
            try
            {
                using var members = new Members(view);
                members.ShowMembersList(community, CancellationToken.None);
                var pending = members.Pending;
                var field = typeof(MembersListView).GetField(nameof(MembersListView.ActiveSectionChanged), BindingFlags.Instance | BindingFlags.NonPublic);
                var selectSection = field?.GetValue(view) as System.Action<MembersListView.MemberListSections>
                                    ?? throw new AssertionException("The controller must subscribe to subsection changes.");
                selectSection(MembersListView.MemberListSections.Banned);

                // Act
                members.Reset();
                pending.TrySetResult(0);
                yield return null;
                yield return null;

                // Assert
                Assert.That(members.Requests, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        private sealed class Members : MembersListController
        {
            internal int Requests { get; private set; }
            internal UniTaskCompletionSource<int?> Pending { get; private set; } = new ();

            // The view stores these services. This public community uses only the overridden page fetch.
            internal Members(MembersListView view) : base(view, null!, null!, null, null!, null!, null!, null!, null!, null!) { }

            protected override UniTask<int?> FetchDataAsync(CancellationToken ct)
            {
                Requests++;
                Pending = new UniTaskCompletionSource<int?>();
                return Pending.Task;
            }
        }
    }
}
