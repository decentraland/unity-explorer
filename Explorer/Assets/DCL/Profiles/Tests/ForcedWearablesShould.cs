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

            Assert.That(profile.Avatar.Wearables, Does.Contain(new URN(WEARABLE)));
            Assert.That(profile.Avatar.Wearables, Does.Contain(new URN(OWNED_WEARABLE)), "the profile's own wearables must be left alone");
        }

        [Test]
        public void StripOnlyWhatItForced()
        {
            var forced = new ForcedWearables(new[] { new URN(WEARABLE) });
            Profile profile = NewProfile(OWNED_WEARABLE);

            forced.ApplyTo(profile);
            forced.RemoveFrom(profile);

            Assert.That(profile.Avatar.Wearables, Does.Not.Contain(new URN(WEARABLE)));
            Assert.That(profile.Avatar.Wearables, Does.Contain(new URN(OWNED_WEARABLE)));
        }

        [Test]
        public void StripAForcedWearableTheProfileAlreadyCarries()
        {
            // The profile about to be deployed is rebuilt from the equipped set, not from the one ApplyTo touched,
            // so the strip has to work on a wearable this instance never applied itself.
            var forced = new ForcedWearables(new[] { new URN(WEARABLE) });
            Profile profile = NewProfile(WEARABLE, OWNED_WEARABLE);

            forced.RemoveFrom(profile);

            Assert.That(profile.Avatar.Wearables, Is.EquivalentTo(new[] { new URN(OWNED_WEARABLE) }));
        }

        [Test]
        public void ShortenUrnsOnTheWayIn()
        {
            // An extended URN carries a token id; the profile holds the shortened form.
            var forced = new ForcedWearables(new[] { new URN($"{WEARABLE}:105") });
            Profile profile = NewProfile();

            forced.ApplyTo(profile);

            Assert.That(profile.Avatar.Wearables, Does.Contain(new URN(WEARABLE)));
        }

        [Test]
        public void StripAWearableForcedByItsExtendedUrn()
        {
            var forced = new ForcedWearables(new[] { new URN($"{WEARABLE}:105") });
            Profile profile = NewProfile(WEARABLE);

            forced.RemoveFrom(profile);

            Assert.That(profile.Avatar.Wearables, Does.Not.Contain(new URN(WEARABLE)));
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
            forced.RemoveFrom(profile);

            Assert.That(profile.Avatar.Wearables, Is.EquivalentTo(new[] { new URN(OWNED_WEARABLE) }));
        }

        [Test]
        public void KeepAvatarWearablesBackedByAHashSet()
        {
            // ForcedWearables reaches the backing set through this cast, because Avatar.wearables is internal to
            // DCL.SharedAPI. Pinned here so a change of backing type fails in CI instead of disabling the strip.
            Assert.That(NewProfile().Avatar.Wearables, Is.InstanceOf<HashSet<URN>>());
        }
    }
}
