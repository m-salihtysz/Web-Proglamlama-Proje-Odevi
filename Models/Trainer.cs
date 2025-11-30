using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitnessCenter.Web.Models
{
    public class Trainer
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Specializations { get; set; }

        [StringLength(200)]
        public string? AvailableHours { get; set; }

        // Çalışma günleri (Pazartesi, Salı, Çarşamba, Perşembe, Cuma, Cumartesi, Pazar)
        [StringLength(100)]
        public string? WorkDays { get; set; } // Örn: "Monday,Wednesday,Friday" veya "Pazartesi,Çarşamba,Cuma"

        // Çalışma saatleri
        public TimeSpan? WorkStartTime { get; set; } // Örn: 09:00
        public TimeSpan? WorkEndTime { get; set; } // Örn: 18:00

        [Required]
        public int GymId { get; set; }
        public Gym? Gym { get; set; }

        public ICollection<TrainerService> TrainerServices { get; set; } = new List<TrainerService>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";
    }
}

