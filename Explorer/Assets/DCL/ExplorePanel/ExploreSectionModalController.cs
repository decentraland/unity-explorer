using Cysharp.Threading.Tasks;
using DCL.UI;
using MVC;
using System.Collections.Generic;
using System.Threading;

namespace DCL.ExplorePanel
{
    /// <summary>
    ///     Shows one explore section on its own over the panel that opened it, borrowing the single view the explore panel hosts.
    /// </summary>
    public class ExploreSectionModalController : ControllerBase<ExploreSectionModalView, ExploreSectionModalParameter>
    {
        private readonly IReadOnlyDictionary<ExploreSections, IHostableSection> sections;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private IHostableSection section => sections[inputData.Section];

        public ExploreSectionModalController(ViewFactoryMethod viewFactory, IReadOnlyDictionary<ExploreSections, IHostableSection> sections) : base(viewFactory)
        {
            this.sections = sections;
        }

        protected override void OnBeforeViewShow() =>
            section.AttachTo(viewInstance!.SectionHost);

        protected override void OnViewShow()
        {
            IHostableSection shown = section;
            shown.Activate();

            // The view may still sit on the OUT frame of its last hide.
            shown.ResetAnimator();
        }

        protected override void OnViewClose()
        {
            IHostableSection shown = section;

            // Another host may already have claimed the view back.
            if (shown.CurrentHost != viewInstance!.SectionHost) return;

            shown.Deactivate();
            shown.AttachToHome();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            viewInstance!.CloseButton.OnClickAsync(ct);
    }

    public readonly struct ExploreSectionModalParameter
    {
        public readonly ExploreSections Section;

        public ExploreSectionModalParameter(ExploreSections section)
        {
            Section = section;
        }
    }
}
