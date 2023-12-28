namespace CommunicationData.URLHelpers
{
    public readonly struct URN
    {
        private readonly string urn;

        public URN(string urn)
        {
            this.urn = urn;
        }

        public bool Equals(URN other) =>
            Equals(other.urn);

        public bool Equals(string other) =>
            urn == other;

        public override bool Equals(object obj) =>
            obj is URN other && Equals(other);

        public override string ToString() =>
            urn;

        public override int GetHashCode() =>
            urn != null ? urn.GetHashCode() : 0;

        public string Shorten(int parts)
        {
            int index = -1;

            for (var i = 0; i < parts; i++)
            {
                index = urn.IndexOf(':', index + 1);
                if (index == -1) break;
            }

            return index != -1 ? urn[..index] : urn;
        }

        public static implicit operator string(URN urn) =>
            urn.urn;

        public static implicit operator URN(string urn) =>
            new (urn);
    }
}
