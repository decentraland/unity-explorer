namespace DCL.Translation.Processors
{
    // TokType defines the "category" of a piece of a chat message.
    public enum TokType
    {
        Text,           // General text to be translated
        Tag,            // A Rich Text tag like <color> or </link>
        Protected,      // Text that must NOT be translated (e.g., links, worlds, coordinates...)
        Emoji,          // An emoji grapheme
        Number,         // A protected number, date, or currency
        Command         // A slash command like /goto
    }

    // Tok (Token) represents a single,
    // processed segment of a chat message.
    public readonly struct Tok
    {
        public readonly int Id;
        public readonly TokType Type;
        public readonly string Value;

        public Tok(int id, TokType t, string v)
        {
            Id = id;
            Type = t;
            Value = v;
        }

        // A helper to create a new token with
        // a modified value, preserving other properties.
        public Tok With(string newV)
        {
            return new Tok(Id, Type, newV);
        }
    }
}