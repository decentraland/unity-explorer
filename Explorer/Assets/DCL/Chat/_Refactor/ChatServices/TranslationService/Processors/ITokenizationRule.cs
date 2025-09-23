using System.Collections.Generic;
using DCL.Chat.ChatServices.TranslationService.Utilities;

namespace DCL.Chat.ChatServices.TranslationService.Processors
{
    namespace DCL.Translation.Service.Processing
    {
        public interface ITokenizationRule
        {
            List<Tok> Process(List<Tok> tokens);
        }
    }
}