namespace ECS.StreamableLoading.Components.Common
{
    public struct CommonLoadingArguments
    {
        public const int TIMEOUT = 60;

        public string URL;
        public int Attempts;
        public int Timeout;

        public CommonLoadingArguments(string url, int timeout = TIMEOUT) : this()
        {
            URL = url;
            Timeout = timeout;
        }
    }
}
