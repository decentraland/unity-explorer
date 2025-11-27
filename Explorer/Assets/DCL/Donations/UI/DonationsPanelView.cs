using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using MVC;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Donations.UI
{
    public class DonationsPanelView : ViewBase, IView
    {
        [field: Header("References")]
        [field: SerializeField] private Button closeButton { get; set; } = null!;
        [field: SerializeField] private Button cancelButton { get; set; } = null!;
        [field: SerializeField] private SkeletonLoadingView loadingView { get; set; } = null!;

        private readonly UniTask[] closingTasks = new UniTask[3];

        public void SetLoadingState(bool active)
        {
            if (active)
                loadingView.ShowLoading();
            else
                loadingView.HideLoading();
        }

        public void ConfigurePanel(Profile? profile,
            ISceneFacade sceneFacade,
            float currentBalance,
            float suggestedDonationAmount,
            float manaUsdPrice,
            ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            
        }

        public UniTask[] GetClosingTasks(UniTask controllerTask, CancellationToken ct)
        {
            closingTasks[0] = closeButton.OnClickAsync(ct);
            closingTasks[1] = cancelButton.OnClickAsync(ct);
            closingTasks[2] = controllerTask;

            return closingTasks;
        }
    }
}
