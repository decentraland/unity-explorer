using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DCL.UI.InputFieldFormatting
{
    public interface ITextFormatter
    {
        string FormatText(ReadOnlySpan<char> text);

        IReadOnlyList<(TextFormatMatchType, Match)> GetMatches(string text);
    }

    public enum TextFormatMatchType
    {
        URL,
        SCENE,
        WORLD,
        NAME
    }
}
