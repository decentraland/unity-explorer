using UnityEngine;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Enables every category at every severity, ignoring any configured entry
    /// </summary>
    public class AllEnabledCategorySeverityMatrix : ICategorySeverityMatrix
    {
        public bool IsEnabled(string category, LogType severity) => true;
    }
}
