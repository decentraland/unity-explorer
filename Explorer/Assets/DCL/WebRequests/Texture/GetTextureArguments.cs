using DCL.Diagnostics;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;

namespace DCL.WebRequests
{
    public readonly struct GetTextureArguments
    {
        //TODO remove IsReadable
        public readonly bool IsReadable;
        public readonly ITexturesUnzip TexturesUnzip;

        public GetTextureArguments(bool isReadable, ITexturesUnzip texturesUnzip)
        {
            if (isReadable)
            {
                ReportHub.LogError(ReportData.UNSPECIFIED, "Required readable texture");
                throw new InvalidOperationException();
            }

            IsReadable = isReadable;
            TexturesUnzip = texturesUnzip;
        }
    }
}
