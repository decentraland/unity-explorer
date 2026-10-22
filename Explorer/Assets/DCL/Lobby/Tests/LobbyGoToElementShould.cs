using DCL.UIToolkit.Elements;
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
        private const string WITH_TEXT_CLASS = "lobby-goto--with-text";
        private const string INVALID_CLASS = "lobby-goto--invalid";
        private const string CHECKING_CLASS = "lobby-goto--checking";

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
        public void ShowTheCommandThePlaceholderAndTheErrorLine()
        {
            Assert.AreEqual("/goto", element.Q<Label>("Command").text);
            Assert.AreEqual("e.g. 3,-5 or mana.dcl.eth", input.textEdition.placeholder);
            Assert.IsFalse(input.textEdition.hidePlaceholderOnFocus);
            Assert.AreEqual(PickingMode.Ignore, element.Q("Icon").pickingMode);
            Assert.AreEqual("Coordinates or World name doesn't exist", element.Q<Label>("ErrorLabel").text);
            Assert.AreEqual(PickingMode.Ignore, element.Q("Error").pickingMode);
        }

        [Test]
        public void SubmitTheTrimmedTextOnReturnAndKeepTheField()
        {
            //Arrange
            element.Text = "  3,-5 ";

            //Act
            Press(KeyCode.Return);

            //Assert
            Assert.AreEqual(1, submitted.Count);
            Assert.AreEqual("3,-5", submitted[0]);
            Assert.AreEqual("  3,-5 ", element.Text);
            Assert.IsTrue(element.ClassListContains(WITH_TEXT_CLASS));
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

        [TestCase("")]
        [TestCase("   ")]
        public void SubmitNothingAndStayValidWhileTheFieldIsBlank(string text)
        {
            //Arrange
            element.Text = text;

            //Act
            Press(KeyCode.Return);
            Click();

            //Assert
            Assert.AreEqual(0, submitted.Count);
            Assert.AreEqual(text, element.Text);
            Assert.IsFalse(element.ClassListContains(INVALID_CLASS));
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

        [TestCase("", false)]
        [TestCase("   ", false)]
        [TestCase("3,-5", true)]
        [TestCase("m", true)]
        [TestCase(" x ", true)]
        public void ShowTheGoButtonOnceSomethingIsTyped(string text, bool expected)
        {
            //Act
            element.Text = text;

            //Assert
            Assert.AreEqual(expected, element.ClassListContains(WITH_TEXT_CLASS));
        }

        [TestCase("3,-5")]
        [TestCase(" 3,-5/spawn ")]
        [TestCase("3, -5")]
        [TestCase("mana.dcl.eth")]
        [TestCase("mana.dcl.eth/3,-5/spawn")]
        [TestCase("eax")]
        [TestCase("eax/3,-5")]
        [TestCase("eax/spawn")]
        [TestCase("eax/3,-5/spawn")]
        [TestCase("3")]
        [TestCase("random")]
        [TestCase("crowd")]
        [TestCase("crowd.dcl.eth")]
        public void SubmitTheTrimmedDestination(string text)
        {
            //Arrange
            element.Text = text;

            //Act
            Press(KeyCode.Return);

            //Assert
            Assert.AreEqual(1, submitted.Count);
            Assert.AreEqual(text.Trim(), submitted[0]);
            Assert.AreEqual(text, element.Text);
            Assert.IsFalse(element.ClassListContains(INVALID_CLASS));
        }

        [TestCase("1000,-500")]
        [TestCase("3,-5,7")]
        [TestCase("eax/1000,-500")]
        [TestCase("your.world.eth")]
        [TestCase("my-world.dcl.eth")]
        [TestCase("foo bar")]
        [TestCase("mana.dcl.eth/")]
        public void MarkItselfInvalidAndKeepTheTextOnSubmittingANonDestination(string text)
        {
            //Arrange
            element.Text = text;

            //Act
            Press(KeyCode.Return);

            //Assert
            Assert.AreEqual(0, submitted.Count);
            Assert.AreEqual(text, element.Text);
            Assert.IsTrue(element.ClassListContains(INVALID_CLASS));
            Assert.IsTrue(element.ClassListContains(WITH_TEXT_CLASS));
        }

        [Test]
        public void MarkItselfInvalidOnTheGoButtonToo()
        {
            //Arrange
            element.Text = "1000,-500";

            //Act
            Click();

            //Assert
            Assert.AreEqual(0, submitted.Count);
            Assert.AreEqual("1000,-500", element.Text);
            Assert.IsTrue(element.ClassListContains(INVALID_CLASS));
        }

        [Test]
        public void DropTheInvalidMarkOnceTheTextChanges()
        {
            //Arrange
            element.Text = "1000,-500";
            Press(KeyCode.Return);

            //Act
            input.value = "100,-50";

            //Assert
            Assert.IsFalse(element.ClassListContains(INVALID_CLASS));
        }

        [Test]
        public void SubmitTheTrimmedTextOnTheGoButton()
        {
            //Arrange
            element.Text = " mana.dcl.eth ";

            //Act
            Click();

            //Assert
            Assert.AreEqual(1, submitted.Count);
            Assert.AreEqual("mana.dcl.eth", submitted[0]);
        }

        // Closing the lobby from the callback detaches the pill before a queued change event would run
        [Test]
        public void HideTheGoButtonWhenEmptiedBeforeTheSubscriberDetachesIt()
        {
            //Arrange
            element.Submitted = _ =>
            {
                element.Text = string.Empty;
                element.RemoveFromHierarchy();
            };

            element.Text = "eax";

            //Act
            Press(KeyCode.Return);

            //Assert
            Assert.AreEqual(string.Empty, element.Text);
            Assert.IsFalse(element.ClassListContains(WITH_TEXT_CLASS));
        }

        [Test]
        public void ShowTheSpinnerAndTakeNoEditOrSubmitWhileChecking()
        {
            //Arrange
            element.Text = "eax";

            //Act
            element.IsChecking = true;

            //Assert
            Assert.IsTrue(element.IsChecking);
            Assert.IsTrue(element.ClassListContains(CHECKING_CLASS));
            Assert.IsTrue(input.isReadOnly);
            Assert.IsTrue(element.Q<LoadingSpinnerElement>("Spinner").IsSpinning);

            //Act
            Press(KeyCode.Return);
            Click();

            //Assert
            Assert.AreEqual(0, submitted.Count);

            //Act
            element.IsChecking = false;

            //Assert
            Assert.IsFalse(element.ClassListContains(CHECKING_CLASS));
            Assert.IsFalse(input.isReadOnly);
            Assert.IsFalse(element.Q<LoadingSpinnerElement>("Spinner").IsSpinning);
            Assert.AreEqual("eax", element.Text);
        }

        [Test]
        public void EndTheCheckMarkedInvalidWithTheTextKept()
        {
            //Arrange
            element.Text = "eax";
            element.IsChecking = true;

            //Act
            element.ShowInvalid();

            //Assert
            Assert.IsFalse(element.IsChecking);
            Assert.IsFalse(input.isReadOnly);
            Assert.IsTrue(element.ClassListContains(INVALID_CLASS));
            Assert.AreEqual("eax", element.Text);

            //Act
            input.value = "eax2";

            //Assert
            Assert.IsFalse(element.ClassListContains(INVALID_CLASS));
        }

        [Test]
        public void EndTheCheckOnReset()
        {
            //Arrange
            element.Text = "eax";
            element.IsChecking = true;

            //Act
            element.Reset();

            //Assert
            Assert.IsFalse(element.IsChecking);
            Assert.IsFalse(input.isReadOnly);
            Assert.AreEqual(string.Empty, element.Text);
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
        public void EmptyTheFieldAndDropTheFocusAndTheInvalidMarkOnReset()
        {
            //Arrange
            element.Text = "1000,-500";
            input.Focus();
            Press(KeyCode.Return);

            //Act
            element.Reset();

            //Assert
            Assert.AreEqual(string.Empty, element.Text);
            Assert.IsFalse(element.ClassListContains(WITH_TEXT_CLASS));
            Assert.IsFalse(element.ClassListContains(INVALID_CLASS));
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
