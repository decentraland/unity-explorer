using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Donations.UI
{
    public class DonationsPanelView : ViewBase, IView
    {
        [field: Header("References")]
        [field: SerializeField] private Button closeButton { get; set; } = null!;

        private readonly UniTask[] closingTasks = new UniTask[2];

        public void SetLoadingState(bool active)
        {

        }

        public UniTask[] GetClosingTasks(UniTask controllerTask, CancellationToken ct)
        {
            closingTasks[0] = closeButton.OnClickAsync(ct);
            closingTasks[1] = controllerTask;

            return closingTasks;
        }
    }
}
