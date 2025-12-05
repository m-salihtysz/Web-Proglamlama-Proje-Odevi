using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessCenter.Web.Data;
using FitnessCenter.Web.Models;
using FitnessCenter.Web.Services;
using FitnessCenter.Web.ViewModels;

namespace FitnessCenter.Web.Controllers
{
    [Authorize]
    public class AppointmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppointmentService _appointmentService;
        private readonly ILogger<AppointmentsController> _logger;

        public AppointmentsController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager,
            AppointmentService appointmentService,
            ILogger<AppointmentsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _appointmentService = appointmentService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            IQueryable<Appointment> appointmentsQuery = _context.Appointments
                .Include(a => a.Gym)
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .Include(a => a.Member);

            if (!isAdmin)
            {
                appointmentsQuery = appointmentsQuery.Where(a => a.MemberId == user.Id);
            }

            var appointments = await appointmentsQuery.ToListAsync();
            appointments = appointments
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.AppointmentTime)
                .ToList();

            return View(appointments);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Gym)
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .Include(a => a.Member)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAdmin && appointment.MemberId != user.Id)
            {
                return Forbid();
            }

            return View(appointment);
        }

        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Create()
        {
            ViewData["GymId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.Gyms.ToListAsync(), "Id", "Name");
            ViewData["TrainerId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(new List<Trainer>(), "Id", "FullName");
            ViewData["ServiceId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(new List<Service>(), "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Create(AppointmentViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Challenge();
                }

                var service = await _context.Services.FindAsync(viewModel.ServiceId);
                
                if (service == null)
                {
                    ModelState.AddModelError("", "Hizmet bulunamadı");
                    await PopulateViewDataAsync(viewModel.GymId, viewModel.TrainerId);
                    return View(viewModel);
                }

                var isAvailable = await _appointmentService.IsSlotAvailableAsync(
                    viewModel.TrainerId, 
                    viewModel.AppointmentDate, 
                    viewModel.AppointmentTime, 
                    service.DurationMinutes);

                if (!isAvailable)
                {
                    ModelState.AddModelError("", "Bu saat dilimi zaten dolu. Lütfen başka bir saat seçin.");
                    await PopulateViewDataAsync(viewModel.GymId, viewModel.TrainerId);
                    return View(viewModel);
                }

                var appointment = new Appointment
                {
                    MemberId = user.Id,
                    GymId = viewModel.GymId,
                    TrainerId = viewModel.TrainerId,
                    ServiceId = viewModel.ServiceId,
                    AppointmentDate = viewModel.AppointmentDate,
                    AppointmentTime = viewModel.AppointmentTime,
                    Price = service.Price,
                    DurationMinutes = service.DurationMinutes,
                    Status = AppointmentStatus.Pending
                };

                _context.Add(appointment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            await PopulateViewDataAsync(viewModel.GymId, viewModel.TrainerId);
            return View(viewModel);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Gym)
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .Include(a => a.Member)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            return View(appointment);
        }

        [HttpPost, ActionName("Approve")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveConfirmed(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment != null)
            {
                appointment.Status = AppointmentStatus.Approved;
                appointment.ApprovedAt = DateTime.UtcNow;
                _context.Update(appointment);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reject(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Gym)
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .Include(a => a.Member)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            return View(appointment);
        }

        [HttpPost, ActionName("Reject")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RejectConfirmed(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment != null)
            {
                appointment.Status = AppointmentStatus.Rejected;
                _context.Update(appointment);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Gym)
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .Include(a => a.Member)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAdmin && appointment.MemberId != user.Id)
            {
                return Forbid();
            }

            return View(appointment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment != null)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Challenge();
                }

                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

                if (!isAdmin && appointment.MemberId != user.Id)
                {
                    return Forbid();
                }

                appointment.Status = AppointmentStatus.Cancelled;
                _context.Update(appointment);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetTrainers(int gymId, string? appointmentDate = null)
        {
            try
            {
                var allTrainers = await _context.Trainers
                    .Where(t => t.GymId == gymId)
                    .ToListAsync();

                if (!string.IsNullOrEmpty(appointmentDate) && 
                    (DateTime.TryParse(appointmentDate, out var date) || 
                     DateTime.TryParseExact(appointmentDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out date)))
                {
                    var dayOfWeek = date.DayOfWeek.ToString();
                    allTrainers = allTrainers
                        .Where(t => !string.IsNullOrEmpty(t.WorkDays) && 
                                   t.WorkDays.Contains(dayOfWeek, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                var trainers = allTrainers
                    .Select(t => new { 
                        id = t.Id, 
                        fullName = t.FullName,
                        workStartTime = t.WorkStartTime.HasValue ? t.WorkStartTime.Value.ToString(@"hh\:mm") : null,
                        workEndTime = t.WorkEndTime.HasValue ? t.WorkEndTime.Value.ToString(@"hh\:mm") : null
                    })
                    .ToList();
                
                return Json(trainers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTrainers hatası: GymId={GymId}, Date={Date}", gymId, appointmentDate);
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTrainerWorkHours(int trainerId)
        {
            var trainer = await _context.Trainers.FindAsync(trainerId);
            if (trainer == null)
            {
                return Json(new { error = "Antrenör bulunamadı" });
            }

            return Json(new
            {
                workDays = !string.IsNullOrEmpty(trainer.WorkDays) 
                    ? trainer.WorkDays.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() 
                    : new List<string>(),
                workStartTime = trainer.WorkStartTime.HasValue ? trainer.WorkStartTime.Value.ToString(@"hh\:mm") : null,
                workEndTime = trainer.WorkEndTime.HasValue ? trainer.WorkEndTime.Value.ToString(@"hh\:mm") : null
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetServices(int trainerId)
        {
            var trainer = await _context.Trainers
                .Include(t => t.TrainerServices)
                    .ThenInclude(ts => ts.Service)
                .FirstOrDefaultAsync(t => t.Id == trainerId);
            
            if (trainer == null)
            {
                return Json(new List<object>());
            }
            
            var services = trainer.TrainerServices
                .Where(ts => ts.Service != null)
                .Select(ts => new { 
                    id = ts.Service!.Id, 
                    name = ts.Service.Name,
                    price = ts.Service.Price,
                    durationMinutes = ts.Service.DurationMinutes
                })
                .ToList();
            
            return Json(services);
        }

        private async Task PopulateViewDataAsync(int? gymId, int? trainerId)
        {
            ViewData["GymId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.Gyms.ToListAsync(), "Id", "Name", gymId);
            
            if (trainerId.HasValue)
            {
                ViewData["TrainerId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                    await _context.Trainers.Where(t => t.GymId == gymId).ToListAsync(), 
                    "Id", "FullName", trainerId);
                
                var trainer = await _context.Trainers
                    .Include(t => t.TrainerServices)
                        .ThenInclude(ts => ts.Service)
                    .FirstOrDefaultAsync(t => t.Id == trainerId);
                
                if (trainer != null)
                {
                    ViewData["ServiceId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                        trainer.TrainerServices.Select(ts => ts.Service), "Id", "Name");
                }
            }
            else
            {
                ViewData["TrainerId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(new List<Trainer>(), "Id", "FullName");
                ViewData["ServiceId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(new List<Service>(), "Id", "Name");
            }
        }
    }
}

