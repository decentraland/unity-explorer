using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using ECS.TestSuite;
using NUnit.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
            var profile = Profile.Create();
            var clean = Profile.Create();
            int countAfterCreates = Profile.POOL.CountInactive;

            PopulateAllFields(profile);

            profile.Dispose();
            var recycled = Profile.Create();

            // Dispose+Create = net zero change from countAfterCreates
            Assert.AreEqual(countAfterCreates, Profile.POOL.CountInactive);
            Assert.IsTrue(clean.IsSameProfile(recycled));
            Assert.IsNull(recycled.blocked);
            Assert.IsNull(recycled.interests);
            Assert.IsNull(recycled.Links);

            recycled.Dispose();
            clean.Dispose();

            Assert.AreEqual(countAfterCreates + 2, Profile.POOL.CountInactive);
        }

        [Test]
        public void DeserializeProfilesConcurrently()
        {
            const string JSON = @"{""avatars"":[{""name"":""test"",""userId"":""0x123"",
                ""hasClaimedName"":false,""description"":"""",""tutorialStep"":0,
                ""version"":1,""hasConnectedWeb3"":false,
                ""blocked"":[""0xa"",""0xb""],""interests"":[""gaming"",""art""],
                ""avatar"":{""bodyShape"":"""",""wearables"":[],""forceRender"":[],""emotes"":[],
                    ""eyes"":{""color"":{""r"":0,""g"":0,""b"":0,""a"":1}},
                    ""hair"":{""color"":{""r"":0,""g"":0,""b"":0,""a"":1}},
                    ""skin"":{""color"":{""r"":0,""g"":0,""b"":0,""a"":1}}},
                ""links"":[{""title"":""web"",""url"":""https://test.com""}]}]}";

            var tasks = new Task[50];

            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    Profile? profile = JsonConvert.DeserializeObject<Profile>(JSON,
                        RealmProfileRepository.SERIALIZER_SETTINGS);

                    profile?.Dispose();
                });
            }

            Assert.DoesNotThrow(() => Task.WaitAll(tasks));
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
            profile.blocked = new HashSet<string> { "0xblocked1" };
            profile.interests = new List<string> { "gaming" };

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
