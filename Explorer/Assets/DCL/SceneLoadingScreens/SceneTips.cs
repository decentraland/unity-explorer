using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility.Ownership;

namespace DCL.SceneLoadingScreens
{
    public struct SceneTips
    {
        public TimeSpan Duration { get; }
        public bool Random { get; }
        public IList<Tip> Tips { get; }

        public SceneTips(TimeSpan duration, bool random, IList<Tip> tips)
        {
            Duration = duration;
            Random = random;
            Tips = tips;
        }

        public void Release()
        {
            foreach (Tip tip in Tips) tip.Image?.Release();
        }

        public struct Tip
        {
            public string Title { get; }
            public string Body { get; }
            public ContextualLocalizedAsset<Sprite>? Image { get; }

            public Tip(string title, string body, ContextualLocalizedAsset<Sprite>? image)
            {
                Title = title;
                Body = body;
                Image = image;
            }

            public async UniTask<LoadedTip> LoadAsync()
            {
                Weak<Sprite> image = Weak<Sprite>.Null;
                if (Image != null) image = await Image.AssetAsync();
                return new LoadedTip(Title, Body, image);
            }
        }

        public struct LoadedTip
        {
            public LoadedTip(string title, string body, Weak<Sprite> image)
            {
                Title = title;
                Body = body;
                Image = image;
            }

            public string Title { get; }
            public string Body { get; }
            public Weak<Sprite> Image { get; }
        }
    }
}
