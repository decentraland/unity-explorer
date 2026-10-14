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

        [Test]
        public void SubmitNothingWhileTheFieldIsBlank()
        {
            //Arrange
            element.Text = "   ";

            //Act
            Press(KeyCode.Return);

            //Assert
            Assert.AreEqual(0, submitted.Count);
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

        private void Press(KeyCode keyCode)
        {
            using KeyDownEvent evt = KeyDownEvent.GetPooled('\0', keyCode, EventModifiers.None);
            evt.target = input;
            input.SendEvent(evt);
        }
    }
}
