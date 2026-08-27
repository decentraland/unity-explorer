using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.BugReporting.UI
{
    public class PerformanceIssuePromptView : ViewBase, IView
    {
        [field: SerializeField] public Toggle DontShowAgainToggle { get; private set; } = null!;
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;
        [field: SerializeField] public Button ReportBugButton { get; private set; } = null!;
    }
}
