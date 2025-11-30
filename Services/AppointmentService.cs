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

        public async Task<List<Appointment>> GetMemberAppointmentsAsync(string memberId)
        {
            // SQLite doesn't support TimeSpan in ORDER BY, so we fetch first then sort in memory
            var appointments = await _context.Appointments
                .Include(a => a.Gym)
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .Where(a => a.MemberId == memberId)
                .ToListAsync();

            return appointments
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.AppointmentTime)
                .ToList();
        }

        public async Task<List<Trainer>> GetAvailableTrainersAsync(DateTime date)
        {
            var dayOfWeek = date.DayOfWeek.ToString();
            
            return await _context.Trainers
                .Include(t => t.Gym)
                .Where(t => t.AvailableHours != null && t.AvailableHours.Contains(dayOfWeek))
                .ToListAsync();
        }
    }
}

