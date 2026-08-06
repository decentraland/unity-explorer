using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.BugReporting.UI
{
    public class PerformanceIssuePromptView : ViewBase, IView
    {
        [field: SerializeField] internal Toggle DontShowAgainToggle { get; private set; } = null!;
        [field: SerializeField] internal Button CloseButton { get; private set; } = null!;
        [field: SerializeField] internal Button ReportBugButton { get; private set; } = null!;
    }
}
