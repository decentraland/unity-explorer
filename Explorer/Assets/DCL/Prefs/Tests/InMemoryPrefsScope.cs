using System;
using System.Reflection;

namespace DCL.Prefs.Tests
{
    /// <summary>
    ///     Swaps the store behind <see cref="DCLPlayerPrefs" /> for an in-memory one, and puts the original back
    ///     on dispose. Reflection is the only seam: the store is chosen once at startup and never re-initialized.
    /// </summary>
    public sealed class InMemoryPrefsScope : IDisposable
    {
        private static readonly FieldInfo PREFS_FIELD =
            typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static)!;

        private readonly object? originalPrefs;

        public InMemoryPrefsScope()
        {
            originalPrefs = PREFS_FIELD.GetValue(null);
            Reset();
        }

        public void Dispose()
        {
            PREFS_FIELD.SetValue(null, originalPrefs);
        }

        /// <summary>
        ///     Discards everything stored so far, standing in for an installation that has never run before.
        /// </summary>
        public void Reset()
        {
            PREFS_FIELD.SetValue(null, new InMemoryDCLPlayerPrefs());
        }
    }
}
