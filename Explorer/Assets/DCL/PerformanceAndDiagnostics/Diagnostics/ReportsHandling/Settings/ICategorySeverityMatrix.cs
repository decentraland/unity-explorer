using UnityEngine;

namespace DCL.Diagnostics
{
    public interface ICategorySeverityMatrix
    {
        bool IsEnabled(string category, LogType severity);
    }
}
