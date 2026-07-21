using DCL.Browser;
using System;

namespace DCL.ExternalUrlPrompt
{
    public partial class ExternalUrlPromptController
    {
        public struct Params
        {
            public Uri Uri { get; }

            public Params(string url)
            {
                Uri = ExternalUrlPolicy.IsWebScheme(url) && Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                    ? uri
                    : null;
            }
        }
    }
}
