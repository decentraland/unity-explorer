using System;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace DCL.SDKComponents.InputModifier.Components
{
    /// <summary>
    ///     Allows systems to modify different aspects of the player input.
    /// </summary>
    public struct InputModifierComponent
    {
        [Flags]
        private enum ModifierId
        {
            None,
            Walk = 1,
            Jog = 1 << 1,
            Run = 1 << 2,
            Jump = 1 << 3,
            Emote = 1 << 4,
            DoubleJump = 1 << 5,
            Gliding = 1 << 6,
            All = 1 << 31
        }

        private ModifierId disabledMask;

        public bool EverythingEnabled => disabledMask == ModifierId.None;

        /// <summary>
        ///     When set to true, disables all related properties (Walk, Jog, Run, Jump, Emote).
        ///     When set to false, only this property is affected.
        /// </summary>
        public bool DisableAll
        {
            get => (disabledMask & ModifierId.All) != 0;
            set => disabledMask = value ? disabledMask | ModifierId.All : disabledMask & ~ModifierId.All;
        }

        /// <summary>
        ///     Gets or sets the DisableWalk property.
        ///     <para>Get: Returns true if DisableAll is true or if DisableWalk is explicitly set to true.</para>
        ///     <para>Set: Explicitly sets the DisableWalk property to the given value.</para>
        /// </summary>
        public bool DisableWalk
        {
            get => IsDisabled(ModifierId.Walk);
            set => disabledMask = value ? disabledMask | ModifierId.Walk : disabledMask & ~ModifierId.Walk;
        }

        /// <summary>
        ///     Gets or sets the DisableJog property.
        ///     <para>Get: Returns true if DisableAll is true or if DisableJog is explicitly set to true.</para>
        ///     <para>Set: Explicitly sets the DisableJog property to the given value.</para>
        /// </summary>
        public bool DisableJog
        {
            get => IsDisabled(ModifierId.Jog);
            set => disabledMask = value ? disabledMask | ModifierId.Jog : disabledMask & ~ModifierId.Jog;
        }

        /// <summary>
        ///     Gets or sets the DisableRun property.
        ///     <para>Get: Returns true if DisableAll is true or if DisableRun is explicitly set to true.</para>
        ///     <para>Set: Explicitly sets the DisableRun property to the given value.</para>
        /// </summary>
        public bool DisableRun
        {
            get => IsDisabled(ModifierId.Run);
            set => disabledMask = value ? disabledMask | ModifierId.Run : disabledMask & ~ModifierId.Run;
        }

        /// <summary>
        ///     Gets or sets the DisableJump property.
        ///     <para>Get: Returns true if DisableAll is true or if DisableJump is explicitly set to true.</para>
        ///     <para>Set: Explicitly sets the DisableJump property to the given value.</para>
        /// </summary>
        public bool DisableJump
        {
            get => IsDisabled(ModifierId.Jump);
            set => disabledMask = value ? disabledMask | ModifierId.Jump : disabledMask & ~ModifierId.Jump;
        }

        /// <summary>
        ///     Gets or sets the DisableEmote property.
        ///     <para>Get: Returns true if DisableAll is true or if DisableEmote is explicitly set to true.</para>
        ///     <para>Set: Explicitly sets the DisableEmote property to the given value.</para>
        /// </summary>
        public bool DisableEmote
        {
            get => IsDisabled(ModifierId.Emote);
            set => disabledMask = value ? disabledMask | ModifierId.Emote : disabledMask & ~ModifierId.Emote;
        }

        /// <summary>
        ///     Gets or sets the DisableDoubleJump property.
        ///     <para>Get: Returns true if DisableAll is true or if DisableDoubleJump is explicitly set to true.</para>
        ///     <para>Set: Explicitly sets the DisableDoubleJump property to the given value.</para>
        /// </summary>
        public bool DisableDoubleJump
        {
            get => IsDisabled(ModifierId.DoubleJump);
            set => disabledMask = value ? disabledMask | ModifierId.DoubleJump : disabledMask & ~ModifierId.DoubleJump;
        }

        /// <summary>
        ///     Gets or sets the DisableGliding property.
        ///     <para>Get: Returns true if DisableAll is true or if DisableGliding is explicitly set to true.</para>
        ///     <para>Set: Explicitly sets the DisableGliding property to the given value.</para>
        /// </summary>
        public bool DisableGliding
        {
            get => IsDisabled(ModifierId.Gliding);
            set => disabledMask = value ? disabledMask | ModifierId.Gliding : disabledMask & ~ModifierId.Gliding;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsDisabled(ModifierId modifier) => (disabledMask & (ModifierId.All | modifier)) != 0;

        public void RemoveAllModifiers() =>
            disabledMask = ModifierId.None;
    }
}
