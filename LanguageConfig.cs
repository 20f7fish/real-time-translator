namespace RealtimeTranslator
{
    public static class LanguageConfig
    {
        public static readonly Dictionary<string, string> SupportedLanguages = new()
        {
            { "auto", "自动检测" },
            { "zh", "中文" },
            { "en", "英语" },
            { "ja", "日语" },
            { "ko", "韩语" },
            { "fr", "法语" },
            { "es", "西班牙语" },
            { "ru", "俄语" },
            { "de", "德语" },
            { "it", "意大利语" },
            { "tr", "土耳其语" },
            { "pt", "葡萄牙语" },
            { "vi", "越南语" },
            { "id", "印尼语" },
            { "th", "泰语" },
            { "ms", "马来语" },
            { "ar", "阿拉伯语" },
            { "hi", "印地语" }
        };

        public static void DisplayLanguages()
        {
            Console.WriteLine("\n支持的语言列表：");
            Console.WriteLine("代码\t语言");
            Console.WriteLine("----\t----");
            foreach (var lang in SupportedLanguages)
            {
                Console.WriteLine($"{lang.Key}\t{lang.Value}");
            }
        }

        public static bool IsValidLanguage(string code)
        {
            return SupportedLanguages.ContainsKey(code);
        }
    }
} 