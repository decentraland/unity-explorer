namespace DCL.WebRequests
{
    public readonly struct GetTextureArguments
    {
        public readonly bool IsReadable;

        public GetTextureArguments(bool isReadable)
        {
            IsReadable = isReadable;
        }
    }
}
