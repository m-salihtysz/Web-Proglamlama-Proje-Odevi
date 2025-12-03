using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.ViewModels
{
    public class AppointmentViewModel
    {
        [Required(ErrorMessage = "Spor salonu gereklidir")]
        [Display(Name = "Spor Salonu")]
        public int GymId { get; set; }

        [Required(ErrorMessage = "Antrenör gereklidir")]
        [Display(Name = "Antrenör")]
        public int TrainerId { get; set; }

        [Required(ErrorMessage = "Hizmet gereklidir")]
        [Display(Name = "Hizmet")]
        public int ServiceId { get; set; }

        [Required(ErrorMessage = "Tarih gereklidir")]
        [DataType(DataType.Date)]
        [Display(Name = "Randevu Tarihi")]
        public DateTime AppointmentDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Saat gereklidir")]
        [DataType(DataType.Time)]
        [Display(Name = "Randevu Saati")]
        public TimeSpan AppointmentTime { get; set; }
    }
}

