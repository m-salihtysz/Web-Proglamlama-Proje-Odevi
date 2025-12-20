using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

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

        private string FormatJsonToReadableText(string jsonString)
        {
            try
            {
                var doc = JsonDocument.Parse(jsonString);
                return FormatJsonElement(doc.RootElement);
            }
            catch
            {
                return jsonString;
            }
        }

        private string FormatJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var objText = new StringBuilder();
                    foreach (var prop in element.EnumerateObject())
                    {
                        objText.AppendLine($"• {prop.Name}: {FormatJsonElement(prop.Value)}");
                    }
                    return objText.ToString().TrimEnd();
                
                case JsonValueKind.Array:
                    var arrayText = new StringBuilder();
                    foreach (var item in element.EnumerateArray())
                    {
                        arrayText.AppendLine($"- {FormatJsonElement(item)}");
                    }
                    return arrayText.ToString().TrimEnd();
                
                case JsonValueKind.String:
                    return element.GetString() ?? "";
                
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                default:
                    return element.ToString();
            }
        }

        private string? EnhancePhotoForFitness(string base64Image)
        {
            try
            {
                // Base64'ten byte array'e çevir
                var imageBytes = Convert.FromBase64String(base64Image);
                
                using (var image = Image.Load(imageBytes))
                {
                    var originalWidth = image.Width;
                    var originalHeight = image.Height;
                    
                    // Genişletme faktörleri: yana %15, üste %10 daha iri
                    var newWidth = (int)(originalWidth * 1.15f);  // %15 daha geniş
                    var newHeight = (int)(originalHeight * 1.10f); // %10 daha yüksek
                    
                    // Fit görünüm için görüntü iyileştirmeleri
                    image.Mutate(x =>
                    {
                        // Önce görüntüyü genişlet (yana %15, üste %10 daha iri)
                        x.Resize(new ResizeOptions
                        {
                            Size = new Size(newWidth, newHeight),
                            Mode = ResizeMode.Stretch, // Orijinal oranı korumadan genişlet
                            Sampler = KnownResamplers.Lanczos3 // Yüksek kaliteli resize
                        });
                        
                        // Kontrast artır (daha belirgin görünüm)
                        x.Contrast(1.2f);
                        
                        // Parlaklık artır (özellikle üst kısım için)
                        x.Brightness(1.1f);
                        
                        // Doygunluk artır (daha canlı renkler)
                        x.Saturate(1.2f);
                        
                        // Keskinleştirme (daha net görünüm)
                        x.GaussianSharpen(1.0f);
                    });
                    
                    // JPEG olarak kaydet (yüksek kalite)
                    using (var ms = new MemoryStream())
                    {
                        image.Save(ms, new JpegEncoder { Quality = 95 });
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enhancing photo for fitness");
                return null;
            }
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
                prompt.AppendLine("\nÖNEMLİ: Yanıtını SADECE düz metin olarak ver. JSON formatı kullanma. İki bölüm halinde düzenle:");
                prompt.AppendLine("---EGZERSİZ ÖNERİLERİ---");
                prompt.AppendLine("[Buraya egzersiz önerilerini yaz]");
                prompt.AppendLine("---DİYET ÖNERİLERİ---");
                prompt.AppendLine("[Buraya diyet önerilerini yaz]");
                prompt.AppendLine("Başına veya sonuna JSON, kod bloğu işaretleri veya açıklama ekleme. Sadece düz metin döndür.");

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

                            // Parse response - try JSON first, then fallback to text splitting
                            string exerciseRecs;
                            string dietRecs;
                            
                            // Try JSON format first
                            try
                            {
                                var resultDoc = JsonDocument.Parse(text);
                                if (resultDoc.RootElement.TryGetProperty("exerciseRecommendations", out var ex))
                                {
                                    var exValue = ex.GetString() ?? "";
                                    // If the value is still a JSON string, try to parse it and format it
                                    if (exValue.TrimStart().StartsWith("[") || exValue.TrimStart().StartsWith("{"))
                                    {
                                        exerciseRecs = FormatJsonToReadableText(exValue);
                                    }
                                    else
                                    {
                                        exerciseRecs = exValue;
                                    }
                                }
                                else
                                {
                                    exerciseRecs = "Egzersiz önerileri mevcut değil.";
                                }
                                
                                if (resultDoc.RootElement.TryGetProperty("dietSuggestions", out var diet))
                                {
                                    var dietValue = diet.GetString() ?? "";
                                    // If the value is still a JSON string, try to parse it and format it
                                    if (dietValue.TrimStart().StartsWith("[") || dietValue.TrimStart().StartsWith("{"))
                                    {
                                        dietRecs = FormatJsonToReadableText(dietValue);
                                    }
                                    else
                                    {
                                        dietRecs = dietValue;
                                    }
                                }
                                else
                                {
                                    dietRecs = "Diyet önerileri mevcut değil.";
                                }
                                
                                return (exerciseRecs, dietRecs);
                            }
                            catch
                            {
                                // If not JSON, try to split by markers
                                var exerciseMarker = "---EGZERSİZ ÖNERİLERİ---";
                                var dietMarker = "---DİYET ÖNERİLERİ---";
                                
                                if (text.Contains(exerciseMarker) && text.Contains(dietMarker))
                                {
                                    var exerciseIndex = text.IndexOf(exerciseMarker);
                                    var dietIndex = text.IndexOf(dietMarker);
                                    
                                    exerciseRecs = text.Substring(exerciseIndex + exerciseMarker.Length, 
                                        dietIndex - exerciseIndex - exerciseMarker.Length).Trim();
                                    dietRecs = text.Substring(dietIndex + dietMarker.Length).Trim();
                                }
                                else
                                {
                                    // Fallback: split by common keywords
                                    var partsArray = text.Split(new[] { 
                                        "Diyet Önerileri", "diet", "DIET", "Beslenme", "beslenme", 
                                        "\n\nDiyet", "\n\nBeslenme", "DİYET ÖNERİLERİ", "DIET SUGGESTIONS" 
                                    }, StringSplitOptions.None);
                                    
                                    exerciseRecs = partsArray.Length > 0 ? partsArray[0].Trim() : text;
                                    dietRecs = partsArray.Length > 1 ? partsArray[1].Trim() : "Diyet önerileri mevcut değil.";
                                }
                                
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

        public async Task<(string ExerciseRecommendations, string DietSuggestions, string? FitPhotoBase64, string? OriginalPhotoBase64)> GetRecommendationsFromPhotoAsync(IFormFile photo)
        {
            try
            {
                var apiKey = _configuration["GoogleAI:ApiKey"];
                if (string.IsNullOrEmpty(apiKey) || apiKey == "your-google-ai-api-key-here" || apiKey.StartsWith("your-"))
                {
                    return ("⚠️ Google AI (Gemini) API anahtarı yapılandırılmamış. Lütfen appsettings.json dosyasına geçerli bir API anahtarı ekleyin. API anahtarı almak için: https://aistudio.google.com/app/apikey", 
                           "⚠️ Google AI (Gemini) API anahtarı yapılandırılmamış. Lütfen appsettings.json dosyasına geçerli bir API anahtarı ekleyin. API anahtarı almak için: https://aistudio.google.com/app/apikey",
                           null, null);
                }

                if (photo == null || photo.Length == 0)
                {
                    return ("❌ Fotoğraf yüklenemedi. Lütfen geçerli bir fotoğraf seçin.", 
                           "❌ Fotoğraf yüklenemedi. Lütfen geçerli bir fotoğraf seçin.",
                           null, null);
                }

                // Validate image file
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(photo.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return ("❌ Desteklenmeyen dosya formatı. Lütfen JPG, PNG, GIF veya WEBP formatında bir fotoğraf yükleyin.", 
                           "❌ Desteklenmeyen dosya formatı. Lütfen JPG, PNG, GIF veya WEBP formatında bir fotoğraf yükleyin.",
                           null, null);
                }

                // Convert image to base64
                string base64Image;
                string originalPhotoBase64;
                string? fitPhotoBase64 = null;
                using (var memoryStream = new MemoryStream())
                {
                    await photo.CopyToAsync(memoryStream);
                    var imageBytes = memoryStream.ToArray();
                    base64Image = Convert.ToBase64String(imageBytes);
                    originalPhotoBase64 = base64Image; // Orijinal fotoğrafı sakla
                    
                    // Fit fotoğraf için görüntüyü işle (keskinleştir, efekt ekle)
                    fitPhotoBase64 = EnhancePhotoForFitness(base64Image);
                }

                // Determine MIME type
                string mimeType = fileExtension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };

                var prompt = new StringBuilder();
                prompt.AppendLine("Sen deneyimli bir fitness ve beslenme uzmanısın. Yüklenen fotoğrafı analiz ederek kişiselleştirilmiş, motive edici ve anlaşılır öneriler sun. TÜM CEVAPLARINI TÜRKÇE VER.");
                prompt.AppendLine("Fotoğraftaki kişiyi asla azarlama veya yargılama. Nazikçe ve destekleyici bir dil kullan. Fotoğraftan vücut tipi, genel görünüm ve potansiyel fitness seviyesi hakkında genel gözlemler yapabilirsin.");
                prompt.AppendLine("\nLütfen şunları sağla:");
                prompt.AppendLine("1. Egzersiz önerileri: Bölgesel çalışma (ör. tam vücut, üst vücut, alt vücut), spesifik hareket isimleri, set/tekrar sayıları ve haftalık sıklık.");
                prompt.AppendLine("2. Diyet önerileri: Kahvaltı, öğle, akşam ve ara öğünler için örnek menüler; genel kalori ve makro tavsiyeleri.");
                prompt.AppendLine("3. Metin tarzı: Kısa başlıklar ve madde işaretleri kullan, okunması kolay ve motive edici olsun.");
                prompt.AppendLine("\nÖNEMLİ: Yanıtını SADECE düz metin olarak ver. JSON formatı kullanma. İki bölüm halinde düzenle:");
                prompt.AppendLine("---EGZERSİZ ÖNERİLERİ---");
                prompt.AppendLine("[Buraya egzersiz önerilerini yaz]");
                prompt.AppendLine("---DİYET ÖNERİLERİ---");
                prompt.AppendLine("[Buraya diyet önerilerini yaz]");
                prompt.AppendLine("Başına veya sonuna JSON, kod bloğu işaretleri veya açıklama ekleme. Sadece düz metin döndür.");

                // Find a vision-capable model
                string? fullModelName = null;
                string? workingVersion = null;
                
                try
                {
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
                                // Prefer vision-capable models
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
                                                    var modelShortName = modelName.Contains("/") ? modelName.Split('/').Last() : modelName;
                                                    if (!modelShortName.Contains("-exp") && !modelShortName.Contains("-preview"))
                                                    {
                                                        fullModelName = modelName;
                                                        workingVersion = version;
                                                        _logger.LogInformation("Using vision model: {Model} (version: {Version})", fullModelName, workingVersion);
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
                        ("v1beta", "gemini-1.5-pro")
                    };
                    
                    foreach (var (version, model) in commonModels)
                    {
                        fullModelName = model;
                        workingVersion = version;
                        break; // Try the first one
                    }
                }
                
                if (string.IsNullOrEmpty(fullModelName) || string.IsNullOrEmpty(workingVersion))
                {
                    return ("❌ Hiçbir çalışan Gemini modeli bulunamadı. Lütfen API anahtarınızın doğru olduğundan ve Gemini API'nin aktif olduğundan emin olun.", 
                           "❌ Hiçbir çalışan Gemini modeli bulunamadı. Lütfen API anahtarınızın doğru olduğundan ve Gemini API'nin aktif olduğundan emin olun.",
                           null, originalPhotoBase64);
                }
                
                // Build request with image
                var modelNameForUrl = fullModelName.Contains("/") ? fullModelName.Split('/').Last() : fullModelName;
                var url = $"https://generativelanguage.googleapis.com/{workingVersion}/models/{modelNameForUrl}:generateContent?key={apiKey}";
                
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = prompt.ToString() },
                                new
                                {
                                    inline_data = new
                                    {
                                        mime_type = mimeType,
                                        data = base64Image
                                    }
                                }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(responseContent))
                {
                    _logger.LogInformation("Gemini API vision response received successfully");
                    var jsonDoc = JsonDocument.Parse(responseContent);
                    
                    if (jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var candidate = candidates[0];
                        if (candidate.TryGetProperty("content", out var contentObj) && 
                            contentObj.TryGetProperty("parts", out var parts) && 
                            parts.GetArrayLength() > 0)
                        {
                            var text = parts[0].GetProperty("text").GetString() ?? "";

                            // Clean JSON response
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

                            // Parse response - try JSON first, then fallback to text splitting
                            string exerciseRecs;
                            string dietRecs;
                            
                            // Try JSON format first
                            try
                            {
                                var resultDoc = JsonDocument.Parse(text);
                                if (resultDoc.RootElement.TryGetProperty("exerciseRecommendations", out var ex))
                                {
                                    var exValue = ex.GetString() ?? "";
                                    // If the value is still a JSON string, try to parse it and format it
                                    if (exValue.TrimStart().StartsWith("[") || exValue.TrimStart().StartsWith("{"))
                                    {
                                        exerciseRecs = FormatJsonToReadableText(exValue);
                                    }
                                    else
                                    {
                                        exerciseRecs = exValue;
                                    }
                                }
                                else
                                {
                                    exerciseRecs = "Egzersiz önerileri mevcut değil.";
                                }
                                
                                if (resultDoc.RootElement.TryGetProperty("dietSuggestions", out var diet))
                                {
                                    var dietValue = diet.GetString() ?? "";
                                    // If the value is still a JSON string, try to parse it and format it
                                    if (dietValue.TrimStart().StartsWith("[") || dietValue.TrimStart().StartsWith("{"))
                                    {
                                        dietRecs = FormatJsonToReadableText(dietValue);
                                    }
                                    else
                                    {
                                        dietRecs = dietValue;
                                    }
                                }
                                else
                                {
                                    dietRecs = "Diyet önerileri mevcut değil.";
                                }
                                
                                // Fit fotoğrafı zaten görüntü işleme ile oluşturuldu (keskinleştirme, efektler)
                                // Eğer görüntü işleme başarısız olduysa null olacak
                                
                                return (exerciseRecs, dietRecs, fitPhotoBase64, originalPhotoBase64);
                            }
                            catch
                            {
                                // If not JSON, try to split by markers
                                var exerciseMarker = "---EGZERSİZ ÖNERİLERİ---";
                                var dietMarker = "---DİYET ÖNERİLERİ---";
                                
                                if (text.Contains(exerciseMarker) && text.Contains(dietMarker))
                                {
                                    var exerciseIndex = text.IndexOf(exerciseMarker);
                                    var dietIndex = text.IndexOf(dietMarker);
                                    
                                    exerciseRecs = text.Substring(exerciseIndex + exerciseMarker.Length, 
                                        dietIndex - exerciseIndex - exerciseMarker.Length).Trim();
                                    dietRecs = text.Substring(dietIndex + dietMarker.Length).Trim();
                                }
                                else
                                {
                                    // Fallback: split by common keywords
                                    var partsArray = text.Split(new[] { 
                                        "Diyet Önerileri", "diet", "DIET", "Beslenme", "beslenme", 
                                        "\n\nDiyet", "\n\nBeslenme", "DİYET ÖNERİLERİ", "DIET SUGGESTIONS" 
                                    }, StringSplitOptions.None);
                                    
                                    exerciseRecs = partsArray.Length > 0 ? partsArray[0].Trim() : text;
                                    dietRecs = partsArray.Length > 1 ? partsArray[1].Trim() : "Diyet önerileri mevcut değil.";
                                }
                                
                                // Fit fotoğrafı zaten görüntü işleme ile oluşturuldu (keskinleştirme, efektler)
                                // Eğer görüntü işleme başarısız olduysa null olacak
                                
                                return (exerciseRecs, dietRecs, fitPhotoBase64, originalPhotoBase64);
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogError("Gemini API vision error: {StatusCode} - {Content}", response.StatusCode, responseContent ?? "No content");
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        return ("❌ Geçersiz API anahtarı. Lütfen appsettings.json dosyasındaki GoogleAI:ApiKey değerini kontrol edin. API anahtarı almak için: https://aistudio.google.com/app/apikey", 
                               "❌ Geçersiz API anahtarı. Lütfen appsettings.json dosyasındaki GoogleAI:ApiKey değerini kontrol edin. API anahtarı almak için: https://aistudio.google.com/app/apikey",
                               null, originalPhotoBase64);
                    }
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || 
                        (responseContent?.Contains("429") == true) || 
                        (responseContent?.Contains("RESOURCE_EXHAUSTED") == true) ||
                        (responseContent?.Contains("quota") == true))
                    {
                        return ("⚠️ API kotası dolmuş veya ücretsiz tier limitine ulaşıldı. Lütfen birkaç dakika bekleyip tekrar deneyin. Kullanım limitlerinizi kontrol etmek için: https://ai.dev/usage?tab=rate-limit", 
                               "⚠️ API kotası dolmuş veya ücretsiz tier limitine ulaşıldı. Lütfen birkaç dakika bekleyip tekrar deneyin. Kullanım limitlerinizi kontrol etmek için: https://ai.dev/usage?tab=rate-limit",
                               null, originalPhotoBase64);
                    }
                    
                    var errorMsg = !string.IsNullOrEmpty(responseContent) ? responseContent : "Bilinmeyen hata";
                    return ($"❌ API Hatası: {response.StatusCode} - {errorMsg}", $"❌ API Hatası: {response.StatusCode} - {errorMsg}",
                           null, originalPhotoBase64);
                }

                return ("Öneriler oluşturulamadı. Lütfen tekrar deneyin.", 
                       "Öneriler oluşturulamadı. Lütfen tekrar deneyin.",
                       null, originalPhotoBase64);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI recommendations from photo");
                
                if (ex.Message.Contains("401") || ex.Message.Contains("403"))
                {
                    return ("❌ Geçersiz API anahtarı. Lütfen appsettings.json dosyasındaki GoogleAI:ApiKey değerini kontrol edin. API anahtarı almak için: https://aistudio.google.com/app/apikey", 
                           "❌ Geçersiz API anahtarı. Lütfen appsettings.json dosyasındaki GoogleAI:ApiKey değerini kontrol edin. API anahtarı almak için: https://aistudio.google.com/app/apikey",
                           null, null);
                }
                
                if (ex.Message.Contains("quota") || ex.Message.Contains("429") || ex.Message.Contains("RESOURCE_EXHAUSTED") || ex.Message.Contains("TooManyRequests"))
                {
                    return ("⚠️ API kotası dolmuş veya ücretsiz tier limitine ulaşıldı. Lütfen birkaç dakika bekleyip tekrar deneyin. Kullanım limitlerinizi kontrol etmek için: https://ai.dev/usage?tab=rate-limit", 
                           "⚠️ API kotası dolmuş veya ücretsiz tier limitine ulaşıldı. Lütfen birkaç dakika bekleyip tekrar deneyin. Kullanım limitlerinizi kontrol etmek için: https://ai.dev/usage?tab=rate-limit",
                           null, null);
                }
                
                return ($"❌ Hata: {ex.Message}", $"❌ Hata: {ex.Message}", null, null);
            }
        }

    }
}

