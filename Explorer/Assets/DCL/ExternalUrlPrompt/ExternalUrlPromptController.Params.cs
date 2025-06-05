using System;

namespace DCL.ExternalUrlPrompt
{
    public partial class ExternalUrlPromptController
    {
        public struct Params
        {
            public Uri Uri { get; }

            public Params(Uri uri)
            {
                Uri = uri;
            }
            public Params(string url)
            {
                Uri = Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ? uri : null;
            }
        }
    }
}
