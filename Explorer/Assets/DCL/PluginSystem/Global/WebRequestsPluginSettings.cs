using DCL.WebRequests.Analytics;
using System;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class WebRequestsPluginSettings : IDCLPluginSettings
    {
        [Header("Editor Only")]
        [SerializeField]
        public WebRequestsContainerParams webRequestsContainerParams = new ();
    }
}
