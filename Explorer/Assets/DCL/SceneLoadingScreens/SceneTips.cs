using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using RichTypes;
using System;
using System.Collections.Generic;
using UnityEngine;

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
            public string Key { get; }
            public string Title { get; }
            public string Body { get; }
            public ContextualLocalizedAsset<Sprite>? Image { get; }

            public Tip(string key, string title, string body, ContextualLocalizedAsset<Sprite>? image)
            {
                Key = key;
                Title = title;
                Body = body;
                Image = image;
            }

            public Tip(string key)
            {
                Key = key;
                Title = string.Empty;
                Body = string.Empty;
                Image = null;
            }

            public async UniTask<LoadedTip> LoadAsync()
            {
                Weak<Sprite> image = Weak<Sprite>.Null;
                if (Image != null) image = await Image.AssetAsync();
                return new LoadedTip(Key, Title, Body, image);
            }
        }

        public struct LoadedTip
        {
            public string Key { get; }
            public string Title { get; }
            public string Body { get; }
            public Weak<Sprite> Image { get; }

            public LoadedTip(string key, string title, string body, Weak<Sprite> image)
            {
                Key = key;
                Title = title;
                Body = body;
                Image = image;
            }
        }
    }
}
