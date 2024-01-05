using UnityEngine;

namespace DCL.Browser
{
    public class UnityAppWebBrowser : IWebBrowser
    {
        public void OpenUrl(string url)
        {
            Application.OpenURL(url);
        }
    }
}
