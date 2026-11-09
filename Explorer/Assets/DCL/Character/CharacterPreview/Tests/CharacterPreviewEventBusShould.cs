using Arch.Core;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace DCL.CharacterPreview.Tests
{
    [TestFixture]
    public class CharacterPreviewEventBusShould
    {
        private readonly List<Object> created = new ();

        private CharacterPreviewEventBus bus = null!;
        private World world = null!;

        [SetUp]
        public void SetUp()
        {
            bus = new CharacterPreviewEventBus();
            world = World.Create();
        }

        [TearDown]
        public void TearDown()
        {
            World.Destroy(world);

            foreach (Object obj in created)
                Object.DestroyImmediate(obj);

            created.Clear();
        }

        [Test]
        public void KeepTheLastShownOnTop()
        {
            // Arrange
            CharacterPreviewControllerBase first = CreatePreview();
            CharacterPreviewControllerBase second = CreatePreview();

            // Act
            first.OnShow();
            second.OnShow();

            // Assert
            Assert.AreSame(second, bus.Top);
        }

        [Test]
        public void FallBackToThePreviousOneWhenTheTopHides()
        {
            // Arrange
            CharacterPreviewControllerBase first = CreatePreview();
            CharacterPreviewControllerBase second = CreatePreview();
            CharacterPreviewControllerBase third = CreatePreview();
            first.OnShow();
            second.OnShow();
            third.OnShow();

            // Act & Assert
            third.OnHide();
            Assert.AreSame(second, bus.Top);

            second.OnHide();
            Assert.AreSame(first, bus.Top);

            first.OnHide();
            Assert.IsNull(bus.Top);
        }

        [Test]
        public void KeepTheTopWhenALowerOneHides()
        {
            // Arrange
            CharacterPreviewControllerBase first = CreatePreview();
            CharacterPreviewControllerBase second = CreatePreview();
            first.OnShow();
            second.OnShow();

            // Act
            first.OnHide();

            // Assert
            Assert.AreSame(second, bus.Top);
        }

        [Test]
        public void MoveAReshownOneToTop()
        {
            // Arrange
            CharacterPreviewControllerBase first = CreatePreview();
            CharacterPreviewControllerBase second = CreatePreview();
            first.OnShow();
            second.OnShow();

            // Act
            first.OnShow();

            // Assert
            Assert.AreSame(first, bus.Top);
            second.OnHide();
            Assert.AreSame(first, bus.Top);
        }

        [Test]
        public void ForgetADisposedOne()
        {
            // Arrange
            CharacterPreviewControllerBase first = CreatePreview();
            CharacterPreviewControllerBase second = CreatePreview();
            first.OnShow();
            second.OnShow();

            // Act & Assert
            first.Dispose();
            Assert.AreSame(second, bus.Top);

            second.Dispose();
            Assert.IsNull(bus.Top);
        }

        [Test]
        public void RaiseShowForSubscribers()
        {
            // Arrange
            CharacterPreviewControllerBase preview = CreatePreview();
            CharacterPreviewControllerBase? shown = null;
            bus.OnAnyCharacterPreviewShowEvent += p => shown = p;

            // Act
            preview.OnShow();

            // Assert
            Assert.AreSame(preview, shown);
        }

        [Test]
        public void RestoreOnlyTheNewTopWhenTheTopHides()
        {
            // Arrange
            CharacterPreviewControllerBase first = CreatePreview();
            CharacterPreviewControllerBase second = CreatePreview();
            CharacterPreviewControllerBase third = CreatePreview();
            first.OnShow();
            second.OnShow();
            third.OnShow();
            var restored = new List<CharacterPreviewControllerBase>();
            bus.OnCharacterPreviewRestoredEvent += restored.Add;

            // Act
            third.OnHide();

            // Assert
            CollectionAssert.AreEqual(new[] { second }, restored);
        }

        [Test]
        public void RestoreNothingWhenALowerOneHides()
        {
            // Arrange
            CharacterPreviewControllerBase first = CreatePreview();
            CharacterPreviewControllerBase second = CreatePreview();
            first.OnShow();
            second.OnShow();
            var restored = new List<CharacterPreviewControllerBase>();
            bus.OnCharacterPreviewRestoredEvent += restored.Add;

            // Act
            first.OnHide();

            // Assert
            CollectionAssert.IsEmpty(restored);
        }

        [Test]
        public void RestoreNothingWhenTheLastOneHides()
        {
            // Arrange
            CharacterPreviewControllerBase preview = CreatePreview();
            preview.OnShow();
            var restored = new List<CharacterPreviewControllerBase>();
            bus.OnCharacterPreviewRestoredEvent += restored.Add;

            // Act
            preview.OnHide();

            // Assert
            CollectionAssert.IsEmpty(restored);
        }

        [Test]
        public void RaiseAnyShownOnlyOnEmptyTransitions()
        {
            // Arrange
            CharacterPreviewControllerBase first = CreatePreview();
            CharacterPreviewControllerBase second = CreatePreview();
            var changes = new List<bool>();
            bus.OnAnyShownChangedEvent += changes.Add;

            // Act & Assert
            first.OnShow();
            second.OnShow();
            first.OnShow();
            second.OnHide();
            CollectionAssert.AreEqual(new[] { true }, changes);

            first.OnHide();
            CollectionAssert.AreEqual(new[] { true, false }, changes);
        }

        [Test]
        public void RaiseAnyShownFalseWhenTheLastOneIsDisposed()
        {
            // Arrange
            CharacterPreviewControllerBase preview = CreatePreview();
            preview.OnShow();
            var changes = new List<bool>();
            bus.OnAnyShownChangedEvent += changes.Add;

            // Act
            preview.Dispose();

            // Assert
            CollectionAssert.AreEqual(new[] { false }, changes);
            Assert.IsFalse(bus.AnyShown);
        }

        [Test]
        public void RaiseNothingForAHideWithoutShow()
        {
            // Arrange
            CharacterPreviewControllerBase preview = CreatePreview();
            var changes = new List<bool>();
            bus.OnAnyShownChangedEvent += changes.Add;

            // Act
            preview.OnHide();
            preview.OnHide();

            // Assert
            CollectionAssert.IsEmpty(changes);
        }

        private CharacterPreviewControllerBase CreatePreview() =>
            new CharacterPreviewTestViews.TestPreview(CharacterPreviewTestViews.Create(created), Substitute.For<ICharacterPreviewFactory>(), world, bus);
    }
}
