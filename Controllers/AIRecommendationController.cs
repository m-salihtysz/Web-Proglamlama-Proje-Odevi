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
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                if (!viewModel.Height.HasValue || !viewModel.Weight.HasValue)
                {
                    viewModel.ErrorMessage = "Lütfen boy ve kilo bilgilerinizi girin.";
                    return View(viewModel);
                }

                var (exerciseRecs, dietRecs) = await _aiService.GetRecommendationsAsync(
                        viewModel.Height, 
                        viewModel.Weight, 
                        viewModel.BodyType, 
                        viewModel.FitnessGoals);
                    viewModel.ExerciseRecommendations = exerciseRecs;
                    viewModel.DietSuggestions = dietRecs;
            }
            catch (Exception ex)
            {
                viewModel.ErrorMessage = $"Bir hata oluştu: {ex.Message}";
            }

            return View(viewModel);
        }
    }
}

