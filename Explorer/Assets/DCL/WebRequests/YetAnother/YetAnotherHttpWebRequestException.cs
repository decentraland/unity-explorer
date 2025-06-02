using System;
using System.Collections.Generic;

namespace DCL.WebRequests
{
    public class YetAnotherHttpWebRequestException : WebRequestException
    {
        public YetAnotherHttpWebRequestException(YetAnotherWebRequest webRequest, Exception exception) : base(webRequest, exception)
        {
            Text = exception.Message;
        }

        public override Dictionary<string, string>? ResponseHeaders => base.ResponseHeaders;
    }
}
