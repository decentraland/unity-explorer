namespace CommunicationData.URLHelpers
{
    public static class URNExtensions
    {
        // TODO: would be ideal that urns are represented in a custom type so we keep extensions specific
        public static string ShortenURN(this string input, int parts)
        {
            int index = -1;

            for (var i = 0; i < parts; i++)
            {
                index = input.IndexOf(':', index + 1);
                if (index == -1) break;
            }

            return index != -1 ? input[..index] : input;
        }
    }
}
