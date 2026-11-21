using Newtonsoft.Json;
using NUnit.Framework;

namespace DCL.Profiles.Tests
{
    public class ProfileConverterShould
    {
        private const string USER_ID = "0x0000000000000000000000000000000000000001";

        [Test]
        public void TreatNullValueTypeFieldsAsMissing()
        {
            // Arrange
            string json = "{\"avatars\":[{"
                          + "\"userId\":\"" + USER_ID + "\","
                          + "\"hasClaimedName\":null,"
                          + "\"tutorialStep\":null,"
                          + "\"version\":null,"
                          + "\"hasConnectedWeb3\":null,"
                          + "\"birthDate\":null,"
                          + "\"avatar\":{\"emotes\":[{\"slot\":null,\"urn\":\"wave\"}]}"
                          + "}]}";

            // Act
            if (JsonConvert.DeserializeObject<Profile>(json, new ProfileConverter()) is not { } profile)
            {
                Assert.Fail("Profile was not deserialized");
                return;
            }

            // Assert
            Assert.That(profile.HasClaimedName, Is.False);
            Assert.That(profile.TutorialStep, Is.EqualTo(0));
            Assert.That(profile.Version, Is.EqualTo(0));
            Assert.That(profile.HasConnectedWeb3, Is.False);
            Assert.That(profile.Birthdate, Is.Null);

            profile.Dispose();
        }
    }
}
