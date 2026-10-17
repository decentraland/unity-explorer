namespace Utility
{
    public static class ByteSize
    {
        private const ulong KB = 1024;
        private const ulong MB = KB * 1024;
        private const ulong GB = MB * 1024;
        private const ulong TB = GB * 1024;

        public static string ToReadableString(ulong bytes)
        {
            if (bytes < KB) return $"{bytes} B";
            if (bytes < MB) return $"{(double)bytes / KB:0.##} KB";
            if (bytes < GB) return $"{(double)bytes / MB:0.##} MB";
            if (bytes < TB) return $"{(double)bytes / GB:0.##} GB";
            return $"{(double)bytes / TB:0.##} TB";
        }
    }
}
