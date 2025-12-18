using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Web.Services;
using FitnessCenter.Web.ViewModels;

namespace FitnessCenter.Web.Controllers
{
    [Authorize]
    public class AIRecommendationController : Controller
    {
        private readonly AIService _aiService;

        public AIRecommendationController(AIService aiService)
        {
            _aiService = aiService;
        }

        public IActionResult Index()
        {
            return View(new AIRecommendationViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(AIRecommendationViewModel viewModel)
        {
            try
            {
                // Check if photo upload method is selected
                if (viewModel.RecommendationMethod == "photo" && viewModel.Photo != null && viewModel.Photo.Length > 0)
                {
                    // Use photo upload method
                    var (exerciseRecs, dietRecs) = await _aiService.GetRecommendationsFromPhotoAsync(viewModel.Photo);
                    viewModel.ExerciseRecommendations = exerciseRecs;
                    viewModel.DietSuggestions = dietRecs;
                }
                else if (viewModel.RecommendationMethod == "text")
                {
                    // Validate text input method
                    if (!viewModel.Height.HasValue || !viewModel.Weight.HasValue)
                    {
                        viewModel.ErrorMessage = "Lütfen boy ve kilo bilgilerinizi girin.";
                        return View(viewModel);
                    }

                    // Use text input method
                    var (exerciseRecs, dietRecs) = await _aiService.GetRecommendationsAsync(
                        viewModel.Height, 
                        viewModel.Weight, 
                        viewModel.BodyType, 
                        viewModel.FitnessGoals);
                    viewModel.ExerciseRecommendations = exerciseRecs;
                    viewModel.DietSuggestions = dietRecs;
                }
                else
                {
                    viewModel.ErrorMessage = "Lütfen bilgilerinizi girin veya bir fotoğraf yükleyin.";
                }
            }
            catch (Exception ex)
            {
                viewModel.ErrorMessage = $"Bir hata oluştu: {ex.Message}";
            }

            return View(viewModel);
        }
    }
}

