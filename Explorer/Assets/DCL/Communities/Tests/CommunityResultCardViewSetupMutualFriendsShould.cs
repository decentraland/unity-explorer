using DCL.Communities.CommunitiesBrowser;
using DCL.FeatureFlags;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using CommunityData = DCL.Communities.CommunitiesDataProvider.DTOs.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.Tests
{
    // Exercises CommunityResultCardView.SetupMutualFriends: the array-of-structs per-slot thumbnail
    // manager (one ReactiveProperty + one CTS per mutual-friend slot) that replaced the removed
    // ProfilePictureView.Setup call. mutualFriends/MutualFriendsConfig/MutualThumbnail are private/internal,
    // so the slot array is wired up via reflection rather than by changing their visibility.
    public class CommunityResultCardViewSetupMutualFriendsShould
    {
        private CommunityResultCardView view = null!;
        private Type mutualThumbnailType = null!;
        private Array thumbnails = null!;
        private Sprite placeholderSprite = null!;

        [SetUp]
        public void SetUp()
        {
            placeholderSprite = Sprite.Create(new Texture2D(1, 1), new Rect(0, 0, 1, 1), Vector2.zero);

            FeatureFlagsConfiguration.Reset();
            OfficialWalletsHelper.Reset();
            GetProfileThumbnailCommand.Reset();

            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            OfficialWalletsHelper.Initialize(new OfficialWalletsHelper());

            // The bound thumbnail's fetch fires-and-forgets through GetProfileThumbnailCommand.Instance. Serving
            // a cached sprite keeps ExecuteAsync on its synchronous cache-hit path (SetLoaded, no await), so the
            // fire-and-forget completes within SetupMutualFriends itself instead of leaving a pending continuation.
            var thumbnailCache = Substitute.For<ISpriteCache>();
            thumbnailCache.GetCachedSprite(Arg.Any<string>()).Returns(placeholderSprite);

            var profileRepositoryWrapper = new ProfileRepositoryWrapper(
                Substitute.For<IProfileRepository>(),
                Substitute.For<IProfileCache>(),
                thumbnailCache,
                Substitute.For<IWeb3IdentityCache>());

            GetProfileThumbnailCommand.Initialize(new GetProfileThumbnailCommand(profileRepositoryWrapper));
            LogAssert.ignoreFailingMessages = true;

            var go = new GameObject(nameof(CommunityResultCardView));
            go.SetActive(false); // keep Awake() (9 button-listener wirings) from ever running; SetupMutualFriends doesn't need it
            view = go.AddComponent<CommunityResultCardView>();

            // OnDestroy touches these three directly (not just the ones Awake wires), so every test needs them.
            SetField(view, typeof(CommunityResultCardView), "mainButton", CreateButton("MainButton"));
            SetField(view, typeof(CommunityResultCardView), "viewCommunityButton", CreateButton("ViewCommunityButton"));
            SetField(view, typeof(CommunityResultCardView), "joinCommunityButton", CreateButton("JoinCommunityButton"));

            FieldInfo mutualFriendsField = typeof(CommunityResultCardView).GetField("mutualFriends", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Type mutualFriendsConfigType = mutualFriendsField.FieldType;
            FieldInfo thumbnailsField = mutualFriendsConfigType.GetField("thumbnails", BindingFlags.Public | BindingFlags.Instance)!;
            mutualThumbnailType = thumbnailsField.FieldType.GetElementType()!;

            thumbnails = Array.CreateInstance(mutualThumbnailType, 1);
            thumbnails.SetValue(BuildSlot(CreateBoundProfilePictureView(), CreateTooltipView()), 0);

            object config = Activator.CreateInstance(mutualFriendsConfigType)!;
            SetField(config, mutualFriendsConfigType, "thumbnails", thumbnails);
            mutualFriendsField.SetValue(view, config);
        }

        [TearDown]
        public void TearDown()
        {
            if (view != null)
                UnityEngine.Object.DestroyImmediate(view.gameObject);

            LogAssert.ignoreFailingMessages = false;
            GetProfileThumbnailCommand.Reset();
            OfficialWalletsHelper.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        [Test]
        public void CreateAndBindTheSlotThumbnailOnFirstBind()
        {
            view.SetupMutualFriends(null!, CommunityDataWithFriends(CompactInfo("0xaaa", "Ana")));

            object slot = thumbnails.GetValue(0)!;
            var thumbnail = (ReactiveProperty<ProfileThumbnailViewModel>?) GetField(slot, mutualThumbnailType, "thumbnail");
            var cts = (CancellationTokenSource?) GetField(slot, mutualThumbnailType, "thumbnailCts");

            Assert.IsNotNull(thumbnail);
            Assert.IsNotNull(cts);
            Assert.IsFalse(cts!.IsCancellationRequested);
        }

        [Test]
        public void ReuseTheSlotsReactivePropertyAndRestartItsCtsOnRebindWithADifferentFriend()
        {
            view.SetupMutualFriends(null!, CommunityDataWithFriends(CompactInfo("0xaaa", "Ana")));

            object firstSlot = thumbnails.GetValue(0)!;
            var firstThumbnail = (ReactiveProperty<ProfileThumbnailViewModel>?) GetField(firstSlot, mutualThumbnailType, "thumbnail");
            var firstCts = (CancellationTokenSource?) GetField(firstSlot, mutualThumbnailType, "thumbnailCts");

            view.SetupMutualFriends(null!, CommunityDataWithFriends(CompactInfo("0xbbb", "Bob")));

            object secondSlot = thumbnails.GetValue(0)!;
            var secondThumbnail = (ReactiveProperty<ProfileThumbnailViewModel>?) GetField(secondSlot, mutualThumbnailType, "thumbnail");
            var secondCts = (CancellationTokenSource?) GetField(secondSlot, mutualThumbnailType, "thumbnailCts");

            Assert.AreSame(firstThumbnail, secondThumbnail, "Rebinding the slot must reuse its ReactiveProperty, not allocate a new one.");
            Assert.IsTrue(firstCts!.IsCancellationRequested, "Rebinding the slot must cancel the CTS handed out for its previous load.");
            Assert.IsFalse(secondCts!.IsCancellationRequested);
            Assert.AreNotSame(firstCts, secondCts);
        }

        [Test]
        public void CancelTheInFlightLoadWhenTheCardIsDestroyed()
        {
            view.SetupMutualFriends(null!, CommunityDataWithFriends(CompactInfo("0xaaa", "Ana")));

            object slot = thumbnails.GetValue(0)!;
            var cts = (CancellationTokenSource?) GetField(slot, mutualThumbnailType, "thumbnailCts");
            Assert.IsFalse(cts!.IsCancellationRequested);

            UnityEngine.Object.DestroyImmediate(view.gameObject);

            Assert.IsTrue(cts!.IsCancellationRequested, "OnDestroy must cancel every mutual-friend thumbnail's in-flight load.");
        }

        private static CommunityData CommunityDataWithFriends(params Profile.CompactInfo[] friends)
        {
            var data = new CommunityData();
            typeof(CommunityData).GetProperty("Friends")!.SetValue(data, (IReadOnlyList<Profile.CompactInfo>) friends);
            return data;
        }

        private static Profile.CompactInfo CompactInfo(string userId, string name) =>
            new (userId, name);

        private object BuildSlot(ProfilePictureView picture, ProfileNameTooltipView tooltip)
        {
            object slot = Activator.CreateInstance(mutualThumbnailType)!;
            SetField(slot, mutualThumbnailType, "root", new GameObject("MutualSlotRoot"));
            SetField(slot, mutualThumbnailType, "picture", picture);
            SetField(slot, mutualThumbnailType, "profileNameTooltip", tooltip);
            return slot;
        }

        private static Button CreateButton(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            return go.AddComponent<Button>();
        }

        // Builds a real, minimally-wired ProfilePictureView: the GameObject stays inactive throughout so
        // ImageView.Awake() (which dereferences its Image field) never runs before the field is set below.
        private static ProfilePictureView CreateBoundProfilePictureView()
        {
            var pictureGo = new GameObject(nameof(ProfilePictureView));
            pictureGo.SetActive(false);

            var imageGo = new GameObject("Thumbnail", typeof(RectTransform));
            imageGo.SetActive(false);
            imageGo.transform.SetParent(pictureGo.transform, false);

            Image image = imageGo.AddComponent<Image>();
            ImageView imageView = imageGo.AddComponent<ImageView>();
            SetField(imageView, typeof(ImageView), "<Image>k__BackingField", image);

            ProfilePictureView view = pictureGo.AddComponent<ProfilePictureView>();
            SetField(view, typeof(ProfilePictureView), "thumbnailImageView", imageView);
            return view;
        }

        private static ProfileNameTooltipView CreateTooltipView()
        {
            var go = new GameObject(nameof(ProfileNameTooltipView));
            go.SetActive(false);

            var textGo = new GameObject("Name", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            TextMeshProUGUI nameText = textGo.AddComponent<TextMeshProUGUI>();

            ProfileNameTooltipView tooltip = go.AddComponent<ProfileNameTooltipView>();
            SetField(tooltip, typeof(ProfileNameTooltipView), "nameText", nameText);
            SetField(tooltip, typeof(ProfileNameTooltipView), "verifiedMark", new GameObject("Verified"));
            SetField(tooltip, typeof(ProfileNameTooltipView), "officialMark", new GameObject("Official"));
            return tooltip;
        }

        private static void SetField(object target, Type type, string fieldName, object value)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                              ?? throw new MissingFieldException(type.FullName, fieldName);
            field.SetValue(target, value);
        }

        private static object? GetField(object target, Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                              ?? throw new MissingFieldException(type.FullName, fieldName);
            return field.GetValue(target);
        }
    }
}
