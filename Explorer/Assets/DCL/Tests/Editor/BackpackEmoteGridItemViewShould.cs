using DCL.AvatarRendering.Thumbnails.Utils;
using DCL.Backpack.EmotesSection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Tests.Editor
{
    [TestFixture]
    public class BackpackEmoteGridItemViewShould
    {
        private BackpackEmoteGridItemView item = null!;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DCL/Backpack/Assets/BackpackEmoteGridItem.prefab");
            item = Object.Instantiate(prefab).GetComponent<BackpackEmoteGridItemView>();
        }

        [TearDown]
        public void TearDown() =>
            Object.DestroyImmediate(item.gameObject);

        [Test]
        public void ShowASelectableNamedCardBeforeItsThumbnailArrives()
        {
            item.FullBackpackItem.SetActive(false);
            item.ItemId = "urn:decentraland:off-chain:base-avatars:clap";
            string? selected = null;
            item.OnSelectItem += (_, id) => selected = id;

            item.ShowMetadata("Clap");
            item.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left, clickCount = 1 });

            Assert.That(item.FullBackpackItem.activeSelf, Is.True);
            Assert.That(item.LoadingView.gameObject.activeSelf, Is.False);
            Assert.That(item.ThumbnailName.gameObject.activeInHierarchy, Is.True);
            Assert.That(item.ThumbnailName.text, Is.EqualTo("Clap"));
            Assert.That(item.ThumbnailName.raycastTarget, Is.False);
            Assert.That(item.WearableThumbnail.enabled, Is.False);
            Assert.That(selected, Is.EqualTo(item.ItemId));
        }

        [Test]
        public void RestoreTheNameWhenAReusedCardHasNoArtwork()
        {
            Sprite sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);

            try
            {
                item.ShowMetadata("Clap");
                item.SetThumbnail(sprite);
                Assert.That(item.WearableThumbnail.sprite, Is.EqualTo(sprite));
                Assert.That(item.WearableThumbnail.enabled, Is.True);
                Assert.That(item.ThumbnailName.gameObject.activeSelf, Is.False);

                item.ShowMetadata("Wave");
                Assert.That(item.WearableThumbnail.sprite, Is.Null);
                item.SetThumbnail(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite);
                Assert.That(item.WearableThumbnail.enabled, Is.False);
                Assert.That(item.ThumbnailName.gameObject.activeInHierarchy, Is.True);
                Assert.That(item.ThumbnailName.text, Is.EqualTo("Wave"));
            }
            finally { Object.DestroyImmediate(sprite); }
        }
    }
}
