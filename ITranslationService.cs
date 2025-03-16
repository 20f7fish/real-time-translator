using System;
using System.Threading.Tasks;

namespace RealtimeTranslator
{
    public interface ITranslationService : IDisposable
    {
        Task<string> TranslateAsync(string text, string fromLanguage, string toLanguage);
    }
} 