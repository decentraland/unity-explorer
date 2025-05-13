using System;

namespace DCL.UI.InputFieldFormatting
{
    public interface ITextFormatter
    {
        string FormatText(ReadOnlySpan<char> text);
    }
}
