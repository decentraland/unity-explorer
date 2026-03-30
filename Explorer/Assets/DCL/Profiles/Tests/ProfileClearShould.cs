using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using ECS.TestSuite;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Profiles.Tests
{
    public class ProfileClearShould
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() =>
            EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [Test]
        public void ResetAllFieldsWhenReturnedToPool()
        {
            int initialInactive = Profile.POOL.CountInactive;

            var profile = Profile.Create();
            var clean = Profile.Create();
            PopulateAllFields(profile);

            profile.Dispose();
            var recycled = Profile.Create();

            Assert.AreEqual(initialInactive, Profile.POOL.CountInactive);
            Assert.IsTrue(clean.IsSameProfile(recycled));

            recycled.Dispose();
            clean.Dispose();

            Assert.AreEqual(initialInactive + 2, Profile.POOL.CountInactive);
        }

        private static void PopulateAllFields(Profile profile)
        {
            ref Profile.CompactInfo compact = ref profile.GetCompact();
            compact.UserId = "0x1234567890abcdef1234567890abcdef12345678";
            compact.Name = "TestUser";
            compact.HasClaimedName = true;
            compact.ClaimedNameColor = Color.red;
            compact.FaceSnapshotUrl = URLAddress.FromString("https://example.com/face.png");
            compact.UnclaimedName = "unclaimed";

            profile.HasConnectedWeb3 = true;
            profile.Description = "A test description";
            profile.TutorialStep = 42;
            profile.Email = "test@test.com";
            profile.Country = "Argentina";
            profile.EmploymentStatus = "Employed";
            profile.Gender = "Male";
            profile.Pronouns = "he/him";
            profile.RelationshipStatus = "Single";
            profile.SexualOrientation = "Straight";
            profile.Language = "English";
            profile.Profession = "Developer";
            profile.RealName = "John Doe";
            profile.Hobbies = "Coding";
            profile.Birthdate = new DateTime(1990, 1, 1);
            profile.Version = 7;
            profile.IsDirty = true;
            profile.Links = new List<LinkJsonDto> { new () { title = "test", url = "https://test.com" } };

            profile.Avatar = new Avatar();
            profile.Avatar.BodyShape = BodyShape.FEMALE;
            profile.Avatar.wearables.Add(new URN("urn:decentraland:off-chain:base-avatars:test_wearable"));
            profile.Avatar.forceRender.Add("upper_body");
            profile.Avatar.emotes[0] = new URN("urn:decentraland:off-chain:base-avatars:clap");
            profile.Avatar.HairColor = Color.blue;
            profile.Avatar.EyesColor = Color.green;
            profile.Avatar.SkinColor = Color.yellow;
            profile.Avatar.BodySnapshotUrl = URLAddress.FromString("https://example.com/body.png");
        }
    }
}
