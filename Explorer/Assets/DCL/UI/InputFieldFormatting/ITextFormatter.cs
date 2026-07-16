using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DCL.UI.InputFieldFormatting
{
    public interface ITextFormatter
    {
        string FormatText(ReadOnlySpan<char> text);

        void GetMatches(string text, List<(TextFormatMatchType, Match)> matchesResult);
    }

    public enum TextFormatMatchType
    {
        URL,
        SCENE,
        WORLD,
        NAME
    }
}
