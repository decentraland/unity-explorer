using DCL.MarketplaceCredits.Purchase.Cart;
using DCL.Web3;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    public class ShopCartShould
    {
        private const string BUYER = "0x99995f38fc9d786eab5c3a1b1c4e6ae5f4e99999";
        private const string OTHER_BUYER = "0x11115f38fc9d786eab5c3a1b1c4e6ae5f4e11111";
        private const string COLLECTION = "0x2222222222222222222222222222222222222222";

        private IWeb3IdentityCache identityCache = null!;
        private IWeb3Identity identity = null!;
        private ShopCart cart = null!;

        [SetUp]
        public void SetUp()
        {
            identityCache = Substitute.For<IWeb3IdentityCache>();
            identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address(BUYER));
            identityCache.Identity.Returns(identity);
            cart = new ShopCart(identityCache);
        }

        [TearDown]
        public void TearDown() =>
            cart.Dispose();

        private static ShopListingDto Primary(string itemId, int priceCredits = 10, int available = 3) =>
            new ()
            {
                tradeId = "trade-" + itemId,
                listingType = "primary",
                contractAddress = COLLECTION,
                itemId = itemId,
                priceCredits = priceCredits,
                available = available,
            };

        private static ShopListingDto Secondary(string itemId, string tokenId, int priceCredits = 7) =>
            new ()
            {
                tradeId = "trade-token-" + tokenId,
                listingType = "secondary",
                contractAddress = COLLECTION.ToUpperInvariant().Replace("0X", "0x"),
                itemId = itemId,
                tokenId = tokenId,
                priceCredits = priceCredits,
            };

        [Test]
        public void KeyLinesByContractAndItemOrToken()
        {
            // Act
            cart.Add(Primary("3"), ShopCartSource.Grid);
            cart.Add(Secondary("3", "105"), ShopCartSource.Grid);

            // Assert
            Assert.AreEqual(2, cart.Count);
            Assert.AreEqual($"{COLLECTION}-3", cart.Lines[0].Id);
            Assert.AreEqual($"{COLLECTION}-t105", cart.Lines[1].Id);
            Assert.IsTrue(cart.Contains(COLLECTION, "3"));
            Assert.IsTrue(cart.Contains(Secondary("3", "105")));
        }

        [Test]
        public void BumpAPrimaryLineUpToItsStockAndReportEachUnit()
        {
            // Arrange
            var added = 0;
            cart.ItemAdded += (_, _) => added++;

            // Act
            bool first = cart.Add(Primary("3", available: 2), ShopCartSource.Grid);
            bool second = cart.Add(Primary("3", available: 2), ShopCartSource.Trending);
            bool third = cart.Add(Primary("3", available: 2), ShopCartSource.Grid);

            // Assert
            Assert.IsTrue(first);
            Assert.IsTrue(second);
            Assert.IsFalse(third);
            Assert.AreEqual(1, cart.Count);
            Assert.AreEqual(2, cart.Lines[0].Quantity);
            Assert.AreEqual(ShopCartSource.Grid, cart.Lines[0].Source, "the first touch keeps its provenance");
            Assert.AreEqual(2, cart.TotalUnits);
            Assert.AreEqual(20, cart.TotalCredits);
            Assert.AreEqual(2, added);
        }

        [Test]
        public void IgnoreADuplicateSecondaryLine()
        {
            // Act
            cart.Add(Secondary("3", "105"), ShopCartSource.Grid);
            bool again = cart.Add(Secondary("3", "105"), ShopCartSource.Grid);
            bool bumped = cart.Increment(cart.Lines[0].Id);

            // Assert
            Assert.IsFalse(again);
            Assert.IsFalse(bumped);
            Assert.AreEqual(1, cart.Lines[0].Quantity);
        }

        [Test]
        public void ClampQuantityBetweenOneAndStock()
        {
            // Arrange
            cart.Add(Primary("3", available: 4), ShopCartSource.Grid);
            string id = cart.Lines[0].Id;

            // Act
            bool decrementedAtFloor = cart.Decrement(id);
            bool set = cart.SetQuantity(id, 99);

            // Assert
            Assert.IsFalse(decrementedAtFloor);
            Assert.IsTrue(set);
            Assert.AreEqual(4, cart.Lines[0].Quantity);
            Assert.AreEqual(40, cart.TotalCredits);
        }

        [Test]
        public void RemoveOnlyTheListedLinesAndReportThem()
        {
            // Arrange
            cart.Add(Primary("1"), ShopCartSource.Grid);
            cart.Add(Primary("2"), ShopCartSource.Grid);
            cart.Add(Primary("3"), ShopCartSource.Grid);
            var removed = 0;
            var changed = 0;
            cart.ItemRemoved += _ => removed++;
            cart.Changed += () => changed++;

            // Act
            int count = cart.RemoveAll(new[] { $"{COLLECTION}-1", $"{COLLECTION}-3", $"{COLLECTION}-missing" });

            // Assert
            Assert.AreEqual(2, count);
            Assert.AreEqual(2, removed);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(1, cart.Count);
            Assert.AreEqual($"{COLLECTION}-2", cart.Lines[0].Id);
        }

        [Test]
        public void WipeOnlyWhenADifferentAccountSignsIn()
        {
            // Arrange
            cart.Add(Primary("3"), ShopCartSource.Grid);

            // Act: the same account re-signs, then signs out, then another account signs in.
            identityCache.OnIdentityChanged += Raise.Event<Action>();
            int afterSameAccount = cart.Count;

            identityCache.Identity.Returns((IWeb3Identity?)null);
            identityCache.OnIdentityCleared += Raise.Event<Action>();
            int afterSignOut = cart.Count;

            IWeb3Identity other = Substitute.For<IWeb3Identity>();
            other.Address.Returns(new Web3Address(OTHER_BUYER));
            identityCache.Identity.Returns(other);
            identityCache.OnIdentityChanged += Raise.Event<Action>();

            // Assert
            Assert.AreEqual(1, afterSameAccount);
            Assert.AreEqual(1, afterSignOut);
            Assert.AreEqual(0, cart.Count);
        }
    }
}
