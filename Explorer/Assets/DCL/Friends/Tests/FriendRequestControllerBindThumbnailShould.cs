using DCL.Friends.UI.Requests;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.Tests
{
    // Exercises the private BindThumbnail/RestartThumbnailCts helpers via reflection. They hold the
    // dictionary reuse-vs-create and per-view CTS bookkeeping that replaced the removed
    // ProfilePictureView.Setup/SetupAsync calls, and are otherwise only reachable through a live view.
    public class FriendRequestControllerBindThumbnailShould
    {
        private FriendRequestController controller = null!;
        private ProfilePictureView view = null!;
        private MethodInfo bindThumbnailMethod = null!;
        private MethodInfo restartThumbnailCtsMethod = null!;

        [SetUp]
        public void SetUp()
        {
            controller = new FriendRequestController(
                () => null!,
                Substitute.For<IWeb3IdentityCache>(),
                Substitute.For<IFriendsService>(),
                Substitute.For<IProfileRepository>(),
                Substitute.For<IInputBlock>(),
                null!);

            view = CreateBoundProfilePictureView();

            bindThumbnailMethod = typeof(FriendRequestController).GetMethod("BindThumbnail", BindingFlags.NonPublic | BindingFlags.Instance)!;
            restartThumbnailCtsMethod = typeof(FriendRequestController).GetMethod("RestartThumbnailCts", BindingFlags.NonPublic | BindingFlags.Instance)!;
        }

        [TearDown]
        public void TearDown() =>
            controller.Dispose();

        [Test]
        public void CreateAndBindOnFirstThumbnailRequest()
        {
            var property = (ReactiveProperty<ProfileThumbnailViewModel>) bindThumbnailMethod.Invoke(controller, new object[] { view, Color.red })!;

            Assert.AreEqual(ProfileThumbnailViewModel.State.Loading, property.Value.ThumbnailState);
            Assert.AreEqual(Color.red, property.Value.ProfileColor);
        }

        [Test]
        public void ReuseTheSamePropertyAndRestartLoadingOnRebind()
        {
            var first = (ReactiveProperty<ProfileThumbnailViewModel>) bindThumbnailMethod.Invoke(controller, new object[] { view, Color.red })!;

            var second = (ReactiveProperty<ProfileThumbnailViewModel>) bindThumbnailMethod.Invoke(controller, new object[] { view, Color.blue })!;

            Assert.AreSame(first, second, "Rebinding the same ProfilePictureView must reuse its ReactiveProperty, not allocate a new one.");
            Assert.AreEqual(ProfileThumbnailViewModel.State.Loading, second.Value.ThumbnailState);
            Assert.AreEqual(Color.blue, second.Value.ProfileColor);
        }

        [Test]
        public void CreateASeparateCtsPerView()
        {
            ProfilePictureView otherView = CreateBoundProfilePictureView();

            var firstToken = (CancellationToken) restartThumbnailCtsMethod.Invoke(controller, new object[] { view, CancellationToken.None })!;
            var secondToken = (CancellationToken) restartThumbnailCtsMethod.Invoke(controller, new object[] { otherView, CancellationToken.None })!;

            Assert.IsFalse(firstToken.IsCancellationRequested);
            Assert.IsFalse(secondToken.IsCancellationRequested);
            Assert.AreNotEqual(firstToken, secondToken);
        }

        [Test]
        public void CancelThePreviousCtsWhenTheSameViewIsRestarted()
        {
            var firstToken = (CancellationToken) restartThumbnailCtsMethod.Invoke(controller, new object[] { view, CancellationToken.None })!;

            var secondToken = (CancellationToken) restartThumbnailCtsMethod.Invoke(controller, new object[] { view, CancellationToken.None })!;

            Assert.IsTrue(firstToken.IsCancellationRequested, "Restarting a view's CTS must cancel the token handed out for its previous load.");
            Assert.IsFalse(secondToken.IsCancellationRequested);
        }

        [Test]
        public void PropagateCancellationFromTheLinkedParentToken()
        {
            using var parentCts = new CancellationTokenSource();

            var token = (CancellationToken) restartThumbnailCtsMethod.Invoke(controller, new object[] { view, parentCts.Token })!;

            parentCts.Cancel();

            Assert.IsTrue(token.IsCancellationRequested);
        }

        [Test]
        public void CancelAllOutstandingLoadsOnDispose()
        {
            var token = (CancellationToken) restartThumbnailCtsMethod.Invoke(controller, new object[] { view, CancellationToken.None })!;

            controller.Dispose();

            Assert.IsTrue(token.IsCancellationRequested, "Disposing the controller must cancel every view's in-flight thumbnail load.");
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
            SetField(imageView, "<Image>k__BackingField", image);

            ProfilePictureView view = pictureGo.AddComponent<ProfilePictureView>();
            SetField(view, "thumbnailImageView", imageView);
            return view;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                              ?? throw new MissingFieldException(target.GetType().FullName, fieldName);
            field.SetValue(target, value);
        }
    }
}
