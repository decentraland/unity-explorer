namespace DCL.WebRequests
{
    public partial interface IWebRequest
    {
        void SetTimeout(int timeout);

        void SetRequestHeader(string name, string value);
    }
}
