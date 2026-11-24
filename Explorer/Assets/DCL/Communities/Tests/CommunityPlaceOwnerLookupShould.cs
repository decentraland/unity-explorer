using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesCard.Places;
using DCL.Communities.CommunityCreation;
using DCL.Input;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using CommunityData = DCL.Communities.CommunitiesDataProvider.DTOs.GetCommunityResponse.CommunityData;
using Object = UnityEngine.Object;

namespace DCL.Communities.Tests
{
    public class CommunityPlaceOwnerLookupShould
    {
        private const string OWNER = "0x1111111111111111111111111111111111111111";
        private const string WIZARD = "Assets/DCL/Communities/CommunityCreation/Prefabs/CommunityCreationWizard.prefab";
        private const string PLACES = "Assets/DCL/Communities/CommunitiesCard/Prefabs/CommunityCardPlaces.prefab";

        private readonly List<string> warnings = new ();
        private IPlacesAPIService places = null!;
        private IProfileRepository profiles = null!;
        private SelfProfile self = null!;
        private CommunityCreationEditionView wizard = null!;
        private CommunityCreationEditionController creation = null!;

        private UniTask wizardLifetime;

        [SetUp]
        public void SetUp()
        {
            warnings.Clear();
            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.INTERNAL_SERVER_ERROR, RecordWarning);
            places = Substitute.For<IPlacesAPIService>();
            profiles = Substitute.For<IProfileRepository>();
            self = Substitute.For<SelfProfile>();
            self.ProfileAsync(Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(ProfileReadResult.FromOk(Profile.NewRandomProfile(OWNER))));
            OwnedPlaces();
            wizard = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WIZARD)).GetComponent<CommunityCreationEditionView>();
            creation = new CommunityCreationEditionController(() => wizard, null!, Substitute.For<IInputBlock>(), null!, places, self, null!, profiles, null!);
        }

        [TearDown]
        public void TearDown()
        {
            NotificationsBusController.Instance.UnsubscribeFromNotificationTypeReceived(NotificationType.INTERNAL_SERVER_ERROR, RecordWarning);
            wizard.CancelButtonClicked?.Invoke();
            wizardLifetime.GetAwaiter().GetResult();
            creation.Dispose();
            Object.DestroyImmediate(wizard.gameObject);
        }

        [Test]
        public void ShowOnlyTheNameRequirementWhenOpeningCreateDirectly()
        {
            OpenCreate(false);

            Assert.That(NamePromptVisible(), Is.True);
            self.DidNotReceive().ProfileAsync(Arg.Any<CancellationToken>());
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void OpenAnEligibleCreationFormWithNoOwnedPlaces()
        {
            OpenCreate(true);

            Assert.That(NamePromptVisible(), Is.False);
            places.Received(1).GetDestinationsByOwnerAsync(OWNER, Arg.Any<CancellationToken>());
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void IgnoreACommunityPlacesLookupThatFinishesOverTheNamePrompt()
        {
            using var section = new CancellationTokenSource();
            var pending = new UniTaskCompletionSource<ProfileTier?>();
            profiles.GetAsync(OWNER, 0, null, section.Token, true, IProfileRepository.FetchBehaviour.Default, ProfileTier.Kind.Compact)
                    .Returns(pending.Task);
            places.GetDestinationsByIdsAsync(Arg.Any<IEnumerable<string>>(), section.Token)
                  .Returns(Response(new PlacesData.PlaceInfo(Vector2Int.zero) { id = "place", owner = OWNER }));
            PlacesSectionView view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PLACES)).GetComponent<PlacesSectionView>();
            try
            {
                using var card = new PlacesSectionController(view, new ThumbnailLoader(null), null!, places, null!, null!, null!, null!, profiles, null!, null!, null!);
                card.ShowPlaces(new CommunityData { id = "community" }, new[] { "place" }, section.Token);
                profiles.Received(1).GetAsync(OWNER, 0, null, section.Token, true, IProfileRepository.FetchBehaviour.Default, ProfileTier.Kind.Compact);

                section.Cancel();
                card.Reset();
                OpenCreate(false);
                pending.TrySetException(new InvalidOperationException("Profile request failed after closing Places"));

                Assert.That(NamePromptVisible(), Is.True);
                Assert.That(warnings, Is.Empty);
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        [Test]
        public void WarnOnceWhenAnActiveOwnerLookupFails()
        {
            var names = new CommunityPlaceOwnerNames(profiles);
            profiles.GetAsync(OWNER, 0, null, Arg.Any<CancellationToken>(), true, IProfileRepository.FetchBehaviour.Default, ProfileTier.Kind.Compact)
                    .Returns(UniTask.FromException<ProfileTier?>(new InvalidOperationException("Profile service unavailable")));

            names.LoadAsync(new[] { new PlacesData.PlaceInfo(Vector2Int.zero) { owner = OWNER } }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(warnings, Is.EqualTo(new[] { "There was an error getting owners names. Please try again." }));
        }

        [Test]
        public void LeaveTheOwnerNameBlankWhenTheProfileDoesNotExist()
        {
            var names = new CommunityPlaceOwnerNames(profiles);
            profiles.GetAsync(OWNER, 0, null, Arg.Any<CancellationToken>(), true, IProfileRepository.FetchBehaviour.Default, ProfileTier.Kind.Compact)
                    .Returns(UniTask.FromResult<ProfileTier?>(null));

            names.LoadAsync(new[] { new PlacesData.PlaceInfo(Vector2Int.zero) { owner = OWNER } }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(names.GetName(OWNER), Is.Empty);
            Assert.That(warnings, Is.Empty);
        }

        private void OpenCreate(bool eligible) =>
            wizardLifetime = creation.LaunchViewLifeCycleAsync(default, new CommunityCreationEditionParameter(eligible, string.Empty, null), CancellationToken.None);

        private bool NamePromptVisible() =>
            ((GameObject)new SerializedObject(wizard).FindProperty("getNamePanel").objectReferenceValue).activeSelf;

        private void OwnedPlaces(params PlacesData.PlaceInfo[] owned) =>
            places.GetDestinationsByOwnerAsync(OWNER, Arg.Any<CancellationToken>()).Returns(Response(owned));

        private static UniTask<PlacesData.IPlacesAPIResponse> Response(params PlacesData.PlaceInfo[] data) =>
            UniTask.FromResult<PlacesData.IPlacesAPIResponse>(new PlacesData.PlacesAPIResponse { ok = true, total = data.Length, data = new List<PlacesData.PlaceInfo>(data) });

        private void RecordWarning(INotification notification) =>
            warnings.Add(notification.GetHeader());
    }
}
