using DCL.Chat.ChatReactions.Simulation;
using DCL.Chat.ChatReactions.Simulation.UI;
using DCL.Chat.ChatReactions.Simulation.World;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class ParticleIntegratorShould
    {
        private const float DT = 0.016f;

        // ── World particles ─────────────────────────────────────

        [Test]
        public void AdvanceWorldParticleAge()
        {
            // Arrange
            var buffer = new[] { MakeWorldParticle(age: 0f, lifetime: 2f) };

            // Act
            ParticleIntegrator.Step(buffer, 1, Vector3.zero, 0f, DT);

            // Assert
            Assert.That(buffer[0].age, Is.EqualTo(DT).Within(1e-6f));
        }

        [Test]
        public void MarkWorldParticleDeadWhenAgeExceedsLifetime()
        {
            // Arrange
            var buffer = new[] { MakeWorldParticle(age: 0.99f, lifetime: 1f) };

            // Act
            ParticleIntegrator.Step(buffer, 1, Vector3.zero, 0f, 0.02f);

            // Assert
            Assert.That(buffer[0].alive, Is.EqualTo(0));
        }

        [Test]
        public void NotMarkWorldParticleDeadBeforeLifetime()
        {
            // Arrange
            var buffer = new[] { MakeWorldParticle(age: 0f, lifetime: 1f) };

            // Act
            ParticleIntegrator.Step(buffer, 1, Vector3.zero, 0f, DT);

            // Assert
            Assert.That(buffer[0].alive, Is.EqualTo(1));
        }

        [Test]
        public void ApplyGravityToWorldVelocity()
        {
            // Arrange
            var gravity = new Vector3(0f, -9.8f, 0f);
            var buffer = new[] { MakeWorldParticle(vel: Vector3.zero) };

            // Act
            ParticleIntegrator.Step(buffer, 1, gravity, 0f, DT);

            // Assert
            Assert.That(buffer[0].vel.y, Is.LessThan(0f));
            Assert.That(buffer[0].vel.y, Is.EqualTo(-9.8f * DT).Within(1e-4f));
        }

        [Test]
        public void ApplyDragToWorldVelocity()
        {
            // Arrange
            float drag = 2f;
            var buffer = new[] { MakeWorldParticle(vel: new Vector3(0f, 10f, 0f)) };

            // Act
            ParticleIntegrator.Step(buffer, 1, Vector3.zero, drag, DT);

            // Assert
            float expectedDragFactor = Mathf.Exp(-drag * DT);
            Assert.That(buffer[0].vel.y, Is.EqualTo(10f * expectedDragFactor).Within(1e-4f));
        }

        [Test]
        public void IntegrateWorldPosition()
        {
            // Arrange
            var vel = new Vector3(100f, 0f, 0f);
            var buffer = new[] { MakeWorldParticle(pos: Vector3.zero, vel: vel) };

            // Act
            ParticleIntegrator.Step(buffer, 1, Vector3.zero, 0f, DT);

            // Assert — vel is unchanged (no gravity/drag), pos += vel * dt
            Assert.That(buffer[0].pos.x, Is.EqualTo(100f * DT).Within(1e-3f));
        }

        // Ensures a zero-length tick produces no state changes.
        [Test]
        public void HandleZeroDeltaTime()
        {
            // Arrange
            var buffer = new[] { MakeWorldParticle(age: 0.5f, vel: Vector3.up) };
            float originalAge = buffer[0].age;

            // Act
            ParticleIntegrator.Step(buffer, 1, Vector3.zero, 0f, 0f);

            // Assert
            Assert.That(buffer[0].age, Is.EqualTo(originalAge));
            Assert.That(buffer[0].alive, Is.EqualTo(1));
        }

        // Verifies that position integration is skipped for particles that die during the step.
        [Test]
        public void NotUpdateDeadWorldParticlePosition()
        {
            // Arrange — particle that will die this step
            var buffer = new[] { MakeWorldParticle(age: 1f, lifetime: 1f, vel: new Vector3(100f, 0f, 0f)) };
            var originalPos = buffer[0].pos;

            // Act
            ParticleIntegrator.Step(buffer, 1, Vector3.zero, 0f, DT);

            // Assert
            Assert.That(buffer[0].alive, Is.EqualTo(0));
            Assert.That(buffer[0].pos, Is.EqualTo(originalPos));
        }

        [Test]
        public void ProcessMultipleWorldParticles()
        {
            // Arrange
            var buffer = new[]
            {
                MakeWorldParticle(age: 0f, lifetime: 2f),
                MakeWorldParticle(age: 0f, lifetime: 2f),
                MakeWorldParticle(age: 0f, lifetime: 2f),
            };

            // Act
            ParticleIntegrator.Step(buffer, 3, Vector3.zero, 0f, DT);

            // Assert
            for (int i = 0; i < 3; i++)
                Assert.That(buffer[i].age, Is.EqualTo(DT).Within(1e-6f));
        }

        // ── UI particles ─────────────────────────────────────────

        [Test]
        public void AdvanceUiParticleAge()
        {
            // Arrange
            var buffer = new[] { MakeUiParticle(age: 0f, lifetime: 2f) };

            // Act
            ParticleIntegrator.Step(buffer, 1, Vector2.zero, 0f, DT);

            // Assert
            Assert.That(buffer[0].age, Is.EqualTo(DT).Within(1e-6f));
        }

        [Test]
        public void MarkUiParticleDeadWhenAgeExceedsLifetime()
        {
            // Arrange
            var buffer = new[] { MakeUiParticle(age: 0.99f, lifetime: 1f) };

            // Act
            ParticleIntegrator.Step(buffer, 1, Vector2.zero, 0f, 0.02f);

            // Assert
            Assert.That(buffer[0].alive, Is.EqualTo(0));
        }

        [Test]
        public void ApplyAccelerationToUiVelocity()
        {
            // Arrange
            var accel = new Vector2(0f, 100f);
            var buffer = new[] { MakeUiParticle(screenVel: Vector2.zero) };

            // Act
            ParticleIntegrator.Step(buffer, 1, accel, 0f, DT);

            // Assert
            Assert.That(buffer[0].screenVel.y, Is.EqualTo(100f * DT).Within(1e-3f));
        }

        [Test]
        public void IntegrateUiScreenPosition()
        {
            // Arrange
            var vel = new Vector2(200f, 0f);
            var buffer = new[] { MakeUiParticle(screenPos: Vector2.zero, screenVel: vel) };

            // Act
            ParticleIntegrator.Step(buffer, 1, Vector2.zero, 0f, DT);

            // Assert
            Assert.That(buffer[0].screenPos.x, Is.EqualTo(200f * DT).Within(1e-2f));
        }

        // ── Helpers ─────────────────────────────────────────

        private static ChatReactionsParticle MakeWorldParticle(
            float age = 0f, float lifetime = 2f,
            Vector3 pos = default, Vector3 vel = default) =>
            new ()
            {
                pos = pos,
                vel = vel,
                age = age,
                lifetime = lifetime,
                alive = 1,
            };

        private static ChatReactionsUiParticle MakeUiParticle(
            float age = 0f, float lifetime = 2f,
            Vector2 screenPos = default, Vector2 screenVel = default) =>
            new ()
            {
                screenPos = screenPos,
                screenVel = screenVel,
                age = age,
                lifetime = lifetime,
                alive = 1,
            };
    }
}
