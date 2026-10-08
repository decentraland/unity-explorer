using DCL.Utilities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.UI.ProfileElements.Tests
{
    [TestFixture]
    public class ProfileWidgetElementShould
    {
        private ProfileWidgetElement widget = null!;
        private ReactiveProperty<ProfileThumbnailViewModel> thumbnail = null!;
        private Sprite sprite = null!;

        [SetUp]
        public void SetUp()
        {
            widget = new ProfileWidgetElement();
            thumbnail = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.Default());
            sprite = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(sprite.texture);
            Object.DestroyImmediate(sprite);
        }

        [Test]
        public void HideTheAddressWhileEmpty()
        {
            //Act
            widget.Name = "Amy";
            widget.Address = string.Empty;

            //Assert
            Assert.AreEqual("Amy", widget.Q<Label>("Name").text);
            Assert.IsTrue(widget.Q<Label>("Address").ClassListContains("profile-widget__address--empty"));

            //Act
            widget.Address = "#1a2b";

            //Assert
            Assert.AreEqual("#1a2b", widget.Q<Label>("Address").text);
            Assert.IsFalse(widget.Q<Label>("Address").ClassListContains("profile-widget__address--empty"));
        }

        [Test]
        public void BindTheThumbnailAndStartItsLoading()
        {
            //Act
            widget.BindThumbnail(thumbnail);

            //Assert
            Assert.AreEqual(ProfileThumbnailViewModel.State.Loading, thumbnail.Value.ThumbnailState);
            Assert.IsTrue(widget.IsLoading);
        }

        [Test]
        public void DrawThePictureOnceLoaded()
        {
            //Arrange
            widget.BindThumbnail(thumbnail);
            thumbnail.SetLoading(Color.red);

            //Act
            thumbnail.SetLoaded(sprite, fromCache: true);

            //Assert
            Assert.IsFalse(widget.IsLoading);
            Assert.AreEqual(sprite.texture, widget.Q("Picture").style.backgroundImage.value.texture, "A full-rect sprite is drawn as its texture so the cover crops it");
            Assert.AreEqual(Color.red, widget.Q("Picture").style.backgroundColor.value);
            Assert.AreEqual(Color.Lerp(Color.red, Color.white, 0.2f), widget.Q("Picture").style.borderTopColor.value);
        }

        [Test]
        public void KeepThePreviousPictureWhileTheNextOneLoads()
        {
            //Arrange
            widget.BindThumbnail(thumbnail);
            thumbnail.SetLoaded(sprite, fromCache: true);

            //Act
            thumbnail.UpdateValue(new ProfileThumbnailViewModel(ProfileThumbnailViewModel.State.Loading, sprite));

            //Assert
            Assert.IsFalse(widget.IsLoading);
            Assert.AreEqual(sprite.texture, widget.Q("Picture").style.backgroundImage.value.texture);
        }

        [Test]
        public void FallBackToTheDefaultPictureOnError()
        {
            //Arrange
            widget.BindThumbnail(thumbnail);
            thumbnail.SetLoaded(sprite, fromCache: true);

            //Act
            thumbnail.UpdateValue(ProfileThumbnailViewModel.Error(Color.blue));

            //Assert
            Assert.IsFalse(widget.IsLoading);
            Assert.AreEqual(StyleKeyword.Null, widget.Q("Picture").style.backgroundImage.keyword);
            Assert.AreEqual(Color.blue, widget.Q("Picture").style.backgroundColor.value);
        }
    }
}
