using Cysharp.Threading.Tasks;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.Cart;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.TestTools;

namespace DCL.Shop.Tests
{
    public class ShopFavoritesShould
    {
        private const string COLLECTION = "0x2222222222222222222222222222222222222222";
        private const string KEY_1 = COLLECTION + "-1";
        private const string KEY_2 = COLLECTION + "-2";

        private MarketplaceShopAPIClient api = null!;
        private IWeb3IdentityCache identityCache = null!;
        private ShopFavorites favorites = null!;

        [SetUp]
        public void SetUp()
        {
            api = Substitute.For<MarketplaceShopAPIClient>(null, null);
            api.GetFavoriteIdsAsync(Arg.Any<CancellationToken>()).Returns(_ => UniTask.FromResult(new List<string>()));
            api.SetFavoriteAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(UniTask.CompletedTask);
            identityCache = Substitute.For<IWeb3IdentityCache>();
            favorites = new ShopFavorites(api, identityCache);
        }

        [TearDown]
        public void TearDown() =>
            favorites.Dispose();

        private static ShopItemCardModel Item(string? itemId) =>
            ShopItemCardModel.FromCatalogItem(new CatalogItemDto { id = $"{COLLECTION}-{itemId}", name = "Item", contractAddress = COLLECTION.ToUpperInvariant().Replace("0X", "0x"), category = "wearable", itemId = itemId, chainId = 137 });

        [Test]
        public void KeyAnItemByItsLowercasedContractAndItemId()
        {
            // Act
            string? key = ShopFavorites.KeyOf(Item("1"));
            string? tokenRowKey = ShopFavorites.KeyOf(Item(null));

            // Assert
            Assert.AreEqual(KEY_1, key);
            Assert.IsNull(tokenRowKey);
        }

        [Test]
        public void LoadTheIdsInTheServerOrder()
        {
            // Arrange
            api.GetFavoriteIdsAsync(Arg.Any<CancellationToken>()).Returns(_ => UniTask.FromResult(new List<string> { KEY_2, KEY_1 }));
            bool? loaded = null;
            favorites.Loaded += success => loaded = success;

            // Act
            favorites.Reload();

            // Assert
            Assert.AreEqual(true, loaded);
            Assert.AreEqual(new[] { KEY_2, KEY_1 }, favorites.Ids);
            Assert.IsTrue(favorites.IsFavorite(KEY_1));
        }

        [Test]
        public void ApplyAToggleAtOnceAndSendIt()
        {
            // Arrange
            ShopItemCardModel item = Item("1");
            var changes = new List<bool>();
            favorites.Changed += (_, _, isFavorite) => changes.Add(isFavorite);

            // Act
            favorites.Toggle(item, ShopCartSource.Grid);
            bool afterAdd = favorites.IsFavorite(KEY_1);
            favorites.Toggle(item, ShopCartSource.Grid);

            // Assert
            Assert.IsTrue(afterAdd);
            Assert.IsFalse(favorites.IsFavorite(KEY_1));
            Assert.AreEqual(new[] { true, false }, changes);

            Received.InOrder(() =>
            {
                _ = api.SetFavoriteAsync(KEY_1, true, Arg.Any<CancellationToken>());
                _ = api.SetFavoriteAsync(KEY_1, false, Arg.Any<CancellationToken>());
            });
        }

        [Test]
        public void IgnoreAnItemWithoutAKey()
        {
            // Act
            favorites.Toggle(Item(null), ShopCartSource.Grid);

            // Assert
            Assert.AreEqual(0, favorites.Count);
            _ = api.DidNotReceive().SetFavoriteAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void SendOneFollowUpForAToggleMadeWhileTheFirstIsInFlight()
        {
            // Arrange
            var gate = new UniTaskCompletionSource();
            api.SetFavoriteAsync(KEY_1, true, Arg.Any<CancellationToken>()).Returns(gate.Task);
            ShopItemCardModel item = Item("1");

            // Act
            favorites.Toggle(item, ShopCartSource.Grid);
            favorites.Toggle(item, ShopCartSource.Grid);
            _ = api.DidNotReceive().SetFavoriteAsync(KEY_1, false, Arg.Any<CancellationToken>());
            gate.TrySetResult();

            // Assert
            Assert.IsFalse(favorites.IsFavorite(KEY_1));
            _ = api.Received(1).SetFavoriteAsync(KEY_1, true, Arg.Any<CancellationToken>());
            _ = api.Received(1).SetFavoriteAsync(KEY_1, false, Arg.Any<CancellationToken>());
        }

        [Test]
        public void ReadTheListAgainWhenAToggleIsRejected()
        {
            // Arrange
            LogAssert.ignoreFailingMessages = true;
            api.SetFavoriteAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_ => UniTask.FromException(new InvalidOperationException("rejected")));
            api.GetFavoriteIdsAsync(Arg.Any<CancellationToken>()).Returns(_ => UniTask.FromResult(new List<string> { KEY_2 }));
            var failures = 0;
            favorites.ToggleFailed += () => failures++;

            // Act
            favorites.Toggle(Item("1"), ShopCartSource.Grid);

            // Assert
            Assert.AreEqual(1, failures);
            Assert.IsFalse(favorites.IsFavorite(KEY_1));
            Assert.AreEqual(new[] { KEY_2 }, favorites.Ids);
        }

        [Test]
        public void KeepAToggleMadeWhileTheListWasLoading()
        {
            // Arrange
            var gate = new UniTaskCompletionSource<List<string>>();
            api.GetFavoriteIdsAsync(Arg.Any<CancellationToken>()).Returns(gate.Task);

            // Act
            favorites.Reload();
            favorites.Toggle(Item("1"), ShopCartSource.Grid);
            gate.TrySetResult(new List<string> { KEY_2 });

            // Assert
            Assert.AreEqual(new[] { KEY_1, KEY_2 }, favorites.Ids);
        }

        [Test]
        public void ForgetTheListWhenTheAccountChanges()
        {
            // Arrange
            favorites.Toggle(Item("1"), ShopCartSource.Grid);

            // Act
            identityCache.OnIdentityChanged += Raise.Event<Action>();

            // Assert
            Assert.AreEqual(0, favorites.Count);
            Assert.IsFalse(favorites.IsFavorite(KEY_1));
        }
    }
}
