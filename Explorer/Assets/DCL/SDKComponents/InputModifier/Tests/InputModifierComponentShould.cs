using DCL.SDKComponents.InputModifier.Components;
using NUnit.Framework;

namespace DCL.SDKComponents.InputModifier.Tests
{
    public class InputModifierComponentShould
    {
        public enum ModifierFlag
        {
            Walk,
            Jog,
            Run,
            Jump,
            Emote,
            DoubleJump,
            Gliding,
            All,
        }

        [Test]
        public void BeEverythingEnabled_ByDefault()
        {
            var component = new InputModifierComponent();
            Assert.IsTrue(component.EverythingEnabled);
        }

        [TestCase(ModifierFlag.Walk)]
        [TestCase(ModifierFlag.Jog)]
        [TestCase(ModifierFlag.Run)]
        [TestCase(ModifierFlag.Jump)]
        [TestCase(ModifierFlag.Emote)]
        [TestCase(ModifierFlag.DoubleJump)]
        [TestCase(ModifierFlag.Gliding)]
        [TestCase(ModifierFlag.All)]
        public void NotBeEverythingEnabled_WhenAnyFlagDisabled(ModifierFlag flag)
        {
            var component = new InputModifierComponent();
            Set(ref component, flag, true);

            Assert.IsFalse(component.EverythingEnabled);
        }

        [TestCase(ModifierFlag.Walk)]
        [TestCase(ModifierFlag.Jog)]
        [TestCase(ModifierFlag.Run)]
        [TestCase(ModifierFlag.Jump)]
        [TestCase(ModifierFlag.Emote)]
        [TestCase(ModifierFlag.DoubleJump)]
        [TestCase(ModifierFlag.Gliding)]
        public void ReportEveryFlagDisabled_WhenDisableAllIsSet(ModifierFlag flag)
        {
            // DisableAll must dominate every individual flag getter.
            var component = new InputModifierComponent { DisableAll = true };

            Assert.IsTrue(Get(in component, flag));
        }

        [TestCase(ModifierFlag.Walk)]
        [TestCase(ModifierFlag.Jog)]
        [TestCase(ModifierFlag.Run)]
        [TestCase(ModifierFlag.Jump)]
        [TestCase(ModifierFlag.Emote)]
        [TestCase(ModifierFlag.DoubleJump)]
        [TestCase(ModifierFlag.Gliding)]
        public void SetOnlyTheTargetedFlag_WhenDisabledIndividually(ModifierFlag flag)
        {
            // Each setter must map to its own bit and leave the others untouched.
            var component = new InputModifierComponent();
            Set(ref component, flag, true);

            foreach (ModifierFlag other in System.Enum.GetValues(typeof(ModifierFlag)))
            {
                if (other == flag) continue;

                Assert.IsFalse(Get(in component, other), $"{other} must stay enabled when only {flag} is disabled");
            }
        }

        [Test]
        public void ClearOnlyTheTargetedFlag_WhenSetBackToFalse()
        {
            var component = new InputModifierComponent();
            component.DisableWalk = true;
            component.DisableGliding = true;

            component.DisableWalk = false;

            Assert.IsFalse(component.DisableWalk);
            Assert.IsTrue(component.DisableGliding);
        }

        [Test]
        public void BeEverythingEnabled_AfterRemoveAllModifiers()
        {
            var component = new InputModifierComponent();
            component.DisableWalk = true;
            component.DisableGliding = true;
            component.DisableDoubleJump = true;

            component.RemoveAllModifiers();

            Assert.IsTrue(component.EverythingEnabled);
            Assert.IsFalse(component.DisableWalk);
            Assert.IsFalse(component.DisableGliding);
            Assert.IsFalse(component.DisableDoubleJump);
        }

        private static void Set(ref InputModifierComponent component, ModifierFlag flag, bool value)
        {
            switch (flag)
            {
                case ModifierFlag.Walk:
                    component.DisableWalk = value;
                    break;
                case ModifierFlag.Jog:
                    component.DisableJog = value;
                    break;
                case ModifierFlag.Run:
                    component.DisableRun = value;
                    break;
                case ModifierFlag.Jump:
                    component.DisableJump = value;
                    break;
                case ModifierFlag.Emote:
                    component.DisableEmote = value;
                    break;
                case ModifierFlag.DoubleJump:
                    component.DisableDoubleJump = value;
                    break;
                case ModifierFlag.Gliding:
                    component.DisableGliding = value;
                    break;
                case ModifierFlag.All:
                    component.DisableAll = value;
                    break;
            }
        }

        private static bool Get(in InputModifierComponent component, ModifierFlag flag) =>
            flag switch
            {
                ModifierFlag.Walk => component.DisableWalk,
                ModifierFlag.Jog => component.DisableJog,
                ModifierFlag.Run => component.DisableRun,
                ModifierFlag.Jump => component.DisableJump,
                ModifierFlag.Emote => component.DisableEmote,
                ModifierFlag.DoubleJump => component.DisableDoubleJump,
                ModifierFlag.Gliding => component.DisableGliding,
                ModifierFlag.All => component.DisableAll,
                _ => false,
            };
    }
}
