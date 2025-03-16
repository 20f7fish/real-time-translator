using Microsoft.Extensions.Configuration;

namespace RealtimeTranslator
{
    public enum TranslationServiceType
    {
        Youdao,
        Google,
        Microsoft
    }

    public static class TranslationServiceFactory
    {
        public static ITranslationService CreateTranslationService(TranslationServiceType serviceType, IConfiguration? config = null)
        {
            return serviceType switch
            {
                TranslationServiceType.Youdao => new YoudaoTranslationService(config ?? throw new ArgumentNullException(nameof(config))),
                TranslationServiceType.Google => new GoogleTranslationService(),
                TranslationServiceType.Microsoft => new MicrosoftTranslationService(config ?? throw new ArgumentNullException(nameof(config))),
                _ => throw new ArgumentException("不支持的翻译服务类型", nameof(serviceType))
            };
        }
    }
} 