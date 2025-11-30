using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.ViewModels
{
    public class TrainerViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Ad gereklidir")]
        [StringLength(100, ErrorMessage = "Ad 100 karakterden uzun olamaz")]
        [Display(Name = "Ad")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyad gereklidir")]
        [StringLength(100, ErrorMessage = "Soyad 100 karakterden uzun olamaz")]
        [Display(Name = "Soyad")]
        public string LastName { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Uzmanlık alanları 500 karakterden uzun olamaz")]
        [Display(Name = "Uzmanlık Alanları")]
        public string? Specializations { get; set; }

        [Display(Name = "Çalışma Günleri")]
        public List<string> WorkDays { get; set; } = new List<string>();

        [Display(Name = "Çalışma Başlangıç Saati")]
        public TimeSpan? WorkStartTime { get; set; }

        [Display(Name = "Çalışma Bitiş Saati")]
        public TimeSpan? WorkEndTime { get; set; }

        [Required(ErrorMessage = "Spor salonu gereklidir")]
        [Display(Name = "Spor Salonu")]
        public int GymId { get; set; }

        [Display(Name = "Hizmetler")]
        public List<int> ServiceIds { get; set; } = new List<int>();
    }
}

