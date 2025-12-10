using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitnessCenter.Web.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        [Required]
        public string MemberId { get; set; } = string.Empty;
        public ApplicationUser? Member { get; set; }

        [Required]
        public int GymId { get; set; }
        public Gym? Gym { get; set; }

        [Required]
        public int TrainerId { get; set; }
        public Trainer? Trainer { get; set; }

        [Required]
        public int ServiceId { get; set; }
        public Service? Service { get; set; }

        [Required]
        public DateTime AppointmentDate { get; set; }

        [Required]
        public TimeSpan AppointmentTime { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int DurationMinutes { get; set; }

        public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ApprovedAt { get; set; }
    }

    public enum AppointmentStatus
    {
        Pending,
        Approved,
        Rejected,
        Completed,
        Cancelled
    }
}
