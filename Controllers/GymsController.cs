using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessCenter.Web.Data;
using FitnessCenter.Web.Models;
using FitnessCenter.Web.ViewModels;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class GymsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GymsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var gyms = await _context.Gyms
                .Include(g => g.Trainers)
                .ToListAsync();
            return View(gyms);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var gym = await _context.Gyms
                .Include(g => g.Trainers)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (gym == null)
            {
                return NotFound();
            }

            return View(gym);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GymViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var gym = new Gym
                {
                    Name = viewModel.Name,
                    Address = viewModel.Address,
                    WorkDays = viewModel.WorkDays != null && viewModel.WorkDays.Any() 
                        ? string.Join(",", viewModel.WorkDays) 
                        : null,
                    WorkStartTime = viewModel.WorkStartTime,
                    WorkEndTime = viewModel.WorkEndTime
                };
                _context.Add(gym);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(viewModel);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var gym = await _context.Gyms.FindAsync(id);
            if (gym == null)
            {
                return NotFound();
            }

            var viewModel = new GymViewModel
            {
                Id = gym.Id,
                Name = gym.Name,
                Address = gym.Address,
                WorkDays = !string.IsNullOrEmpty(gym.WorkDays) 
                    ? gym.WorkDays.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() 
                    : new List<string>(),
                WorkStartTime = gym.WorkStartTime,
                WorkEndTime = gym.WorkEndTime
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, GymViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var gym = await _context.Gyms.FindAsync(id);
                    if (gym == null)
                    {
                        return NotFound();
                    }

                    gym.Name = viewModel.Name;
                    gym.Address = viewModel.Address;
                    gym.WorkDays = viewModel.WorkDays != null && viewModel.WorkDays.Any() 
                        ? string.Join(",", viewModel.WorkDays) 
                        : null;
                    gym.WorkStartTime = viewModel.WorkStartTime;
                    gym.WorkEndTime = viewModel.WorkEndTime;

                    _context.Update(gym);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    var gym = await _context.Gyms.FindAsync(id);
                    if (gym == null)
                    {
                        return NotFound();
                    }
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(viewModel);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var gym = await _context.Gyms
                .FirstOrDefaultAsync(m => m.Id == id);
            if (gym == null)
            {
                return NotFound();
            }

            return View(gym);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var gym = await _context.Gyms.FindAsync(id);
            if (gym != null)
            {
                // Önce bu spor salonuna bağlı randevuları sil
                var appointments = await _context.Appointments
                    .Where(a => a.GymId == id)
                    .ToListAsync();
                if (appointments.Any())
                {
                    _context.Appointments.RemoveRange(appointments);
                }

                // Bu spor salonuna bağlı antrenörleri al
                var trainers = await _context.Trainers
                    .Where(t => t.GymId == id)
                    .ToListAsync();

                // Her antrenör için randevuları ve TrainerService ilişkilerini sil
                foreach (var trainer in trainers)
                {
                    var trainerAppointments = await _context.Appointments
                        .Where(a => a.TrainerId == trainer.Id)
                        .ToListAsync();
                    if (trainerAppointments.Any())
                    {
                        _context.Appointments.RemoveRange(trainerAppointments);
                    }

                    var trainerServices = await _context.TrainerServices
                        .Where(ts => ts.TrainerId == trainer.Id)
                        .ToListAsync();
                    if (trainerServices.Any())
                    {
                        _context.TrainerServices.RemoveRange(trainerServices);
                    }
                }

                // Antrenörleri sil
                if (trainers.Any())
                {
                    _context.Trainers.RemoveRange(trainers);
                }

                // En son spor salonunu sil
                _context.Gyms.Remove(gym);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

    }
}

