using DCL.DebugUtilities;
using System;
using System.Collections.Generic;

namespace DCL.Quality.Runtime
{
    /// <summary>
    ///     Allows to modify the given quality setting at runtime
    /// </summary>
    public interface IQualitySettingRuntime : IDisposable
    {
        bool IsActive { get; }

        void IDisposable.Dispose() { }

        /// <summary>
        ///     Modifies persistent state to manipulate the quality setting at runtime
        /// </summary>
        /// <param name="active"></param>
        void SetActive(bool active);

        /// <summary>
        ///     When the preset is applied it overrides all the values stored persistently
        /// </summary>
        /// <param name="preset"></param>
        void ApplyPreset(QualitySettingsAsset.QualityCustomLevel preset);

        /// <summary>
        ///     Based on the preset that was active on game initialization and the persistent state restores the final values
        /// </summary>
        /// <param name="currentPreset"></param>
        void RestoreState(QualitySettingsAsset.QualityCustomLevel currentPreset);

        void AddDebugView(DebugWidgetBuilder debugWidgetBuilder, List<Action> onUpdate);
    }
}
