using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyGoToElementShould
    {
        private const string FOCUSED_CLASS = "lobby-goto--focused";
        private const string WITH_DESTINATION_CLASS = "lobby-goto--with-destination";

        private readonly List<string> submitted = new ();

        private GameObject documentGameObject = null!;
        private PanelSettings panelSettings = null!;
        private LobbyGoToElement element = null!;
        private TextField input = null!;

        [SetUp]
        public void SetUp()
        {
            submitted.Clear();

            // Focus takes a panel
            documentGameObject = new GameObject(nameof(LobbyGoToElementShould));
            var document = documentGameObject.AddComponent<UIDocument>();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            document.panelSettings = panelSettings;

            element = new LobbyGoToElement { Submitted = submitted.Add };
            document.rootVisualElement.Add(element);
            input = element.Q<TextField>("Input");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(documentGameObject);
            Object.DestroyImmediate(panelSettings);
        }

        [Test]
        public void ShowTheCommandAndThePlaceholder()
        {
            Assert.AreEqual("/goto", element.Q<Label>("Command").text);
            Assert.AreEqual("e.g. 3,-5 or mana.dcl.eth", input.textEdition.placeholder);
            Assert.IsFalse(input.textEdition.hidePlaceholderOnFocus);
            Assert.AreEqual(PickingMode.Ignore, element.Q("Icon").pickingMode);
        }

        [Test]
        public void SubmitTheTrimmedTextOnReturnAndEmptyTheField()
        {
            //Arrange
            element.Text = "  3,-5 ";

            //Act
            Press(KeyCode.Return);

            //Assert
            Assert.AreEqual(1, submitted.Count);
            Assert.AreEqual("3,-5", submitted[0]);
            Assert.AreEqual(string.Empty, element.Text);
        }

        [Test]
        public void SubmitOnTheKeypadEnterToo()
        {
            //Arrange
            element.Text = "mana.dcl.eth";

            //Act
            Press(KeyCode.KeypadEnter);

            //Assert
            Assert.AreEqual(1, submitted.Count);
            Assert.AreEqual("mana.dcl.eth", submitted[0]);
        }

        [TestCase("   ")]
        [TestCase("mana")]
        [TestCase("3")]
        public void SubmitNothingWhileTheFieldHoldsNoDestination(string text)
        {
            //Arrange
            element.Text = text;

            //Act
            Press(KeyCode.Return);

            //Assert
            Assert.AreEqual(0, submitted.Count);
            Assert.AreEqual(text, element.Text);
        }

        [Test]
        public void IgnoreTheOtherKeys()
        {
            //Arrange
            element.Text = "3,-5";

            //Act
            Press(KeyCode.A);
            Press(KeyCode.Space);

            //Assert
            Assert.AreEqual(0, submitted.Count);
            Assert.AreEqual("3,-5", element.Text);
        }

        [TestCase("3,-5", true)]
        [TestCase(" 3,-5/spawn ", true)]
        [TestCase("mana.dcl.eth", true)]
        [TestCase("your.world.eth/3,-5/spawn", true)]
        [TestCase("mana", false)]
        [TestCase("mana/3,-5", false)]
        [TestCase("3", false)]
        [TestCase("random", false)]
        [TestCase("   ", false)]
        public void ShowTheGoButtonWhileTheFieldHoldsADestination(string text, bool expected)
        {
            //Act
            element.Text = text;

            //Assert
            Assert.AreEqual(expected, element.ClassListContains(WITH_DESTINATION_CLASS));
        }

        [Test]
        public void SubmitTheTrimmedTextOnTheGoButtonAndEmptyTheField()
        {
            //Arrange
            element.Text = " mana.dcl.eth ";

            //Act
            Click();

            //Assert
            Assert.AreEqual(1, submitted.Count);
            Assert.AreEqual("mana.dcl.eth", submitted[0]);
            Assert.AreEqual(string.Empty, element.Text);
            Assert.IsFalse(element.ClassListContains(WITH_DESTINATION_CLASS));
        }

        [Test]
        public void MarkItselfWhileTheFieldIsFocused()
        {
            //Act
            input.Focus();

            //Assert
            Assert.IsTrue(element.ClassListContains(FOCUSED_CLASS));

            //Act
            input.Blur();

            //Assert
            Assert.IsFalse(element.ClassListContains(FOCUSED_CLASS));
        }

        [Test]
        public void EmptyTheFieldAndDropTheFocusOnReset()
        {
            //Arrange
            element.Text = "3,-5";
            input.Focus();

            //Act
            element.Reset();

            //Assert
            Assert.AreEqual(string.Empty, element.Text);
            Assert.IsFalse(element.ClassListContains(WITH_DESTINATION_CLASS));
            Assert.IsFalse(element.ClassListContains(FOCUSED_CLASS));
            Assert.AreEqual(0, submitted.Count);
        }

        private void Press(KeyCode keyCode)
        {
            using KeyDownEvent evt = KeyDownEvent.GetPooled('\0', keyCode, EventModifiers.None);
            evt.target = input;
            input.SendEvent(evt);
        }

        // A submit is what a Button turns into a click without a laid-out pointer target
        private void Click()
        {
            Button go = element.Q<Button>("Go");
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
            evt.target = go;
            go.SendEvent(evt);
        }
    }
}
