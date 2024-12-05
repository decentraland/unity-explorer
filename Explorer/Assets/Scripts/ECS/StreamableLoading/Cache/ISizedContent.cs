namespace ECS.StreamableLoading.Cache
{
    public interface ISizedContent
    {
        long ByteSize { get; }
    }

    public static class ByteSize
    {
        private const long KB = 1024;
        private const long MB = KB * 1024;
        private const long GB = MB * 1024;
        private const long TB = GB * 1024;

        public static string ToReadableString(long bytes)
        {
            if (bytes < KB) return $"{bytes} B";
            if (bytes < MB) return $"{(double)bytes / KB:0.##} KB";
            if (bytes < GB) return $"{(double)bytes / MB:0.##} MB";
            if (bytes < TB) return $"{(double)bytes / GB:0.##} GB";
            return $"{(double)bytes / TB:0.##} TB";
        }

        public static string ToReadableString(this ISizedContent sizedContent) =>
            ToReadableString(sizedContent.ByteSize);
    }
}
