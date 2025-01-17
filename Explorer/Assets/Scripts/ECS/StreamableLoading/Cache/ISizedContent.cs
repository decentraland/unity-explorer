namespace ECS.StreamableLoading.Cache
{
    public interface ISizedContent
    {
        long ByteSize { get; }
    }

    public static class ByteSize
    {
        public static string ToReadableString(this ISizedContent sizedContent) =>
            Utility.ByteSize.ToReadableString((ulong)sizedContent.ByteSize);
    }
}
