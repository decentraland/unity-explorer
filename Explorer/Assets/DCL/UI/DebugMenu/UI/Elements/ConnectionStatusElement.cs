using System;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.UI.DebugMenu.UI.Elements
{
    [UxmlElement]
    public partial class ConnectionStatusElement : VisualElement
    {
        private const string USS_BLOCK = "connection-status";
        private const string USS_DESCRIPTION_CONTAINER = USS_BLOCK + "__description-container";
        private const string USS_TITLE = USS_BLOCK + "__title";
        private const string USS_SUBTITLE = USS_BLOCK + "__subtitle";
        private const string USS_STATUS = USS_BLOCK + "__status";
        private const string USS_STATUS_NONE = USS_STATUS + "--none";
        private const string USS_STATUS_LOST = USS_STATUS + "--lost";
        private const string USS_STATUS_POOR = USS_STATUS + "--poor";
        private const string USS_STATUS_GOOD = USS_STATUS + "--good";
        private const string USS_STATUS_EXCELLENT = USS_STATUS + "--excellent";

        [UxmlAttribute]
        public string Title
        {
            get => title.text;
            set => title.text = value;
        }

        [UxmlAttribute]
        public string Subtitle
        {
            get => subtitle.text;
            set => subtitle.text = value;
        }

        private readonly Label title;
        private readonly Label subtitle;
        private readonly Label status;

        public ConnectionStatusElement()
        {
            AddToClassList(USS_BLOCK);

            var descriptionContainer = new VisualElement { name = "description-container" };
            Add(descriptionContainer);
            descriptionContainer.AddToClassList(USS_DESCRIPTION_CONTAINER);

            {
                descriptionContainer.Add(title = new Label("Title") { name = "title" });
                title.AddToClassList(USS_TITLE);

                descriptionContainer.Add(subtitle = new Label("Subtitle goes here") { name = "subtitle" });
                subtitle.AddToClassList(USS_SUBTITLE);
            }

            Add(status = new Label("GOOD") { name = "status" });
            status.AddToClassList(USS_STATUS);

            SetStatus(ConnectionStatus.Good);
        }

        public void SetStatus(ConnectionStatus cs)
        {
            status.RemoveModifiers();
            status.text = cs.ToString().ToUpperInvariant();

            switch (cs)
            {
                case ConnectionStatus.None: status.AddToClassList(USS_STATUS_NONE); break;
                case ConnectionStatus.Lost: status.AddToClassList(USS_STATUS_LOST); break;
                case ConnectionStatus.Poor: status.AddToClassList(USS_STATUS_POOR); break;
                case ConnectionStatus.Good: status.AddToClassList(USS_STATUS_GOOD); break;
                case ConnectionStatus.Excellent: status.AddToClassList(USS_STATUS_EXCELLENT); break;
                default: throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }
    }
}
