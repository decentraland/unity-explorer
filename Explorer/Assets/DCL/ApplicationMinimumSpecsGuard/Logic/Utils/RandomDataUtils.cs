#if UNITY_EDITOR
using System;
using System.Text;

public static class RandomDataUtils
{
    private static readonly Random random = new ();

    private static readonly string[] firstNames =
    {
        "Satoshi", "Ada", "Sergey", "Vitalik", "Hal", "Dorothy"
    };

    private static readonly string[] lastNames =
    {
        "Nakamoto", "Lovelace", "Brin", "Buterin", "Finney", "Vaughan"
    };

    private static readonly string[] locations =
    {
        "Genesis Plaza", "Aetheria", "Vegas City", "WonderZone", "CryptoValley"
    };

    private static readonly string[] wearables =
    {
        "Cyber-Visor", "Dragon-Scale Armor", "Meteorite Staff", "Comms Headset", "Gravity Boots"
    };

    private static readonly string[] genericWords =
    {
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit", "virtual", "reality", "blockchain", "metaverse"
    };

    public static string GetRandomWords(int minWords, int maxWords)
    {
        var sb = new StringBuilder();
        int wordCount = random.Next(minWords, maxWords + 1);
        for (int i = 0; i < wordCount; i++)
        {
            // Mix DCL context with generic words for variety
            string word = random.Next(0, 3) == 0 ? wearables[random.Next(wearables.Length)] : genericWords[random.Next(genericWords.Length)];
            sb.Append(word);
            if (i < wordCount - 1) sb.Append(" ");
        }

        return sb.ToString();
    }

    public static string GetRandomDclName()
    {
        string firstName = firstNames[random.Next(firstNames.Length)];
        string lastName = lastNames[random.Next(lastNames.Length)];
        return $"{firstName} {lastName}";
    }

    public static string GetRandomHardwareName()
    {
        return $"Decentraland-Hyperion-Quantum-Processor-X-Series-Super-GPU-{random.Next(1000, 9999)}-(Virtual-Core-Optimized)";
    }
}
#endif