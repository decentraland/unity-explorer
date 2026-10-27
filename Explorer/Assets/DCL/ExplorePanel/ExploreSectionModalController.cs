using Cysharp.Threading.Tasks;
using DCL.Places;
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
        private readonly IPlacesSectionOpener placesSections;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private IHostableSection section => sections[inputData.Section];

        public ExploreSectionModalController(ViewFactoryMethod viewFactory, IReadOnlyDictionary<ExploreSections, IHostableSection> sections, IPlacesSectionOpener placesSections) : base(viewFactory)
        {
            this.sections = sections;
            this.placesSections = placesSections;
        }

        protected override void OnBeforeViewShow() =>
            section.AttachTo(viewInstance!.SectionHost);

        protected override void OnViewShow()
        {
            IHostableSection shown = section;
            shown.Activate();

            // The view may still sit on the OUT frame of its last hide.
            shown.ResetAnimator();

            // Activation resets the places to Browse.
            if (inputData.PlacesSection != null)
                placesSections.OpenSection(inputData.PlacesSection.Value);
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
        public readonly PlacesSection? PlacesSection;

        public ExploreSectionModalParameter(ExploreSections section, PlacesSection? placesSection = null)
        {
            Section = section;
            PlacesSection = placesSection;
        }
    }
}
