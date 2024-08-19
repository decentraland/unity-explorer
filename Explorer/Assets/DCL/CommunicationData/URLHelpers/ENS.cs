using DCL.CommunicationData.URLHelpers;
using System;

namespace CommunicationData.URLHelpers
{
    public readonly struct ENS
    {
        private readonly string ens;

        public ENS(string ens)
        {
            this.ens = ens;
        }

        public bool IsValid => !string.IsNullOrEmpty(ens) && ens.IsEns();

        public bool Equals (ENS other) => Equals(other.ens);

        public bool Equals(string other) =>
            string.Equals(ens, other, StringComparison.OrdinalIgnoreCase);

        public override string ToString() => ens;
    }
}
