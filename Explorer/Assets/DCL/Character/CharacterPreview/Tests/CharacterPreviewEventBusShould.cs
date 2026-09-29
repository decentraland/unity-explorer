using Arch.Core;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
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
        public void RaiseShowAndHideForSubscribers()
        {
            // Arrange
            CharacterPreviewControllerBase preview = CreatePreview();
            CharacterPreviewControllerBase? shown = null;
            CharacterPreviewControllerBase? hidden = null;
            bus.OnAnyCharacterPreviewShowEvent += p => shown = p;
            bus.OnAnyCharacterPreviewHideEvent += p => hidden = p;

            // Act
            preview.OnShow();
            preview.OnHide();

            // Assert
            Assert.AreSame(preview, shown);
            Assert.AreSame(preview, hidden);
        }

        private CharacterPreviewControllerBase CreatePreview() =>
            new TestPreview(CreateView(), Substitute.For<ICharacterPreviewFactory>(), world, bus);

        private CharacterPreviewView CreateView()
        {
            var viewGo = new GameObject("CharacterPreviewView");
            created.Add(viewGo);
            CharacterPreviewView view = viewGo.AddComponent<CharacterPreviewView>();

            var settings = ScriptableObject.CreateInstance<CharacterPreviewSettingsSO>();
            created.Add(settings);
            SetBackingField(settings, nameof(CharacterPreviewSettingsSO.cursorSettings), Array.Empty<CharacterPreviewInputCursorSetting>());

            SetBackingField(view, nameof(CharacterPreviewView.CharacterPreviewInputDetector), viewGo.AddComponent<CharacterPreviewInputDetector>());
            SetBackingField(view, nameof(CharacterPreviewView.CharacterPreviewCursorContainer), viewGo.AddComponent<CharacterPreviewCursorContainer>());
            SetBackingField(view, nameof(CharacterPreviewView.CharacterPreviewSettingsSo), settings);

            var rawImageGo = new GameObject("RawImage", typeof(RectTransform));
            rawImageGo.transform.SetParent(viewGo.transform);
            SetBackingField(view, nameof(CharacterPreviewView.RawImage), rawImageGo.AddComponent<RawImage>());

            var spinnerGo = new GameObject("Spinner");
            spinnerGo.transform.SetParent(viewGo.transform);
            SetBackingField(view, nameof(CharacterPreviewView.Spinner), spinnerGo);

            return view;
        }

        private static void SetBackingField(object target, string propertyName, object value)
        {
            // Private fields are only reachable through the type that declares them, so base classes are walked explicitly.
            string fieldName = $"<{propertyName}>k__BackingField";

            for (Type? type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo? field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null) continue;

                field.SetValue(target, value);
                return;
            }

            throw new MissingFieldException(target.GetType().Name, fieldName);
        }

        private class TestPreview : CharacterPreviewControllerBase
        {
            public TestPreview(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, World world, CharacterPreviewEventBus characterPreviewEventBus)
                : base(view, previewFactory, world, false, characterPreviewEventBus) { }
        }
    }
}
