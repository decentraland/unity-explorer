using UnityEngine;

namespace Diagnostics.ReportsHandling
{
    public interface ICategorySeverityMatrix
    {
        bool IsEnabled(string category, LogType severity);
    }
}
