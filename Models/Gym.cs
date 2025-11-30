using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class Gym
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Address { get; set; } = string.Empty;

        // Çalışma günleri (Pazartesi, Salı, Çarşamba, Perşembe, Cuma, Cumartesi, Pazar)
        [StringLength(100)]
        public string? WorkDays { get; set; } // Örn: "Monday,Wednesday,Friday" veya "Pazartesi,Çarşamba,Cuma"

        // Çalışma saatleri
        public TimeSpan? WorkStartTime { get; set; } // Örn: 06:00
        public TimeSpan? WorkEndTime { get; set; } // Örn: 22:00

        public ICollection<Service> Services { get; set; } = new List<Service>();
        public ICollection<Trainer> Trainers { get; set; } = new List<Trainer>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}

