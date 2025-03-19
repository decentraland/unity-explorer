using Best.HTTP;

namespace DCL.WebRequests.HTTP2
{
    public interface IHttp2TypedWebRequest
    {
        HTTPRequest Request { get; }
    }
}
