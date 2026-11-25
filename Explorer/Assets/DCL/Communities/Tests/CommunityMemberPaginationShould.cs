using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesCard.Members;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using NUnit.Framework;
using System.Threading;
using UnityEditor;
using UnityEngine;

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
