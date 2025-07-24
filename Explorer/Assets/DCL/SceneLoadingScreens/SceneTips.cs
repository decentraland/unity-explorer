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

        public struct Tip
        {
            public string Title { get; }
            public string Body { get; }
            public Sprite? Image { get; }

            public Tip(string title, string body, Sprite? image)
            {
                Title = title;
                Body = body;
                Image = image;
            }
        }
    }
}
