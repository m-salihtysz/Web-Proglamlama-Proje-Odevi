using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.ViewModels
{
    public class ServiceViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Hizmet adı gereklidir")]
        [StringLength(200, ErrorMessage = "Ad 200 karakterden uzun olamaz")]
        [Display(Name = "Hizmet Adı")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Açıklama 1000 karakterden uzun olamaz")]
        [Display(Name = "Açıklama")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Süre gereklidir")]
        [Range(1, 600, ErrorMessage = "Süre 1 ile 600 dakika arasında olmalıdır")]
        [Display(Name = "Süre (dakika)")]
        public int DurationMinutes { get; set; }

        [Required(ErrorMessage = "Fiyat gereklidir")]
        [Range(0.01, 10000, ErrorMessage = "Fiyat 0.01 ile 10000 arasında olmalıdır")]
        [Display(Name = "Fiyat (TL)")]
        public decimal Price { get; set; }
    }
}

