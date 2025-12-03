using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessCenter.Web.Data;
using FitnessCenter.Web.Models;
using FitnessCenter.Web.Services;

namespace FitnessCenter.Web.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AppointmentsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AppointmentService _appointmentService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AppointmentsApiController(
            ApplicationDbContext context, 
            AppointmentService appointmentService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _appointmentService = appointmentService;
            _userManager = userManager;
        }

        [HttpGet("member/{memberId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetMemberAppointments(string memberId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
            if (!isAdmin && currentUser.Id != memberId)
            {
                return Forbid("You can only access your own appointments.");
            }

            var appointments = await _context.Appointments
                .Include(a => a.Gym)
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .Include(a => a.Member)
                .Where(a => a.MemberId == memberId)
                .ToListAsync();

            var result = appointments
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.AppointmentTime)
                .Select(a => new
                {
                    a.Id,
                    a.AppointmentDate,
                    a.AppointmentTime,
                    a.Price,
                    a.DurationMinutes,
                    a.Status,
                    a.CreatedAt,
                    a.ApprovedAt,
                    GymName = a.Gym != null ? a.Gym.Name : null,
                    TrainerName = a.Trainer != null ? a.Trainer.FullName : null,
                    ServiceName = a.Service != null ? a.Service.Name : null,
                    MemberName = a.Member != null ? $"{a.Member.FirstName} {a.Member.LastName}" : null
                })
                .ToList();

            return Ok(result);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAppointments()
        {
            // Get current user
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
            
            IQueryable<Appointment> appointmentsQuery = _context.Appointments
                .Include(a => a.Gym)
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .Include(a => a.Member);

            if (!isAdmin)
            {
                appointmentsQuery = appointmentsQuery.Where(a => a.MemberId == currentUser.Id);
            }

            // SQLite doesn't support TimeSpan in ORDER BY, so we fetch first then sort in memory
            var appointments = await appointmentsQuery.ToListAsync();

            var result = appointments
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.AppointmentTime)
                .Select(a => new
                {
                    a.Id,
                    a.AppointmentDate,
                    a.AppointmentTime,
                    a.Price,
                    a.DurationMinutes,
                    a.Status,
                    a.CreatedAt,
                    a.ApprovedAt,
                    GymName = a.Gym != null ? a.Gym.Name : null,
                    TrainerName = a.Trainer != null ? a.Trainer.FullName : null,
                    ServiceName = a.Service != null ? a.Service.Name : null,
                    MemberName = a.Member != null ? $"{a.Member.FirstName} {a.Member.LastName}" : null
                })
                .ToList();

            return Ok(result);
        }
    }
}

