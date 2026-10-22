using CommunicationData.URLHelpers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DCL.Profiles.Tests
{
    public class ProfileCompactInfoShould
    {
        private const string USER_ID = "0x0000000000000000000000000000000000000001";

        [Test]
        public void HaveAnEmptyFaceSnapshotUrlWhenCreatedWithoutOne()
        {
            // Act
            var compact = new Profile.CompactInfo(UserId.New(USER_ID).Unwrap());

            // Assert
            Assert.That(compact.FaceSnapshotUrl, Is.EqualTo(URLAddress.EMPTY));
        }

        [Test]
        public void HaveAnEmptyFaceSnapshotUrlWhenTheThumbnailIsMissingFromJson()
        {
            // Arrange
            var json = new JObject { ["userId"] = USER_ID };

            // Act
            Profile.CompactInfo compact = ProfileCompactInfoConverter.ReadJson(json);

            // Assert
            Assert.That(compact.FaceSnapshotUrl, Is.EqualTo(URLAddress.EMPTY));
        }
    }
}
