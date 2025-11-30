using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.ViewModels
{
    public class AIRecommendationViewModel
    {
        [Required(ErrorMessage = "Boy bilgisi zorunludur")]
        [Range(50, 250, ErrorMessage = "Boy 50 ile 250 cm arasında olmalıdır")]
        [Display(Name = "Boy (cm)")]
        public decimal? Height { get; set; }

        [Required(ErrorMessage = "Kilo bilgisi zorunludur")]
        [Range(20, 300, ErrorMessage = "Kilo 20 ile 300 kg arasında olmalıdır")]
        [Display(Name = "Kilo (kg)")]
        public decimal? Weight { get; set; }

        [StringLength(100)]
        [Display(Name = "Vücut Tipi")]
        public string? BodyType { get; set; }

        [StringLength(500)]
        [Display(Name = "Fitness Hedefleri")]
        public string? FitnessGoals { get; set; }

        public string? ExerciseRecommendations { get; set; }
        public string? DietSuggestions { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

