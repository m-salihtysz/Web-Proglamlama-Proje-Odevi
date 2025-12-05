using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitnessCenter.Web.Models
{
    public class Service
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        public int DurationMinutes { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public ICollection<TrainerService> TrainerServices { get; set; } = new List<TrainerService>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}

