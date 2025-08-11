using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class AverageFpsBannerElement : DebugElementBase<AverageFpsBannerElement, AverageFpsBannerDef>, INotifyValueChanged<float>, IBindable
    {
        private Label fpsValueLabel;
        private Label msLabel;

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
            fpsValueLabel = this.Q<Label>("FpsValue");
            msLabel = this.Q<Label>("MsValue");

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
                fpsValueLabel.text = "collecting…";
                msLabel.text = string.Empty;
                SetSeverityClass("ok");
                return;
            }

            float ms = avgNs * NS_TO_MS;
            float fps = 1f / (avgNs * NS_TO_SEC);

            fpsValueLabel.style.display = DisplayStyle.Flex;
            fpsValueLabel.text = fps.ToString("F1") + " fps";
            msLabel.style.display = DisplayStyle.Flex;
            msLabel.text = "(" + ms.ToString("F1") + " ms)";

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

            switch (id)
            {
                case "error":
                    AddToClassList("avg-fps-banner--error");
                    fpsValueLabel.style.color = new StyleColor(new UnityEngine.Color(0.905f, 0.298f, 0.235f));
                    break;
                case "warn":
                    AddToClassList("avg-fps-banner--warn");
                    fpsValueLabel.style.color = new StyleColor(new UnityEngine.Color(0.945f, 0.769f, 0.059f));
                    break;
                default:
                    AddToClassList("avg-fps-banner--ok");
                    fpsValueLabel.style.color = new StyleColor(new UnityEngine.Color(0.180f, 0.800f, 0.443f));
                    break;
            }
        }

        public new class UxmlFactory : UxmlFactory<AverageFpsBannerElement> { }
    }
}
