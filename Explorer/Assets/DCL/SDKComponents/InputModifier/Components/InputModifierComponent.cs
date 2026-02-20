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
            NONE,
            WALK = 1,
            JOG = 1 << 1,
            RUN = 1 << 2,
            JUMP = 1 << 3,
            EMOTE = 1 << 4,
            DOUBLE_JUMP = 1 << 5,
            GLIDING = 1 << 6,
            ALL = 1 << 31
        }

        private ModifierId disabledMask;

        public bool EverythingEnabled => disabledMask == ModifierId.NONE;

        /// <summary>
        ///     When set to true, disables all related properties (Walk, Jog, Run, Jump, Emote).
        ///     When set to false, only this property is affected.
        /// </summary>
        public bool DisableAll
        {
            get => (disabledMask & ModifierId.ALL) != 0;
            set => disabledMask = value ? disabledMask | ModifierId.ALL : disabledMask & ~ModifierId.ALL;
        }

        /// <summary>
        ///     Gets or sets the DisableWalk property.
        ///     <para>Get: Returns true if DisableAll is true or if DisableWalk is explicitly set to true.</para>
        ///     <para>Set: Explicitly sets the DisableWalk property to the given value.</para>
        /// </summary>
        public bool DisableWalk
        {
            get => IsDisabled(ModifierId.WALK);
            set => disabledMask = value ? disabledMask | ModifierId.WALK : disabledMask & ~ModifierId.WALK;
        }

        /// <summary>
        ///     Gets or sets the DisableJog property.
        ///     <para>Get: Returns true if DisableAll is true or if DisableJog is explicitly set to true.</para>
        ///     <para>Set: Explicitly sets the DisableJog property to the given value.</para>
        /// </summary>
        public bool DisableJog
        {
            get => IsDisabled(ModifierId.JOG);
            set => disabledMask = value ? disabledMask | ModifierId.JOG : disabledMask & ~ModifierId.JOG;
        }

        /// <summary>
        ///     Gets or sets the DisableRun property.
        ///     <para>Get: Returns true if DisableAll is true or if DisableRun is explicitly set to true.</para>
        ///     <para>Set: Explicitly sets the DisableRun property to the given value.</para>
        /// </summary>
        public bool DisableRun
        {
            get => IsDisabled(ModifierId.RUN);
            set => disabledMask = value ? disabledMask | ModifierId.RUN : disabledMask & ~ModifierId.RUN;
        }

        /// <summary>
        ///     Gets or sets the DisableJump property.
        ///     <para>Get: Returns true if DisableAll is true or if DisableJump is explicitly set to true.</para>
        ///     <para>Set: Explicitly sets the DisableJump property to the given value.</para>
        /// </summary>
        public bool DisableJump
        {
            get => IsDisabled(ModifierId.JUMP);
            set => disabledMask = value ? disabledMask | ModifierId.JUMP : disabledMask & ~ModifierId.JUMP;
        }

        /// <summary>
        ///     Gets or sets the DisableEmote property.
        ///     <para>Get: Returns true if DisableAll is true or if DisableEmote is explicitly set to true.</para>
        ///     <para>Set: Explicitly sets the DisableEmote property to the given value.</para>
        /// </summary>
        public bool DisableEmote
        {
            get => IsDisabled(ModifierId.EMOTE);
            set => disabledMask = value ? disabledMask | ModifierId.EMOTE : disabledMask & ~ModifierId.EMOTE;
        }

        /// <summary>
        ///     Gets or sets the DisableDoubleJump property.
        ///     <para>Get: Returns true if DisableAll is true or if DisableDoubleJump is explicitly set to true.</para>
        ///     <para>Set: Explicitly sets the DisableDoubleJump property to the given value.</para>
        /// </summary>
        public bool DisableDoubleJump
        {
            get => IsDisabled(ModifierId.DOUBLE_JUMP);
            set => disabledMask = value ? disabledMask | ModifierId.DOUBLE_JUMP : disabledMask & ~ModifierId.DOUBLE_JUMP;
        }

        /// <summary>
        ///     Gets or sets the DisableGliding property.
        ///     <para>Get: Returns true if DisableAll is true or if DisableGliding is explicitly set to true.</para>
        ///     <para>Set: Explicitly sets the DisableGliding property to the given value.</para>
        /// </summary>
        public bool DisableGliding
        {
            get => IsDisabled(ModifierId.GLIDING);
            set => disabledMask = value ? disabledMask | ModifierId.GLIDING : disabledMask & ~ModifierId.GLIDING;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsDisabled(ModifierId modifier) => (disabledMask & (ModifierId.ALL | modifier)) != 0;

        public void RemoveAllModifiers() =>
            disabledMask = ModifierId.NONE;
    }
}
