using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace RealtimeTranslator
{
    public class YoudaoTranslationService : ITranslationService
    {
        private readonly string _appKey;
        private readonly string _appSecret;
        private readonly HttpClient _httpClient;

        public YoudaoTranslationService(IConfiguration configuration)
        {
            _appKey = configuration["Youdao:AppKey"] ?? throw new ArgumentNullException("Youdao:AppKey");
            _appSecret = configuration["Youdao:AppSecret"] ?? throw new ArgumentNullException("Youdao:AppSecret");
            _httpClient = new HttpClient();
        }

        public async Task<string> TranslateAsync(string text, string fromLanguage, string toLanguage)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var salt = DateTime.Now.Ticks.ToString();
            var curtime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds().ToString();
            var input = text.Length <= 20 ? text : text[..10] + text.Length + text[^10..];
            var sign = CalculateSign(input, salt, curtime);

            var parameters = new Dictionary<string, string>
            {
                { "q", text },
                { "from", fromLanguage },
                { "to", toLanguage },
                { "appKey", _appKey },
                { "salt", salt },
                { "sign", sign },
                { "signType", "v3" },
                { "curtime", curtime }
            };

            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync("https://openapi.youdao.com/api", content);
            var responseString = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(responseString);
            var root = document.RootElement;

            if (root.TryGetProperty("errorCode", out var errorCode) && errorCode.GetString() != "0")
            {
                throw new Exception($"有道翻译服务错误: {errorCode.GetString()}");
            }

            if (root.TryGetProperty("translation", out var translation))
            {
                return translation.EnumerateArray().First().GetString() ?? string.Empty;
            }

            throw new Exception("有道翻译服务错误: 未找到翻译结果");
        }

        private string CalculateSign(string input, string salt, string curtime)
        {
            var signStr = _appKey + input + salt + curtime + _appSecret;
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(signStr);
            var hash = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }

    public class YoudaoResponse
    {
        [JsonPropertyName("translation")]
        public List<string>? Translation { get; set; }

        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; set; }
    }
} 