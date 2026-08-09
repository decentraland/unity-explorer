using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Loading.Components;
using DCL.Profiles;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Entity = Arch.Core.Entity;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    /// <summary>
    /// Pins the profile-path structural gate on <see cref="AvatarLoaderSystem"/>'s ApplyProfileToAvatarShape:
    /// a Profile.Version bump that touches only non-render fields (bio/links/pronouns) must NOT forget/recreate
    /// the wearable promise nor re-dirty the avatar; a real wearable change still must.
    /// </summary>
    public class AvatarLoaderProfileGateShould : UnitySystemTestBase<AvatarLoaderSystem>
    {
        [SetUp]
        public void SetUp()
        {
            system = new AvatarLoaderSystem(world);
        }

        private static Profile BuildProfile(IEnumerable<URN> wearables) =>
            new ProfileBuilder()
                .WithUserId("user-1")
                .WithBodyShape(BodyShape.MALE)
                .WithWearables(wearables)
                .WithColors((Color.red, Color.green, Color.blue))
                .WithForceRender(new[] { "hair" })
                .WithVersion(1)
                .Build();

        [Test]
        public void NonStructuralProfileEditDoesNotRebuild()
        {
            // P0: create the AvatarShapeComponent + its wearable promise.
            Profile profile = BuildProfile(new URN[] { "urn:w1", "urn:w2" });
            Entity entity = world.Create(profile, PartitionComponent.TOP_PRIORITY);

            system.Update(0);

            Entity promiseEntity;
            {
                ref AvatarShapeComponent comp = ref world.Get<AvatarShapeComponent>(entity);
                promiseEntity = comp.WearablePromise.Entity;
                comp.IsDirty = false; // instantiator consumed the initial build
            }

            // P1: identical in every render-affecting Avatar field; only a non-render field + Version change.
            profile.Description = "changed bio";
            profile.Version = 2;
            profile.IsDirty = true;

            system.Update(0);

            AvatarShapeComponent after = world.Get<AvatarShapeComponent>(entity);
            Assert.That(after.IsDirty, Is.False, "cosmetic/bio edit must not re-dirty the avatar");
            Assert.That(after.WearablePromise.Entity, Is.EqualTo(promiseEntity), "wearable promise must not be recreated");
            Assert.That(world.IsAlive(promiseEntity), Is.True, "original promise must not be forgotten/destroyed");
        }

        [Test]
        public void StructuralProfileEditStillRebuilds()
        {
            Profile profile = BuildProfile(new URN[] { "urn:w1", "urn:w2" });
            Entity entity = world.Create(profile, PartitionComponent.TOP_PRIORITY);

            system.Update(0);

            Entity promiseEntity;
            {
                ref AvatarShapeComponent comp = ref world.Get<AvatarShapeComponent>(entity);
                promiseEntity = comp.WearablePromise.Entity;
                comp.IsDirty = false;
            }

            // Real wearable change: swap in an avatar carrying one extra URN.
            Profile withExtra = BuildProfile(new URN[] { "urn:w1", "urn:w2", "urn:w3" });
            profile.Avatar = withExtra.Avatar;
            profile.IsDirty = true;

            system.Update(0);

            AvatarShapeComponent after = world.Get<AvatarShapeComponent>(entity);
            Assert.That(after.IsDirty, Is.True, "a real wearable change must rebuild");
            Assert.That(after.WearablePromise.Entity, Is.Not.EqualTo(promiseEntity), "gate must not suppress real rebuilds");
        }
    }
}
