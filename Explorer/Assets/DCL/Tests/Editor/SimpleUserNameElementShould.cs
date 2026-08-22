using DCL.Profiles;
using DCL.UI.ProfileElements;
using ECS.TestSuite;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace DCL.Tests.Editor
{
    /// <summary>
    ///     <see cref="SimpleUserNameElement" /> renders a name authored by another user wherever that user shows up —
    ///     mention suggestions, profile cards, the voice-chat titlebar. A profile name is a schema-valid string that no
    ///     backend rejects, so the element itself is what keeps it filtered, inert and bounded (SEC-034).
    /// </summary>
    public class SimpleUserNameElementShould
    {
        private const string PREFAB_PATH = "Assets/DCL/UI/Profiles/Assets/SimpleUserNameElement.prefab";
        private const string USER_ID = "0x79fdd6f8ba257bda1d5a2a413ae0b43ec300ed10";

        // Unwrap is fine here and only here: the address above is a known-valid constant, so the Option cannot be
        // empty. Production code models the absence instead.
        private static readonly UserId USER = UserId.New(USER_ID).Unwrap();

        private GameObject canvasRoot = null!;
        private SimpleUserNameElement element = null!;
        private TMP_Text nameLabel = null!;
        private TMP_Text hashtagLabel = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Building a CompactInfo derives the validated name, which reads the features registry. The editor domain
            // may still hold one initialized by a play-mode session.
            EcsTestsUtils.TearDownFeaturesRegistry();
            EcsTestsUtils.SetUpFeaturesRegistry();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void SetUp()
        {
            // A copy of the prefab under a canvas, so the labels live in the hierarchy they were authored for and the
            // asset itself is never written to.
            canvasRoot = new GameObject(nameof(SimpleUserNameElementShould), typeof(Canvas));
            element = Object.Instantiate(LoadPrefab(), canvasRoot.transform).GetComponent<SimpleUserNameElement>();
            nameLabel = LabelOf(element, "userNameText");
            hashtagLabel = LabelOf(element, "userNameHashtagText");
        }

        [TearDown]
        public void TearDown() =>
            Object.DestroyImmediate(canvasRoot);

        [Test]
        public void RenderAnotherUsersNameAsPlainText()
        {
            // Arrange — the shipped asset rather than the instance, so this is an assertion about what players get.
            var asset = LoadPrefab().GetComponent<SimpleUserNameElement>();

            // Assert — both labels carry nothing but another user's name and its #XXXX suffix, never styled copy of
            // their own, so rich text is off. Guarded here because a prefab edit could re-enable it unnoticed.
            Assert.IsFalse(LabelOf(asset, "userNameText").richText, "userNameText");
            Assert.IsFalse(LabelOf(asset, "userNameHashtagText").richText, "userNameHashtagText");
        }

        // The mention list and the voice-chat titlebar host their own copies of these labels rather than nesting
        // an instance of the prefab above, so they do not inherit its flags and the guard has to name them.
        [TestCase("Assets/DCL/UI/InputSuggestions/ProfileInputSuggestionElement.prefab")]
        [TestCase("Assets/DCL/VoiceChat/Assets/VoiceChatInCallTitlebar.prefab")]
        public void RenderAnotherUsersNameAsPlainTextWhereverTheElementIsDuplicated(string prefabPath)
        {
            // Arrange
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponentInChildren<SimpleUserNameElement>(true);

            // Assert
            Assert.IsNotNull(asset, prefabPath);
            Assert.IsFalse(LabelOf(asset, "userNameText").richText, $"userNameText in {prefabPath}");
            Assert.IsFalse(LabelOf(asset, "userNameHashtagText").richText, $"userNameHashtagText in {prefabPath}");
        }

        [TestCase("<size=400%><color=#00FF00>Verified Admin</color>")]
        [TestCase("<link=\"https://evil.example.com\">click</link>")]
        [TestCase("<b><i><u>nested")]
        public void NotLetANameReachTheLabelAsMarkup(string name)
        {
            // Act
            element.Setup(new Profile.CompactInfo(USER, name));

            // Assert — TMP only begins parsing a tag at '<', so the absence of both brackets is what makes the name
            // inert no matter which prefab binds this element.
            Assert.That(nameLabel.text, Does.Not.Contain("<").And.Not.Contain(">"), name);
        }

        [Test]
        public void CapAnOversizedName()
        {
            // Arrange — two names well past any plausible cap. The label grows to fit its text, so an uncapped name is
            // an unbounded layout pass.
            var longName = new string('a', 4_000);
            var longerName = new string('a', 40_000);

            // Act
            element.Setup(new Profile.CompactInfo(USER, longName));
            int renderedLongLength = nameLabel.text.Length;
            element.Setup(new Profile.CompactInfo(USER, longerName));

            // Assert — the rendered length saturates instead of tracking the input, whatever the exact cap is.
            Assert.Less(renderedLongLength, longName.Length);
            Assert.AreEqual(renderedLongLength, nameLabel.text.Length);
            StringAssert.EndsWith("…", nameLabel.text);
        }

        [Test]
        public void RenderAClaimedNameUnchanged()
        {
            // Act
            element.Setup(new Profile.CompactInfo(USER, "Guybrush", hasClaimedName: true));

            // Assert — an ordinary name is not clipped, escaped into lookalikes, or given a suffix it never had.
            Assert.AreEqual("Guybrush", nameLabel.text);
            Assert.IsFalse(hashtagLabel.gameObject.activeSelf);
        }

        [Test]
        public void RenderTheSuffixOfAnUnclaimedNameOnlyOnce()
        {
            // Act
            element.Setup(new Profile.CompactInfo(USER, "Guybrush"));

            // Assert — the #XXXX suffix belongs to the hashtag label. Reading the display name into the name label
            // would put a second copy of it there.
            Assert.AreEqual("Guybrush", nameLabel.text);
            Assert.IsTrue(hashtagLabel.gameObject.activeSelf);
            Assert.AreEqual("#ed10", hashtagLabel.text);
        }

        private static GameObject LoadPrefab() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);

        /// <summary>
        ///     Resolves the label through the serialized binding the element writes to, so an assertion cannot drift
        ///     onto a different label than the one under test.
        /// </summary>
        private static TMP_Text LabelOf(SimpleUserNameElement target, string fieldName)
        {
            using var serializedElement = new SerializedObject(target);
            return (TMP_Text)serializedElement.FindProperty(fieldName).objectReferenceValue;
        }
    }
}
