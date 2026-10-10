using DCL.Browser;
using System;

namespace DCL.ExternalUrlPrompt
{
    public partial class ExternalUrlPromptController
    {
        public struct Params
        {
            /// <summary>Null when <c>url</c> is not an absolute http/https URL — callers must null-check.</summary>
            public Uri? Uri { get; }

            public Params(string url)
            {
                Uri = ExternalUrlPolicy.IsWebScheme(url) && Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                    ? uri
                    : null;
            }
        }
    }
}
