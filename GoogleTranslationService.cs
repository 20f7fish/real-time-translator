using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Web;

namespace RealtimeTranslator
{
    public class GoogleTranslationService : ITranslationService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://translate.googleapis.com/translate_a/single";
        private const int MaxRetries = 3;

        public GoogleTranslationService()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                UseProxy = false
            };

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9,zh-CN;q=0.8,zh;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("X-Client-Data", "CJW2yQEIpLbJAQipncoBCMKTywEIkqHLAQiFoM0BCNyxzQEIy7nNAQjLvc0BCKaEzgEI3YTOAQjyhM4BCJOIzgEIqYnOAQ==");
        }

        private async Task<string> TranslateWithRetryAsync(string url, CancellationToken cancellationToken)
        {
            Exception? lastException = null;
            int baseDelay = 1000; // 基础延迟1秒

            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url, cancellationToken);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        throw new HttpRequestException($"API请求失败: {response.StatusCode} - {errorContent}");
                    }
                    
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    
                    using (JsonDocument document = JsonDocument.Parse(responseContent))
                    {
                        var root = document.RootElement;
                        var translationBuilder = new StringBuilder();

                        var translationArray = root.EnumerateArray().First();
                        foreach (var item in translationArray.EnumerateArray())
                        {
                            if (item.GetArrayLength() > 0 && !item[0].ValueKind.Equals(JsonValueKind.Null))
                            {
                                translationBuilder.Append(item[0].GetString());
                            }
                        }

                        return translationBuilder.ToString();
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (i < MaxRetries - 1)
                    {
                        // 使用指数退避策略
                        var delay = baseDelay * Math.Pow(2, i);
                        await Task.Delay((int)delay, cancellationToken);
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

                var parameters = new Dictionary<string, string>
                {
                    { "client", "gtx" },
                    { "sl", fromLanguage == "auto" ? "auto" : fromLanguage },
                    { "tl", toLanguage },
                    { "dt", "t" },
                    { "dj", "1" },
                    { "source", "input" },
                    { "ie", "UTF-8" },
                    { "oe", "UTF-8" },
                    { "q", text }
                };

                var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}"));
                var url = $"{BaseUrl}?{queryString}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                return await TranslateWithRetryAsync(url, cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new Exception("Google翻译服务错误: 请求超时，请稍后重试");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Google翻译服务错误: 网络请求失败 - {ex.Message}");
            }
            catch (JsonException ex)
            {
                throw new Exception($"Google翻译服务错误: 响应格式错误 - {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Google翻译服务错误: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
} 