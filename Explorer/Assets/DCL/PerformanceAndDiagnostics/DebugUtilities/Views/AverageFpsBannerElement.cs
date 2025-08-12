using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class AverageFpsBannerElement : DebugElementBase<AverageFpsBannerElement, AverageFpsBannerDef>, IBindable, INotifyValueChanged<AverageFpsBannerData>
    {
        private const string USS_FPS_VALUE_LABEL_NAME = "FpsValue";
        private const string USS_MS_VALUE_LABEL_NAME = "MsValue";

        private const string COLLECTING_TEXT = "collecting…";
        private const string MS_FORMAT = "({0} ms)";

        private const string USS_BANNER_CLASS = "avg-fps-banner";
        private const string USS_BANNER_SEVERITY_GOOD_CLASS = "avg-fps-banner--good";
        private const string USS_BANNER_SEVERITY_NORMAL_CLASS = "avg-fps-banner--normal";
        private const string USS_BANNER_SEVERITY_BAD_CLASS = "avg-fps-banner--bad";

        private enum Severity
        {
            Good,
            Normal,
            Bad,
        }

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
            fpsValueLabel = this.Q<Label>(USS_FPS_VALUE_LABEL_NAME);
            msLabel = this.Q<Label>(USS_MS_VALUE_LABEL_NAME);

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
                fpsValueLabel.text = COLLECTING_TEXT;
                msLabel.text = string.Empty;
                SetSeverityClass(Severity.Good);
                return;
            }

            fpsValueLabel.style.display = DisplayStyle.Flex;
            fpsValueLabel.text = data.Fps.ToString("F1");
            msLabel.style.display = DisplayStyle.Flex;
            msLabel.text = string.Format(MS_FORMAT, data.Ms.ToString("F1"));

            Severity severity = data.Fps < definition.BadFpsThreshold
                ? Severity.Bad
                : data.Fps < definition.NormalFpsThreshold ? Severity.Normal : Severity.Good;

            SetSeverityClass(severity);
        }

        private void SetSeverityClass(Severity severity)
        {
            RemoveFromClassList(USS_BANNER_SEVERITY_GOOD_CLASS);
            RemoveFromClassList(USS_BANNER_SEVERITY_NORMAL_CLASS);
            RemoveFromClassList(USS_BANNER_SEVERITY_BAD_CLASS);

            AddToClassList(USS_BANNER_CLASS);

            switch (severity)
            {
                case Severity.Bad:
                    AddToClassList(USS_BANNER_SEVERITY_BAD_CLASS);
                    fpsValueLabel.style.color = new StyleColor(new UnityEngine.Color(0.905f, 0.298f, 0.235f));
                    break;
                case Severity.Normal:
                    AddToClassList(USS_BANNER_SEVERITY_NORMAL_CLASS);
                    fpsValueLabel.style.color = new StyleColor(new UnityEngine.Color(0.945f, 0.769f, 0.059f));
                    break;
                default:
                    AddToClassList(USS_BANNER_SEVERITY_GOOD_CLASS);
                    fpsValueLabel.style.color = new StyleColor(new UnityEngine.Color(0.180f, 0.800f, 0.443f));
                    break;
            }
        }

        public new class UxmlFactory : UxmlFactory<AverageFpsBannerElement> { }
    }
}
