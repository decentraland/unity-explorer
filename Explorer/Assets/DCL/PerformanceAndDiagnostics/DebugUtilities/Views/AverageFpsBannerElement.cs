using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class AverageFpsBannerElement : DebugElementBase<AverageFpsBannerElement, DCL.DebugUtilities.AverageFpsBannerDef>, INotifyValueChanged<float>, IBindable
    {
        private Label fpsLabel;
        private Label msLabel;
        private Label legacyTitle;
        private Label legacyDetail;

        private float currentAvgNs;

        public IBinding binding { get; set; }
        public string bindingPath { get; set; }

        float INotifyValueChanged<float>.value
        {
            get => currentAvgNs;
            set
            {
                currentAvgNs = value;
                UpdateVisualsFromAvgNs(value);
            }
        }

        protected override void ConnectBindings()
        {
            fpsLabel = this.Q<Label>("FpsValue");
            msLabel = this.Q<Label>("MsValue");
            legacyTitle = this.Q<Label>("Title");
            legacyDetail = this.Q<Label>("Detail");

            // Ensure initial text states exist to avoid null deref if factory layout differs
            // If using the legacy UXML (Title/Detail), hide them to avoid overlapping
            if (legacyTitle != null) legacyTitle.style.display = DisplayStyle.None;
            if (legacyDetail != null) legacyDetail.style.display = DisplayStyle.None;

            var rightContainer = this.Q<VisualElement>("Right");

            // Create missing labels into the Right container
            if (fpsLabel == null)
                fpsLabel = new Label("collecting…") { name = "FpsValue" };

            if (msLabel == null)
                msLabel = new Label("") { name = "MsValue" };

            if (rightContainer != null)
            {
                if (fpsLabel.hierarchy.parent != rightContainer) rightContainer.Add(fpsLabel);
                if (msLabel.hierarchy.parent != rightContainer) rightContainer.Add(msLabel);
            }
            else
            {
                if (fpsLabel.hierarchy.parent != this) Add(fpsLabel);
                if (msLabel.hierarchy.parent != this) Add(msLabel);
            }

            // The binding drives the avg frame time in ns
            definition.AvgFrameTimeNsBinding.Connect(this);
            // Immediately push current value so UI shows something first frame
            ((INotifyValueChanged<float>)this).SetValueWithoutNotify(definition.AvgFrameTimeNsBinding.Value);
        }

        void INotifyValueChanged<float>.SetValueWithoutNotify(float newValue)
        {
            currentAvgNs = newValue;
            UpdateVisualsFromAvgNs(newValue);
        }

        private void UpdateVisualsFromAvgNs(float avgNs)
        {
            // convert: ns -> ms -> fps
            const float NS_TO_MS = 1e-6f;
            const float NS_TO_SEC = 1e-9f;

            if (avgNs <= 0)
            {
                if (fpsLabel != null) fpsLabel.text = "collecting…";
                if (msLabel != null) msLabel.text = string.Empty;
            if (legacyTitle != null) legacyTitle.text = "Average FPS:";
                if (legacyDetail != null) legacyDetail.text = string.Empty;
                SetSeverityClass("ok");
                return;
            }

            float ms = avgNs * NS_TO_MS;
            float fps = 1f / (avgNs * NS_TO_SEC);

            if (fpsLabel != null) fpsLabel.text = fps.ToString("F1") + " fps";
            if (msLabel != null) msLabel.text = "(" + ms.ToString("F1") + " ms)";
            if (legacyTitle != null) legacyTitle.text = "Average FPS:"; // keep static label
            if (legacyDetail != null) legacyDetail.text = fps.ToString("F1") + " fps  (" + ms.ToString("F1") + " ms)";

            string severity = fps < definition.ErrorFpsThreshold
                ? "error"
                : fps < definition.WarningFpsThreshold ? "warn" : "ok";

            SetSeverityClass(severity);
        }

        private void SetSeverityClass(string id)
        {
            RemoveFromClassList("avg-fps-banner--ok");
            RemoveFromClassList("avg-fps-banner--warn");
            RemoveFromClassList("avg-fps-banner--error");

            AddToClassList("avg-fps-banner");
            // Ensure right exists and is flex
            var right = this.Q<VisualElement>("Right");
            if (right != null) right.style.display = DisplayStyle.Flex;

            switch (id)
            {
                case "error":
                    AddToClassList("avg-fps-banner--error");
                    if (fpsLabel != null) fpsLabel.style.color = new StyleColor(new UnityEngine.Color(0.905f, 0.298f, 0.235f));
                    break;
                case "warn":
                    AddToClassList("avg-fps-banner--warn");
                    if (fpsLabel != null) fpsLabel.style.color = new StyleColor(new UnityEngine.Color(0.945f, 0.769f, 0.059f));
                    break;
                default:
                    AddToClassList("avg-fps-banner--ok");
                    if (fpsLabel != null) fpsLabel.style.color = new StyleColor(new UnityEngine.Color(0.180f, 0.800f, 0.443f));
                    break;
            }
        }

        public new class UxmlFactory : UxmlFactory<AverageFpsBannerElement> { }
    }
}


