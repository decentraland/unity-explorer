using System;

namespace DCL.Platforms
{
    public class ConstPlatform : IPlatform
    {
        private readonly IPlatform.Kind kind;

        public ConstPlatform(IPlatform.Kind kind)
        {
            this.kind = kind;
        }

        public IPlatform.Kind CurrentPlatform() =>
            kind;

        public void Quit()
        {
            throw new NotSupportedException("This is const platform, you can't quit it.");
        }
    }
}
