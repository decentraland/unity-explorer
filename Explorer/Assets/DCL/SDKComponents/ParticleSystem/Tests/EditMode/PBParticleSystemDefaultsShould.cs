using DCL.ECSComponents;
using DCL.SDKComponents.ParticleSystem.Components;
using NUnit.Framework;

namespace DCL.ParticleSystem.Tests
{
    public class PBParticleSystemDefaultsShould
    {
        [Test]
        public void ReturnDefaultRateWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.AreEqual(10f, pb.GetRate());
        }

        [Test]
        public void ReturnExplicitRateWhenSet()
        {
            var pb = new PBParticleSystem { Rate = 50f };
            Assert.AreEqual(50f, pb.GetRate());
        }

        [Test]
        public void ReturnDefaultMaxParticlesWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.AreEqual(1000u, pb.GetMaxParticles());
        }

        [Test]
        public void ReturnExplicitMaxParticlesWhenSet()
        {
            var pb = new PBParticleSystem { MaxParticles = 500 };
            Assert.AreEqual(500u, pb.GetMaxParticles());
        }

        [Test]
        public void ReturnDefaultLifetimeWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.AreEqual(5f, pb.GetLifetime());
        }

        [Test]
        public void ReturnExplicitLifetimeWhenSet()
        {
            var pb = new PBParticleSystem { Lifetime = 3f };
            Assert.AreEqual(3f, pb.GetLifetime());
        }

        [Test]
        public void ReturnDefaultGravityWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.AreEqual(0f, pb.GetGravity());
        }

        [Test]
        public void ReturnExplicitGravityWhenSet()
        {
            var pb = new PBParticleSystem { Gravity = -9.8f };
            Assert.AreEqual(-9.8f, pb.GetGravity());
        }

        [Test]
        public void ReturnTrueForActiveWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.IsTrue(pb.GetActive());
        }

        [Test]
        public void ReturnFalseForActiveWhenExplicitlyFalse()
        {
            var pb = new PBParticleSystem { Active = false };
            Assert.IsFalse(pb.GetActive());
        }

        [Test]
        public void ReturnTrueForLoopWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.IsTrue(pb.GetLoop());
        }

        [Test]
        public void ReturnFalseForLoopWhenExplicitlyFalse()
        {
            var pb = new PBParticleSystem { Loop = false };
            Assert.IsFalse(pb.GetLoop());
        }

        [Test]
        public void ReturnFalseForPrewarmWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.IsFalse(pb.GetPrewarm());
        }

        [Test]
        public void ReturnTrueForPrewarmWhenExplicitlyTrue()
        {
            var pb = new PBParticleSystem { Prewarm = true };
            Assert.IsTrue(pb.GetPrewarm());
        }

        [Test]
        public void ReturnFalseForFaceTravelDirectionWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.IsFalse(pb.GetFaceTravelDirection());
        }

        [Test]
        public void ReturnDefaultBlendModeWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.AreEqual(PBParticleSystem.Types.BlendMode.PsbAlpha, pb.GetBlendMode());
        }

        [Test]
        public void ReturnExplicitBlendModeWhenSet()
        {
            var pb = new PBParticleSystem { BlendMode = PBParticleSystem.Types.BlendMode.PsbAdd };
            Assert.AreEqual(PBParticleSystem.Types.BlendMode.PsbAdd, pb.GetBlendMode());
        }

        [Test]
        public void ReturnDefaultSimulationSpaceWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.AreEqual(PBParticleSystem.Types.SimulationSpace.PssLocal, pb.GetSimulationSpace());
        }

        [Test]
        public void ReturnExplicitSimulationSpaceWhenSet()
        {
            var pb = new PBParticleSystem { SimulationSpace = PBParticleSystem.Types.SimulationSpace.PssWorld };
            Assert.AreEqual(PBParticleSystem.Types.SimulationSpace.PssWorld, pb.GetSimulationSpace());
        }

        [Test]
        public void ReturnDefaultPlaybackStateWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.AreEqual(PBParticleSystem.Types.PlaybackState.PsPlaying, pb.GetPlaybackState());
        }

        [Test]
        public void ReturnExplicitPlaybackStateWhenSet()
        {
            var pb = new PBParticleSystem { PlaybackState = PBParticleSystem.Types.PlaybackState.PsPaused };
            Assert.AreEqual(PBParticleSystem.Types.PlaybackState.PsPaused, pb.GetPlaybackState());
        }

        [Test]
        public void ReturnTrueForBillboardWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.IsTrue(pb.GetBillboard());
        }

        [Test]
        public void ReturnFalseForBillboardWhenExplicitlyFalse()
        {
            var pb = new PBParticleSystem { Billboard = false };
            Assert.IsFalse(pb.GetBillboard());
        }

        [Test]
        public void ReturnDefaultSphereRadiusWhenNotSet()
        {
            var sphere = new PBParticleSystem.Types.Sphere();
            Assert.AreEqual(1f, sphere.GetRadius());
        }

        [Test]
        public void ReturnExplicitSphereRadiusWhenSet()
        {
            var sphere = new PBParticleSystem.Types.Sphere { Radius = 5f };
            Assert.AreEqual(5f, sphere.GetRadius());
        }

        [Test]
        public void ReturnDefaultConeAngleWhenNotSet()
        {
            var cone = new PBParticleSystem.Types.Cone();
            Assert.AreEqual(25f, cone.GetAngle());
        }

        [Test]
        public void ReturnDefaultConeRadiusWhenNotSet()
        {
            var cone = new PBParticleSystem.Types.Cone();
            Assert.AreEqual(1f, cone.GetRadius());
        }

        [Test]
        public void ReturnDefaultDampenWhenNotSet()
        {
            var limitVelocity = new PBParticleSystem.Types.LimitVelocity();
            Assert.AreEqual(1f, limitVelocity.GetDampen());
        }

        [Test]
        public void ReturnDefaultFramesPerSecondWhenNotSet()
        {
            var spriteSheet = new PBParticleSystem.Types.SpriteSheetAnimation();
            Assert.AreEqual(30f, spriteSheet.GetFramesPerSecond());
        }

        [Test]
        public void ReturnDefaultBurstCyclesWhenNotSet()
        {
            var burst = new PBParticleSystem.Types.Burst();
            Assert.AreEqual(1, burst.GetCycles());
        }

        [Test]
        public void ReturnDefaultBurstIntervalWhenNotSet()
        {
            var burst = new PBParticleSystem.Types.Burst();
            Assert.AreEqual(0.01f, burst.GetInterval());
        }

        [Test]
        public void ReturnDefaultBurstProbabilityWhenNotSet()
        {
            var burst = new PBParticleSystem.Types.Burst();
            Assert.AreEqual(1f, burst.GetProbability());
        }
    }
}
