using System;

namespace DCL.WebRequests
{
    public class YetAnotherHttpWebRequestException : WebRequestException
    {
        public YetAnotherHttpWebRequestException(YetAnotherWebRequest webRequest, Exception exception) : base(webRequest, exception)
        {
            Text = exception.Message;
        }
    }
}
