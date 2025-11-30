using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FitnessCenter.Web.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Gym> Gyms { get; set; }
        public DbSet<Trainer> Trainers { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<TrainerService> TrainerServices { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure many-to-many relationship between Trainer and Service
            builder.Entity<TrainerService>()
                .HasKey(ts => new { ts.TrainerId, ts.ServiceId });

            builder.Entity<TrainerService>()
                .HasOne(ts => ts.Trainer)
                .WithMany(t => t.TrainerServices)
                .HasForeignKey(ts => ts.TrainerId);

            builder.Entity<TrainerService>()
                .HasOne(ts => ts.Service)
                .WithMany(s => s.TrainerServices)
                .HasForeignKey(ts => ts.ServiceId);

            // Configure relationships
            builder.Entity<Appointment>()
                .HasOne(a => a.Member)
                .WithMany(u => u.Appointments)
                .HasForeignKey(a => a.MemberId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Appointment>()
                .HasOne(a => a.Gym)
                .WithMany(g => g.Appointments)
                .HasForeignKey(a => a.GymId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Appointment>()
                .HasOne(a => a.Trainer)
                .WithMany(t => t.Appointments)
                .HasForeignKey(a => a.TrainerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Appointment>()
                .HasOne(a => a.Service)
                .WithMany(s => s.Appointments)
                .HasForeignKey(a => a.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure TimeSpan columns for SQLite
            builder.Entity<Trainer>()
                .Property(t => t.WorkStartTime)
                .HasConversion(
                    v => v.HasValue ? v.Value.ToString(@"hh\:mm\:ss") : null,
                    v => !string.IsNullOrEmpty(v) ? TimeSpan.Parse(v) : null);

            builder.Entity<Trainer>()
                .Property(t => t.WorkEndTime)
                .HasConversion(
                    v => v.HasValue ? v.Value.ToString(@"hh\:mm\:ss") : null,
                    v => !string.IsNullOrEmpty(v) ? TimeSpan.Parse(v) : null);

            // Configure TimeSpan columns for Gym
            builder.Entity<Gym>()
                .Property(g => g.WorkStartTime)
                .HasConversion(
                    v => v.HasValue ? v.Value.ToString(@"hh\:mm\:ss") : null,
                    v => !string.IsNullOrEmpty(v) ? TimeSpan.Parse(v) : null);

            builder.Entity<Gym>()
                .Property(g => g.WorkEndTime)
                .HasConversion(
                    v => v.HasValue ? v.Value.ToString(@"hh\:mm\:ss") : null,
                    v => !string.IsNullOrEmpty(v) ? TimeSpan.Parse(v) : null);
        }
    }
}

