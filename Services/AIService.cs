using System.Text;
using System.Text.Json;

namespace FitnessCenter.Web.Services
{
    public class AIService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AIService> _logger;
        private readonly HttpClient _httpClient;

        public AIService(IConfiguration configuration, ILogger<AIService> logger, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<(string ExerciseRecommendations, string DietSuggestions)> GetRecommendationsAsync(
            decimal? height, 
            decimal? weight, 
            string? bodyType, 
            string? fitnessGoals)
        {
            try
            {
                var apiKey = _configuration["GoogleAI:ApiKey"];
                if (string.IsNullOrEmpty(apiKey) || apiKey == "your-google-ai-api-key-here" || apiKey.StartsWith("your-"))
                {
                    return ("⚠️ Google AI (Gemini) API anahtarı yapılandırılmamış. Lütfen appsettings.json dosyasına geçerli bir API anahtarı ekleyin. API anahtarı almak için: https://aistudio.google.com/app/apikey", 
                           "⚠️ Google AI (Gemini) API anahtarı yapılandırılmamış. Lütfen appsettings.json dosyasına geçerli bir API anahtarı ekleyin. API anahtarı almak için: https://aistudio.google.com/app/apikey");
                }

                var prompt = new StringBuilder();
                prompt.AppendLine("Sen deneyimli bir fitness ve beslenme uzmanısın. Aşağıdaki bilgilere göre kişiselleştirilmiş, motive edici ve anlaşılır öneriler sun. TÜM CEVAPLARINI TÜRKÇE VER.");
                prompt.AppendLine("Kullanıcıyı asla azarlama veya yargılama. Ölçüler gerçekçi olmasa bile nazikçe kısa bir uyarı yapabilirsin ama mutlaka örnek, mantıklı bir yetişkin programı yaz.");
                
                if (height.HasValue)
                    prompt.AppendLine($"Boy: {height} cm");
                if (weight.HasValue)
                    prompt.AppendLine($"Kilo: {weight} kg");
                if (!string.IsNullOrEmpty(bodyType))
                    prompt.AppendLine($"Vücut Tipi: {bodyType}");
                if (!string.IsNullOrEmpty(fitnessGoals))
                    prompt.AppendLine($"Fitness Hedefleri: {fitnessGoals}");

                prompt.AppendLine("\nLütfen şunları sağla:");
                prompt.AppendLine("1. Egzersiz önerileri: Bölgesel çalışma (ör. tam vücut, üst vücut, alt vücut), spesifik hareket isimleri, set/tekrar sayıları ve haftalık sıklık.");
                prompt.AppendLine("2. Diyet önerileri: Kahvaltı, öğle, akşam ve ara öğünler için örnek menüler; genel kalori ve makro tavsiyeleri.");
                prompt.AppendLine("3. Metin tarzı: Kısa başlıklar ve madde işaretleri kullan, okunması kolay ve motive edici olsun.");
                prompt.AppendLine("\nSADECE TEK BİR JSON NESNESİ DÖN:");
                prompt.AppendLine("{ \"exerciseRecommendations\": \"...\", \"dietSuggestions\": \"...\" }");
                prompt.AppendLine("Başına veya sonuna açıklama, açıklama cümlesi, ```json gibi kod bloğu işaretleri ekleme.");

                // Gemini API request
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt.ToString() }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Get available models from API
                string? fullModelName = null;
                string? workingVersion = null;
                
                try
                {
                    // Try both v1beta and v1 to find available models
                    var versions = new[] { "v1beta", "v1" };
                    
                    foreach (var version in versions)
                    {
                        var listUrl = $"https://generativelanguage.googleapis.com/{version}/models?key={apiKey}";
                        var listResponse = await _httpClient.GetAsync(listUrl);
                        
                        if (listResponse.IsSuccessStatusCode)
                        {
                            var listContent = await listResponse.Content.ReadAsStringAsync();
                            var listDoc = JsonDocument.Parse(listContent);
                            
                            if (listDoc.RootElement.TryGetProperty("models", out var modelsArray))
                            {
                                // Prefer highest quota models first (flash-live > flash-lite > flash > pro)
                                var preferredModels = new[]
                                {
                                    "gemini-2.5-flash-live",
                                    "gemini-2.0-flash-live",
                                    "gemini-2.5-flash-lite",
                                    "gemini-1.5-flash",
                                    "gemini-1.5-pro",
                                    "gemini-pro"
                                };
                                
                                foreach (var preferredModel in preferredModels)
                                {
                                    foreach (var model in modelsArray.EnumerateArray())
                                    {
                                        if (model.TryGetProperty("name", out var name))
                                        {
                                            var modelName = name.GetString() ?? "";
                                            
                                            if (modelName.Contains(preferredModel))
                                            {
                                                // Check if it supports generateContent
                                                bool supportsGenerateContent = false;
                                                if (model.TryGetProperty("supportedGenerationMethods", out var methods))
                                                {
                                                    foreach (var method in methods.EnumerateArray())
                                                    {
                                                        if (method.GetString() == "generateContent")
                                                        {
                                                            supportsGenerateContent = true;
                                                            break;
                                                        }
                                                    }
                                                }
                                                
                                                if (supportsGenerateContent)
                                                {
                                                    // Skip experimental models that might not work with free tier
                                                    var modelShortName = modelName.Contains("/") ? modelName.Split('/').Last() : modelName;
                                                    if (!modelShortName.Contains("-exp") && !modelShortName.Contains("-preview"))
                                                    {
                                                        fullModelName = modelName; // Use full model name from API
                                                        workingVersion = version;
                                                        _logger.LogInformation("Using model: {Model} (version: {Version})", fullModelName, workingVersion);
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (!string.IsNullOrEmpty(fullModelName)) break;
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(fullModelName)) break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not list models: {Error}", ex.Message);
                }
                
                // If no model found from list, try direct calls with common models
                if (string.IsNullOrEmpty(fullModelName))
                {
                    var commonModels = new[]
                    {
                        ("v1", "gemini-2.5-flash-live"),
                        ("v1beta", "gemini-2.5-flash-live"),
                        ("v1", "gemini-2.0-flash-live"),
                        ("v1beta", "gemini-2.0-flash-live"),
                        ("v1", "gemini-2.5-flash-lite"),
                        ("v1beta", "gemini-2.5-flash-lite"),
                        ("v1", "gemini-1.5-flash"),
                        ("v1beta", "gemini-1.5-flash"),
                        ("v1", "gemini-1.5-pro"),
                        ("v1beta", "gemini-1.5-pro"),
                        ("v1", "gemini-pro"),
                        ("v1beta", "gemini-pro")
                    };
                    
                    foreach (var (version, model) in commonModels)
                    {
                        var testUrl = $"https://generativelanguage.googleapis.com/{version}/models/{model}:generateContent?key={apiKey}";
                        var testRequest = new StringContent("{\"contents\":[{\"parts\":[{\"text\":\"test\"}]}]}", Encoding.UTF8, "application/json");
                        var testResponse = await _httpClient.PostAsync(testUrl, testRequest);
                        
                        if (testResponse.IsSuccessStatusCode || testResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
                        {
                            fullModelName = model;
                            workingVersion = version;
                            _logger.LogInformation("Found working model: {Model} (version: {Version})", model, version);
                            break;
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(fullModelName) || string.IsNullOrEmpty(workingVersion))
                {
                    return ("❌ Hiçbir çalışan Gemini modeli bulunamadı. Lütfen API anahtarınızın doğru olduğundan ve Gemini API'nin aktif olduğundan emin olun.", 
                           "❌ Hiçbir çalışan Gemini modeli bulunamadı. Lütfen API anahtarınızın doğru olduğundan ve Gemini API'nin aktif olduğundan emin olun.");
                }
                
                // Use the model name directly (it might be "models/gemini-1.5-flash" or just "gemini-1.5-flash")
                var modelNameForUrl = fullModelName.Contains("/") ? fullModelName.Split('/').Last() : fullModelName;
                var url = $"https://generativelanguage.googleapis.com/{workingVersion}/models/{modelNameForUrl}:generateContent?key={apiKey}";
                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(responseContent))
                {
                    _logger.LogInformation("Gemini API response received successfully");
                    var jsonDoc = JsonDocument.Parse(responseContent);
                    
                    if (jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var candidate = candidates[0];
                        if (candidate.TryGetProperty("content", out var contentObj) && 
                            contentObj.TryGetProperty("parts", out var parts) && 
                            parts.GetArrayLength() > 0)
                        {
                            var text = parts[0].GetProperty("text").GetString() ?? "";

                            // Çıktı bazen ```json kod bloğu içinde gelebilir, o yüzden temizle
                            text = text.Trim();
                            if (text.StartsWith("```"))
                            {
                                var firstFence = text.IndexOf("```", StringComparison.Ordinal);
                                var secondFence = text.IndexOf("```", firstFence + 3, StringComparison.Ordinal);
                                if (secondFence > firstFence)
                                {
                                    text = text.Substring(firstFence + 3, secondFence - firstFence - 3);
                                }
                                text = text.Trim();
                                if (text.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                                {
                                    text = text.Substring(4).TrimStart();
                                }
                            }

                            // Try to parse JSON response
                            try
                            {
                                var resultDoc = JsonDocument.Parse(text);
                                var exerciseRecs = resultDoc.RootElement.TryGetProperty("exerciseRecommendations", out var ex) 
                                    ? ex.GetString() ?? "Egzersiz önerileri mevcut değil."
                                    : "Egzersiz önerileri mevcut değil.";
                                var dietRecs = resultDoc.RootElement.TryGetProperty("dietSuggestions", out var diet) 
                                    ? diet.GetString() ?? "Diyet önerileri mevcut değil."
                                    : "Diyet önerileri mevcut değil.";
                        
                        return (exerciseRecs, dietRecs);
                    }
                    catch
                    {
                        // If not JSON, split the response
                                var partsArray = text.Split(new[] { "Diyet", "diet", "DIET", "Beslenme", "beslenme", "\n\nDiyet", "\n\nBeslenme" }, StringSplitOptions.None);
                                var exerciseRecs = partsArray.Length > 0 ? partsArray[0].Trim() : text;
                                var dietRecs = partsArray.Length > 1 ? partsArray[1].Trim() : "Diyet önerileri mevcut değil.";
                        
                        return (exerciseRecs, dietRecs);
                    }
                }
                    }
                }
                else
                {
                    _logger.LogError("Gemini API error: {StatusCode} - {Content}", response.StatusCode, responseContent ?? "No content");
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        return ("❌ Geçersiz API anahtarı. Lütfen appsettings.json dosyasındaki GoogleAI:ApiKey değerini kontrol edin. API anahtarı almak için: https://aistudio.google.com/app/apikey", 
                               "❌ Geçersiz API anahtarı. Lütfen appsettings.json dosyasındaki GoogleAI:ApiKey değerini kontrol edin. API anahtarı almak için: https://aistudio.google.com/app/apikey");
                    }
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || 
                        (responseContent?.Contains("429") == true) || 
                        (responseContent?.Contains("RESOURCE_EXHAUSTED") == true) ||
                        (responseContent?.Contains("quota") == true))
                    {
                        return ("⚠️ API kotası dolmuş veya ücretsiz tier limitine ulaşıldı. Lütfen birkaç dakika bekleyip tekrar deneyin. Kullanım limitlerinizi kontrol etmek için: https://ai.dev/usage?tab=rate-limit", 
                               "⚠️ API kotası dolmuş veya ücretsiz tier limitine ulaşıldı. Lütfen birkaç dakika bekleyip tekrar deneyin. Kullanım limitlerinizi kontrol etmek için: https://ai.dev/usage?tab=rate-limit");
                    }
                    
                    var errorMsg = !string.IsNullOrEmpty(responseContent) ? responseContent : "Bilinmeyen hata";
                    return ($"❌ API Hatası: {response.StatusCode} - {errorMsg}", $"❌ API Hatası: {response.StatusCode} - {errorMsg}");
                }

                return ("Öneriler oluşturulamadı. Lütfen tekrar deneyin.", 
                       "Öneriler oluşturulamadı. Lütfen tekrar deneyin.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI recommendations");
                
                if (ex.Message.Contains("401") || ex.Message.Contains("403"))
                {
                    return ("❌ Geçersiz API anahtarı. Lütfen appsettings.json dosyasındaki GoogleAI:ApiKey değerini kontrol edin. API anahtarı almak için: https://aistudio.google.com/app/apikey", 
                           "❌ Geçersiz API anahtarı. Lütfen appsettings.json dosyasındaki GoogleAI:ApiKey değerini kontrol edin. API anahtarı almak için: https://aistudio.google.com/app/apikey");
                }
                
                if (ex.Message.Contains("quota") || ex.Message.Contains("429") || ex.Message.Contains("RESOURCE_EXHAUSTED") || ex.Message.Contains("TooManyRequests"))
                {
                    return ("⚠️ API kotası dolmuş veya ücretsiz tier limitine ulaşıldı. Lütfen birkaç dakika bekleyip tekrar deneyin. Kullanım limitlerinizi kontrol etmek için: https://ai.dev/usage?tab=rate-limit", 
                           "⚠️ API kotası dolmuş veya ücretsiz tier limitine ulaşıldı. Lütfen birkaç dakika bekleyip tekrar deneyin. Kullanım limitlerinizi kontrol etmek için: https://ai.dev/usage?tab=rate-limit");
                }
                
                return ($"❌ Hata: {ex.Message}", $"❌ Hata: {ex.Message}");
            }
        }

    }
}

