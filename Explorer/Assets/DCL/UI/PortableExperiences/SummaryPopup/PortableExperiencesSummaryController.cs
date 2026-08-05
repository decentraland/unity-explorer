using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.Input.Component;
using MVC;
using System.Threading;

namespace DCL.UI.PortableExperiences.SummaryPopup
{
    public class PortableExperiencesSummaryController : ControllerBase<PortableExperiencesSummaryView>
    {
        private static readonly InputMapComponent.Kind[] BLOCKED_INPUTS = { InputMapComponent.Kind.Shortcuts, InputMapComponent.Kind.InWorldCamera };

        private readonly IInputBlock inputBlock;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public PortableExperiencesSummaryController(
            ViewFactoryMethod viewFactory,
            IInputBlock inputBlock)
            : base(viewFactory)
        {
            this.inputBlock = inputBlock;
        }

        public override void Dispose() { }

        private void DisableShortcutsInput() =>
            inputBlock.Disable(BLOCKED_INPUTS);

        private void RestoreInput() =>
            inputBlock.Enable(BLOCKED_INPUTS);

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            viewInstance!.closeButton.OnClickAsync(ct);
    }
}
