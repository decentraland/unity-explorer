using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugOngoingWebRequestWidget : DebugElementBase<DebugOngoingWebRequestWidget, DebugOngoingWebRequestDef>, INotifyValueChanged<DebugOngoingWebRequestDef.DataSource>
    {
        // 5 seconds
        private const ulong WARNING_LEVEL_NS = 5 * 1_000_000_000UL;

        // 10 seconds
        private const ulong ERROR_LEVEL_NS = 10 * 1_000_000_000UL;

        private const string WARNING_STYLE = "warning";
        private const string ERROR_STYLE = "error";

        private readonly EventCallback<PointerUpEvent, int> onHyperlinkClicked;

        private ListView? listView;

        private new DebugOngoingWebRequestDef.DataSource? dataSource;

        public DebugOngoingWebRequestDef.DataSource value
        {
            get => dataSource;

            set
            {
                if (dataSource != value)
                {
                    if (dataSource != null)
                        dataSource.Updated = null;

                    dataSource = value;
                    listView!.itemsSource = dataSource.Requests;
                    dataSource.Updated = OnUpdate;
                }
            }
        }

        public DebugOngoingWebRequestWidget()
        {
            onHyperlinkClicked = HyperlinkOnPointerUp;
        }

        protected override void ConnectBindings()
        {
            listView = this.Q<ListView>();

            // Template is already bound from UXML
            listView.bindItem = BindItem;
            listView.unbindItem = UnbindItem;

            definition.Binding.Connect(this);
        }

        private void OnUpdate() =>
            listView!.RefreshItems();

        private void UnbindItem(VisualElement element, int index)
        {
            Label url = element.Q<Label>("URL")!;
            url.UnregisterCallback(onHyperlinkClicked);
        }

        private void BindItem(VisualElement element, int index)
        {
            Label method = element.Q<Label>("Method")!;
            Label url = element.Q<Label>("URL")!;
            Label duration = element.Q<Label>("Duration")!;

            DebugOngoingWebRequestDef.DebugWebRequestInfo data = dataSource.Requests[index];

            url.text = data.ShortenedUrl;
            url.RegisterCallback(onHyperlinkClicked, index);

            method.text = data.Request.method;
            duration.text = DebugLongMarkerElement.FormatValue(data.Duration, DebugLongMarkerDef.Unit.TimeNanoseconds);

            switch (data.Duration)
            {
                case >= ERROR_LEVEL_NS:
                    element.AddToClassList(ERROR_STYLE); break;
                case >= WARNING_LEVEL_NS:
                    element.AddToClassList(WARNING_STYLE); break;
                default:
                    element.RemoveFromClassList(ERROR_STYLE);
                    element.RemoveFromClassList(WARNING_STYLE);
                    break;
            }
        }

        private void HyperlinkOnPointerUp(PointerUpEvent evt, int index)
        {
            DebugOngoingWebRequestDef.DebugWebRequestInfo data = dataSource.Requests[index];
            Application.OpenURL(data.Request.url);
        }

        void INotifyValueChanged<DebugOngoingWebRequestDef.DataSource>.SetValueWithoutNotify(DebugOngoingWebRequestDef.DataSource newValue) =>
            value = newValue;

        public new class UxmlFactory : UxmlFactory<DebugOngoingWebRequestWidget, UxmlTraits> { }
    }
}
