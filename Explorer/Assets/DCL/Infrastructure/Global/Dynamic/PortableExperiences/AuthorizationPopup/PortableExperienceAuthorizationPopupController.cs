using Cysharp.Threading.Tasks;
using MVC;
using System.Collections.Generic;
using System.Threading;

namespace PortableExperiences.Controller
{
    /// <summary>
    ///     Popup that discloses the capabilities requested by a scene-spawned Portable Experience
    ///     and awaits explicit user approval before the Portable Experience is allowed to run.
    /// </summary>
    public class PortableExperienceAuthorizationPopupController : ControllerBase<PortableExperienceAuthorizationPopupView, PortableExperienceAuthorizationPopupController.Params>
    {
        public PortableExperienceAuthorizationPopupController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public static async UniTask<bool> RequestAuthorizationAsync(IMVCManager mvcManager, string portableExperienceName, IReadOnlyList<string> permissions, CancellationToken ct)
        {
            var completionSource = new UniTaskCompletionSource<bool>();

            var commandParams = new Params(portableExperienceName, permissions, completionSource);
            mvcManager.ShowAndForget(IssueCommand(commandParams), ct);
            if (ct.IsCancellationRequested) return false;

            return await commandParams.GetResultAsync(ct);
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.AuthorizeButton.onClick.AddListener(OnAuthorizeButtonClick);
            viewInstance.DenyButton.onClick.AddListener(OnDenyButtonClick);
        }

        protected override void OnViewShow() =>
            viewInstance!.Setup(inputData.PortableExperienceName, inputData.Permissions);

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            await viewInstance!.WaitChoiceAsync();

        private void OnAuthorizeButtonClick() =>
            inputData.CompletionSource.TrySetResult(true);

        private void OnDenyButtonClick() =>
            inputData.CompletionSource.TrySetResult(false);

        public readonly struct Params
        {
            public readonly string PortableExperienceName;

            public readonly IReadOnlyList<string> Permissions;

            public readonly UniTaskCompletionSource<bool> CompletionSource;

            public Params(string portableExperienceName, IReadOnlyList<string> permissions, UniTaskCompletionSource<bool> completionSource)
            {
                PortableExperienceName = portableExperienceName;
                Permissions = permissions;
                CompletionSource = completionSource;
            }

            public async UniTask<bool> GetResultAsync(CancellationToken ct) =>
                await CompletionSource.Task.AttachExternalCancellation(ct);
        }
    }
}
