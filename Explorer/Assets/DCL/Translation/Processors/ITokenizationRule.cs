using System.Collections.Generic;

namespace DCL.Translation.Processors
{
    namespace DCL.Translation.Service.Processing
    {
        public interface ITokenizationRule
        {
            List<Tok> Process(List<Tok> tokens);
        }
    }
}