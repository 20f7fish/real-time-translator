using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace RealtimeTranslator
{
    public class MicrosoftTranslationService : ITranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _subscriptionKey;
        private readonly string _region;
        private const string BaseUrl = "https://api.cognitive.microsofttranslator.com/translate";
        private const int MaxRetries = 3;

        public MicrosoftTranslationService(IConfiguration configuration)
        {
            _subscriptionKey = configuration["MicrosoftTranslator:SubscriptionKey"] ?? throw new ArgumentNullException("MicrosoftTranslator:SubscriptionKey");
            _region = configuration["MicrosoftTranslator:Region"] ?? throw new ArgumentNullException("MicrosoftTranslator:Region");

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };

            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region", _region);
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        private async Task<string> TranslateWithRetryAsync(string text, string fromLanguage, string toLanguage, CancellationToken cancellationToken)
        {
            Exception? lastException = null;

            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    var route = $"?api-version=3.0&from={fromLanguage}&to={toLanguage}";
                    var body = new[] { new { Text = text } };
                    var requestBody = JsonSerializer.Serialize(body);
                    var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(BaseUrl + route, content, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var document = JsonDocument.Parse(responseBody);
                    var translation = document.RootElement[0]
                        .GetProperty("translations")[0]
                        .GetProperty("text")
                        .GetString();

                    return translation ?? string.Empty;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (i < MaxRetries - 1)
                    {
                        await Task.Delay(1000 * (i + 1), cancellationToken);
                        continue;
                    }
                }
            }

            throw lastException ?? new Exception("未知错误");
        }

        public async Task<string> TranslateAsync(string text, string fromLanguage, string toLanguage)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                    return string.Empty;

                if (fromLanguage == "auto")
                    fromLanguage = "";  // Microsoft Translator uses empty string for auto-detection

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                return await TranslateWithRetryAsync(text, fromLanguage, toLanguage, cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new Exception("Microsoft翻译服务错误: 请求超时，请稍后重试");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Microsoft翻译服务错误: 网络请求失败 - {ex.Message}");
            }
            catch (JsonException ex)
            {
                throw new Exception($"Microsoft翻译服务错误: 响应格式错误 - {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Microsoft翻译服务错误: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
} 