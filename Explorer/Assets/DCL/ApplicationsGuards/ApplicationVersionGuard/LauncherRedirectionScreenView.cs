using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    public class LauncherRedirectionScreenView : ViewBase, IView
    {
        private const string DESCRIPTION_TEMPLATE =
            "Your current Explorer version {0} is outdated (latest version is {1}). "
            + "Please update to the latest version to continue using it and access all new features and improvements.";

        [field: SerializeField]
        public Button CloseWithLauncherButton { get; private set; } = null!;

        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;

        [SerializeField] private TMP_Text Description  = null!;

        public void SetVersions(string current, string latest)
        {
            Description.text = string.Format(DESCRIPTION_TEMPLATE, current, latest);
        }
    }
}
