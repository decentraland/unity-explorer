using System;
using System.Collections.Generic;
using UnityEngine;

namespace Global.AppArgs
{
    /// <summary>
    ///     Build-time-baked list of world ENS names whose deep links may carry the whitelisted-realm dev params
    ///     (see <see cref="DeepLinkAllowlist" />). Populated from the <c>DEEPLINK_WHITELISTED_WORLDS</c> GitHub secret
    ///     by <c>Editor.CloudBuild</c> during the cloud build; empty (loopback-only) in local and Editor runs.
    ///     Lives under a <c>Resources</c> folder so <see cref="DeepLinkAllowlist" /> can load it synchronously at
    ///     cold-start, before DI / Addressables are available.
    /// </summary>
    public class DeepLinkWorldWhitelist : ScriptableObject
    {
        public const string RESOURCE_NAME = "DeepLinkWorldWhitelist";

        [SerializeField] private string[] worlds = Array.Empty<string>();

        public IReadOnlyList<string> Worlds => worlds;

        public void SetWorlds(string[]? value) =>
            worlds = value ?? Array.Empty<string>();
    }
}
