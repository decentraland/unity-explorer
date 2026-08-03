using DCL.Chat;
using DCL.Communities.CommunitiesBrowser;
using DCL.InWorldCamera.PhotoDetail;
using DCL.Navmap;
using DCL.NftPrompt;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace DCL.Tests.Editor
{
    /// <summary>
    ///     Every label that renders text another user wrote either has <c>richText</c> off in its prefab, or keeps it
    ///     on because its own copy is markup and the value reaching it is escaped in code instead. Both halves are
    ///     pinned here against the shipped assets, because a prefab is edited in an inspector where neither choice is
    ///     visible and a single toggle either reopens an injection or silently kills a label's links
    ///     (SEC-008/034/050/084).
    /// </summary>
    public class UntrustedTextLabelsShould
    {
        private const string PLACES_AND_EVENTS = "Assets/DCL/Navmap/Assets/PlacesAndEventsPanel.prefab";
        private const string PLACE_TOAST = "Assets/DCL/Navmap/Assets/PlaceToast.prefab";

        [TestCase(PLACES_AND_EVENTS, nameof(PlaceInfoPanelView), "<PlaceNameLabel>k__BackingField")]
        [TestCase(PLACES_AND_EVENTS, nameof(PlaceInfoPanelView), "<CoordinatesLabel>k__BackingField")]
        [TestCase(PLACES_AND_EVENTS, nameof(PlaceInfoPanelView), "<LiveEventNameLabel>k__BackingField")]
        [TestCase(PLACES_AND_EVENTS, nameof(EventInfoPanelView), "<EventNameLabel>k__BackingField")]
        [TestCase(PLACE_TOAST, nameof(PlaceInfoPanelView), "<PlaceNameLabel>k__BackingField")]
        [TestCase("Assets/DCL/Navmap/Assets/EventEntry.prefab", nameof(EventElementView), "<EventNameLabel>k__BackingField")]
        [TestCase("Assets/DCL/Communities/CommunitiesBrowser/Prefabs/CommunityResultCard.prefab", nameof(CommunityResultCardView), "communityDescription")]
        [TestCase("Assets/DCL/NftPrompt/Assets/NftPrompt.prefab", nameof(NftPromptView), "<TextDescription>k__BackingField")]
        [TestCase("Assets/DCL/Chat/Assets/ChatEntries/ChatEntryUsernameElement.prefab", nameof(ChatEntryUsernameElement), "<userName>k__BackingField")]
        public void RenderUntrustedTextAsPlainText(string prefabPath, string componentType, string fieldName)
        {
            // Arrange
            TMP_Text label = LabelOf(prefabPath, componentType, fieldName);

            // Assert
            Assert.IsNotNull(label, $"{fieldName} is not bound in {prefabPath}");
            Assert.IsFalse(label.richText, $"{fieldName} in {prefabPath} must not render rich text");
        }

        // The other half of the contract. These labels carry markup of their own — a <b> run around a name, or the
        // <link> the description linkifier emits — so turning rich text off here would not harden anything, it would
        // break them. PlaceToast is the reason this test exists: four of its view fields, the description among
        // them, are bound to one single component, so a flag flipped for the coordinates would also silence the
        // description's links.
        [TestCase(PLACES_AND_EVENTS, nameof(EventInfoPanelView), "<HostAndPlaceLabel>k__BackingField")]
        [TestCase(PLACES_AND_EVENTS, nameof(EventInfoPanelView), "<DescriptionLabel>k__BackingField")]
        [TestCase(PLACES_AND_EVENTS, nameof(PlaceInfoPanelView), "<DescriptionLabel>k__BackingField")]
        [TestCase(PLACES_AND_EVENTS, nameof(PlaceInfoPanelView), "<CreatorNameLabel>k__BackingField")]
        [TestCase(PLACE_TOAST, nameof(PlaceInfoPanelView), "<DescriptionLabel>k__BackingField")]
        public void KeepRichTextOnLabelsWhoseOwnCopyIsMarkup(string prefabPath, string componentType, string fieldName)
        {
            // Arrange
            TMP_Text label = LabelOf(prefabPath, componentType, fieldName);

            // Assert
            Assert.IsNotNull(label, $"{fieldName} is not bound in {prefabPath}");
            Assert.IsTrue(label.richText, $"{fieldName} in {prefabPath} needs rich text for its own markup");
        }

        [Test]
        public void ShareOneComponentAcrossThePlaceToastLabels()
        {
            // Arrange — documents the wiring the test above guards, so a reader does not "fix" that test by
            // flipping the flag it asserts.
            TMP_Text description = LabelOf(PLACE_TOAST, nameof(PlaceInfoPanelView), "<DescriptionLabel>k__BackingField");
            TMP_Text coordinates = LabelOf(PLACE_TOAST, nameof(PlaceInfoPanelView), "<CoordinatesLabel>k__BackingField");

            // Assert
            Assert.AreSame(description, coordinates,
                "PlaceToast binds its description and coordinates to one label; both writers must escape in code");
        }

        /// <summary>
        ///     Resolves the label through the serialized binding its view writes to, so an assertion cannot drift onto
        ///     a different label than the one named.
        /// </summary>
        private static TMP_Text LabelOf(string prefabPath, string componentType, string fieldName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, prefabPath);

            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != componentType)
                    continue;

                using var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.FindProperty(fieldName);

                if (property != null)
                    return (TMP_Text)property.objectReferenceValue;
            }

            Assert.Fail($"{componentType}.{fieldName} not found in {prefabPath}");
            return null!;
        }
    }
}
