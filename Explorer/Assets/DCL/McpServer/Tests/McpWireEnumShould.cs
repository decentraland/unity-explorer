using DCL.McpServer.Core;
using NUnit.Framework;
using System.Diagnostics.CodeAnalysis;

namespace DCL.McpServer.Tests
{
    public class McpWireEnumShould
    {
        /// <summary>
        ///     Deliberately mixes every member-naming style the snake_case conversion must handle — PascalCase,
        ///     an acronym run, SCREAMING and SCREAMING_SNAKE — mirroring the real wire enums (CameraMode.FirstPerson,
        ///     CameraMode.SDKCamera, MovementKind.WALK). The members are read only through
        ///     reflection by McpWireEnum, hence the suppressions.
        /// </summary>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        private enum Casing
        {
            FirstPerson,
            WALK,
            DroneView,
            SDKCamera,
            WAIT_TICK,
        }

        [Test]
        public void DeriveSnakeCaseWireNamesFromMemberNames()
        {
            Assert.That(McpWireEnum<Casing>.WIRE_NAMES, Is.EqualTo(new[] { "first_person", "walk", "drone_view", "sdk_camera", "wait_tick" }));
        }

        [Test]
        public void ParseAWireNameBackToItsMember()
        {
            Assert.That(McpWireEnum<Casing>.TryParse("drone_view", out Casing value), Is.True);
            Assert.That(value, Is.EqualTo(Casing.DroneView));
        }

        [Test]
        public void RejectAMemberNameThatIsNotInWireForm()
        {
            Assert.That(McpWireEnum<Casing>.TryParse("DroneView", out _), Is.False);
        }

        [Test]
        public void FormatAMemberAsItsWireName()
        {
            Assert.That(McpWireEnum<Casing>.ToWire(Casing.SDKCamera), Is.EqualTo("sdk_camera"));
        }

        [Test]
        public void NarrowWireNamesToASubsetOfMembers()
        {
            Assert.That(McpWireEnum<Casing>.WireNamesOf(new[] { Casing.WALK, Casing.FirstPerson }), Is.EqualTo(new[] { "walk", "first_person" }));
        }
    }
}
