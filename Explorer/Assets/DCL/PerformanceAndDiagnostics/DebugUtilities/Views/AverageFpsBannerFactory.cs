using DCL.DebugUtilities;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    internal class AverageFpsBannerFactory : IDebugElementFactory<AverageFpsBannerElement, AverageFpsBannerDef>
    {
        public AverageFpsBannerElement Create(AverageFpsBannerDef def)
        {
            var el = new AverageFpsBannerElement();

            el.AddToClassList("avg-fps-banner");

            var row = new VisualElement();
            row.AddToClassList("avg-fps-banner__row");
            el.Add(row);

            var left = new Label("Average FPS:") { name = "LeftLabel" };
            left.AddToClassList("avg-fps-banner__left-label");
            row.Add(left);

            var right = new VisualElement() { name = "Right" };
            right.AddToClassList("avg-fps-banner__right");
            row.Add(right);

            var fps = new Label("collecting…") { name = "FpsValue" };
            fps.AddToClassList("avg-fps-banner__fps");
            right.Add(fps);

            var ms = new Label("") { name = "MsValue" };
            ms.AddToClassList("avg-fps-banner__ms");
            right.Add(ms);

            el.Initialize(def);
            return el;
        }
    }
}


