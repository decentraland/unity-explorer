using Best.HTTP;
using Best.HTTP.Caching;

namespace DCL.WebRequests.HTTP2
{
    public class Http2WebRequestController
    {
        private readonly HTTPCache cache;

        public Http2WebRequestController(HTTPCache cache)
        {
            this.cache = cache;
        }

    }
}
