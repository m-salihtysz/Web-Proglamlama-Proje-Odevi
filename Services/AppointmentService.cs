using FitnessCenter.Web.Data;
using FitnessCenter.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessCenter.Web.Services
{
    public class AppointmentService
    {
        private readonly ApplicationDbContext _context;

        public AppointmentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsSlotAvailableAsync(int trainerId, DateTime appointmentDate, TimeSpan appointmentTime, int durationMinutes, int? excludeAppointmentId = null)
        {
            var appointmentDateTime = appointmentDate.Date.Add(appointmentTime);
            var endTime = appointmentDateTime.AddMinutes(durationMinutes);

            // SQLite doesn't support DateTime.Add in LINQ queries, so fetch first then filter in memory
            var appointments = await _context.Appointments
                .Where(a => a.TrainerId == trainerId
                    && a.Status != AppointmentStatus.Cancelled
                    && a.Id != excludeAppointmentId)
                .ToListAsync();

            var conflictingAppointments = appointments
                .Any(a =>
                {
                    var existingStart = a.AppointmentDate.Date.Add(a.AppointmentTime);
                    var existingEnd = existingStart.AddMinutes(a.DurationMinutes);
                    return existingStart < endTime && existingEnd > appointmentDateTime;
                });

            return !conflictingAppointments;
        }
    }
}

