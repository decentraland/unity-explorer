using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.CharacterMotion.Tests
{
    public class ApplyDragShould
    {
        private ICharacterControllerSettings settings;


        public void SetUp()
        {
            settings = Substitute.For<ICharacterControllerSettings>();
            settings.AirDrag.Returns(2);
            settings.JumpVelocityDrag.Returns(2);
        }


        public void DontApplyWhenGrounded()
        {
            var rigidTransform = new CharacterRigidTransform();
            rigidTransform.MoveVelocity.Velocity = Vector3.forward;
            rigidTransform.IsGrounded = true;

            ApplyAirDrag.Execute(settings, ref rigidTransform, 0.05f);

            Assert.IsTrue(Mathf.Approximately(1, rigidTransform.MoveVelocity.Velocity.z), "Velocity didn't change");
        }


        public void ReduceVelocity()
        {
            var rigidTransform = new CharacterRigidTransform();
            rigidTransform.MoveVelocity.Velocity = Vector3.forward;
            rigidTransform.IsGrounded = false;

            ApplyAirDrag.Execute(settings, ref rigidTransform, 0.05f);

            Assert.IsTrue(rigidTransform.MoveVelocity.Velocity.magnitude < 1, "Velocity is lower");
        }


        public void DontAffectVerticalVelocity()
        {
            var rigidTransform = new CharacterRigidTransform();
            rigidTransform.MoveVelocity.Velocity = Vector3.one;
            rigidTransform.IsGrounded = false;

            ApplyAirDrag.Execute(settings, ref rigidTransform, 0.05f);

            Assert.IsTrue(Mathf.Approximately(1, rigidTransform.MoveVelocity.Velocity.y), "Vertical velocity is same");
            Assert.IsTrue(rigidTransform.MoveVelocity.Velocity.x < 1, "Horizontal Z velocity is less");
            Assert.IsTrue(rigidTransform.MoveVelocity.Velocity.x < 1, "Horizontal X velocity is less");
        }
    }
}
