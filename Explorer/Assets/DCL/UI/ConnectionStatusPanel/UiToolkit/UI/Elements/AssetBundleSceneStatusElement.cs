using DCL.Ipfs;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.UI.ConnectionStatusPanel
{
    [UxmlElement]
    public partial class AssetBundleSceneStatusElement : VisualElement
    {
        private const string USS_BLOCK = "connection-status";
        private const string USS_DESCRIPTION_CONTAINER = USS_BLOCK + "__description-container";
        private const string USS_TITLE = USS_BLOCK + "__title";
        private const string USS_SUBTITLE = USS_BLOCK + "__subtitle";
        private const string USS_STATUS = USS_BLOCK + "__status";
        private const string USS_STATUS_BAD = USS_STATUS + "--lost";
        private const string USS_STATUS_MEDIUM = USS_STATUS + "--poor";
        private const string USS_STATUS_GOOD = USS_STATUS + "--good";

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

        public AssetBundleSceneStatusElement()
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

            Add(status = new Label("Updating") { name = "status" });
            status.AddToClassList(USS_STATUS);

            SetStatus(AssetBundleRegistryEnum.fallback);
        }

        public void SetStatus(AssetBundleRegistryEnum abStatus)
        {
            status.RemoveModifiers();
            switch (abStatus)
            {
                case AssetBundleRegistryEnum.pending:
                    status.text = "FAILED";
                    status.AddToClassList(USS_STATUS_BAD);
                    break;
                case AssetBundleRegistryEnum.fallback:
                    status.text = "UPDATING";
                    status.AddToClassList(USS_STATUS_MEDIUM);
                    break;
                case AssetBundleRegistryEnum.complete:
                    status.text = "LATEST";
                    status.AddToClassList(USS_STATUS_GOOD);
                    break;
            }
        }
    }
}
