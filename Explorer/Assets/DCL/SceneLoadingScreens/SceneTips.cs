using CommunicationData.URLHelpers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SceneLoadingScreens
{
    public struct SceneTips
    {
        public TimeSpan Duration { get; }
        public bool Random { get; }
        public IReadOnlyList<Tip> Tips { get; }

        public SceneTips(TimeSpan duration, bool random, IReadOnlyList<Tip> tips)
        {
            Duration = duration;
            Random = random;
            Tips = tips;
        }

        public struct Tip
        {
            public string Title { get; }
            public string Body { get; }
            public Texture2D? Image { get; }

            public Tip(string title, string body, Texture2D? image)
            {
                Title = title;
                Body = body;
                Image = image;
            }
        }
    }
}
