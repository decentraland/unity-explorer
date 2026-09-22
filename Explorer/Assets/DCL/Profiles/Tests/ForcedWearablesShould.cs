using CommunicationData.URLHelpers;
using DCL.Profiles.Self;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.Profiles.Tests
{
    public class ForcedWearablesShould
    {
        private const string WEARABLE = "urn:decentraland:matic:collections-v2:0x9251f5c79923bc80e5dd8fc6d0c9fa02953aa622:0";
        private const string OWNED_WEARABLE = "urn:decentraland:off-chain:base-avatars:f_sweater";

        private static Profile NewProfile(params string[] wearables) =>
            new ProfileBuilder()
               .WithUserId("0x0000000000000000000000000000000000000001")
               .WithWearables(NewUrnSet(wearables))
               .Build();

        private static HashSet<URN> NewUrnSet(IEnumerable<string> urns)
        {
            var set = new HashSet<URN>();

            foreach (string urn in urns)
                set.Add(new URN(urn));

            return set;
        }

        [Test]
        public void ApplyTheForcedSet()
        {
            var forced = new ForcedWearables(new[] { new URN(WEARABLE) });
            Profile profile = NewProfile(OWNED_WEARABLE);

            forced.ApplyTo(profile);

            Assert.That(profile.Avatar.Wearables, Has.Member(new URN(WEARABLE)));
            Assert.That(profile.Avatar.Wearables, Has.Member(new URN(OWNED_WEARABLE)), "the profile's own wearables must be left alone");
        }

        [Test]
        public void ShortenUrnsOnTheWayIn()
        {
            // An extended URN carries a token id; the profile holds the shortened form.
            var forced = new ForcedWearables(new[] { new URN($"{WEARABLE}:105") });
            Profile profile = NewProfile();

            forced.ApplyTo(profile);

            Assert.That(profile.Avatar.Wearables, Has.Member(new URN(WEARABLE)));
        }

        [Test]
        public void IgnoreEmptyUrns()
        {
            var forced = new ForcedWearables(new[] { default(URN), new URN(WEARABLE) });
            Profile profile = NewProfile();

            forced.ApplyTo(profile);

            Assert.That(profile.Avatar.Wearables, Is.EquivalentTo(new[] { new URN(WEARABLE) }));
        }

        [Test]
        public void LeaveTheProfileAloneWhenNothingIsForced()
        {
            var forced = new ForcedWearables();
            Profile profile = NewProfile(OWNED_WEARABLE);

            forced.ApplyTo(profile);

            Assert.That(profile.Avatar.Wearables, Is.EquivalentTo(new[] { new URN(OWNED_WEARABLE) }));
        }

        [Test]
        public void ReportAnEmptySetAsInactive()
        {
            // SelfProfile blocks every deploy while this is true, so an empty set must not read as active.
            Assert.That(new ForcedWearables().Any, Is.False);
            Assert.That(new ForcedWearables(new[] { default(URN) }).Any, Is.False, "an empty URN is dropped on the way in");
            Assert.That(new ForcedWearables(new[] { new URN(WEARABLE) }).Any, Is.True);
        }

        [Test]
        public void KeepAvatarWearablesBackedByAHashSet()
        {
            // Pinned so a change of backing type fails in CI instead of leaving the avatar bare.
            Assert.That(NewProfile().Avatar.Wearables, Is.InstanceOf<HashSet<URN>>());
        }
    }
}
