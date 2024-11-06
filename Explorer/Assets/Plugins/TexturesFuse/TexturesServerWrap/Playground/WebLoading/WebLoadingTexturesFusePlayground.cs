using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using Plugins.TexturesFuse.TexturesServerWrap.Playground.Displays;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground.WebLoading
{
    public class WebLoadingTexturesFusePlayground : MonoBehaviour
    {
        [Serializable]
        public class TextureOption
        {
            public string url = string.Empty;
            public TextureType textureType = TextureType.Albedo;
        }

        [Header("Dependencies")]
        [SerializeField] private AbstractDebugDisplay display = null!;
        [Header("Config")]
        [SerializeField] private TexturesFusePlayground.Options options = new ();
        [SerializeField] private List<TextureOption> textureOptionList = new ();
        [SerializeField] private bool debugOutputFromNative;
        [Header("Debug")]
        [SerializeField] private int currentIndex = -1;
        [SerializeField] private Texture2D? current;

        private IWebRequestController webRequests = null!;
        private ITexturesFuse fuse = null!;

        [ContextMenu(nameof(Start))]
        private void Start()
        {
            display.EnsureNotNull();

            fuse = new Unzips.TexturesFuse(options.InitOptions, options, debugOutputFromNative);
            webRequests = IWebRequestController.DEFAULT;

            Next();
        }

        [ContextMenu(nameof(Next))]
        public void Next()
        {
            IncreaseIndex();
            LoadAndDisplayAsync(textureOptionList[currentIndex]!).Forget();
        }

        private async UniTaskVoid LoadAndDisplayAsync(TextureOption option)
        {
            var texture = await webRequests.GetTextureAsync(
                new CommonArguments(
                    URLAddress.FromString(option.url)
                ),
                new GetTextureArguments(fuse, option.textureType),
                new GetTextureWebRequest.CreateTextureOp(TextureWrapMode.Clamp, FilterMode.Bilinear),
                destroyCancellationToken,
                ReportCategory.UNSPECIFIED
            );

            current = texture.Texture;
            display.Display(current);
        }

        private void IncreaseIndex()
        {
            currentIndex++;

            if (currentIndex < 0 || currentIndex >= textureOptionList.Count)
                currentIndex = 0;
        }
    }
}
