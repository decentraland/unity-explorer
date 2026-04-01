namespace DCL.Chat.ChatReactions
{
    internal static class RandomExtensions
    {
        public static float NextFloat(this System.Random rng, float min, float max) =>
            (float)(min + rng.NextDouble() * (max - min));
    }
}
