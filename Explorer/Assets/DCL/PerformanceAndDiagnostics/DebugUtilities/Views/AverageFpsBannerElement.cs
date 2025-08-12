using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class AverageFpsBannerElement : DebugElementBase<AverageFpsBannerElement, AverageFpsBannerDef>, IBindable, INotifyValueChanged<AverageFpsBannerData>
    {
        private Label fpsValueLabel;
        private Label msLabel;

        private AverageFpsBannerData currentDisplayData;

        public IBinding binding { get; set; }
        public string bindingPath { get; set; }

        AverageFpsBannerData INotifyValueChanged<AverageFpsBannerData>.value
        {
            get => currentDisplayData;
            set
            {
                currentDisplayData = value;
                UpdateVisualsFromDisplayData(value);
            }
        }

        protected override void ConnectBindings()
        {
            fpsValueLabel = this.Q<Label>("FpsValue");
            msLabel = this.Q<Label>("MsValue");

            // Connect the mandatory precomputed display binding
            definition.AvgDisplayBinding.Connect(this);

            // Immediately push current value so UI shows something first frame
            ((INotifyValueChanged<AverageFpsBannerData>)this).SetValueWithoutNotify(definition.AvgDisplayBinding.Value);
        }

        void INotifyValueChanged<AverageFpsBannerData>.SetValueWithoutNotify(AverageFpsBannerData newValue)
        {
            currentDisplayData = newValue;
            UpdateVisualsFromDisplayData(newValue);
        }


        private void UpdateVisualsFromDisplayData(AverageFpsBannerData data)
        {
            if (data.Fps <= 0)
            {
                fpsValueLabel.text = "collecting…";
                msLabel.text = string.Empty;
                SetSeverityClass("ok");
                return;
            }

            fpsValueLabel.style.display = DisplayStyle.Flex;
            fpsValueLabel.text = data.Fps.ToString("F1");
            msLabel.style.display = DisplayStyle.Flex;
            msLabel.text = "(" + data.Ms.ToString("F1") + " ms)";

            string severity = data.Fps < definition.BadFpsThreshold
                ? "bad"
                : data.Fps < definition.NormalFpsThreshold ? "normal" : "good";

            SetSeverityClass(severity);
        }

        private void SetSeverityClass(string id)
        {
            RemoveFromClassList("avg-fps-banner--good");
            RemoveFromClassList("avg-fps-banner--normal");
            RemoveFromClassList("avg-fps-banner--bad");

            AddToClassList("avg-fps-banner");

            switch (id)
            {
                case "bad":
                    AddToClassList("avg-fps-banner--bad");
                    fpsValueLabel.style.color = new StyleColor(new UnityEngine.Color(0.905f, 0.298f, 0.235f));
                    break;
                case "normal":
                    AddToClassList("avg-fps-banner--normal");
                    fpsValueLabel.style.color = new StyleColor(new UnityEngine.Color(0.945f, 0.769f, 0.059f));
                    break;
                default:
                    AddToClassList("avg-fps-banner--good");
                    fpsValueLabel.style.color = new StyleColor(new UnityEngine.Color(0.180f, 0.800f, 0.443f));
                    break;
            }
        }

        public new class UxmlFactory : UxmlFactory<AverageFpsBannerElement> { }
    }
}
