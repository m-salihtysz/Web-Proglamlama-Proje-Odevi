using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessCenter.Web.Data;
using FitnessCenter.Web.Models;
using FitnessCenter.Web.ViewModels;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class TrainersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TrainersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var trainers = await _context.Trainers
                .Include(t => t.Gym)
                .Include(t => t.TrainerServices)
                    .ThenInclude(ts => ts.Service)
                .ToListAsync();
            return View(trainers);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trainer = await _context.Trainers
                .Include(t => t.Gym)
                .Include(t => t.TrainerServices)
                    .ThenInclude(ts => ts.Service)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (trainer == null)
            {
                return NotFound();
            }

            return View(trainer);
        }

        public async Task<IActionResult> Create()
        {
            ViewData["GymId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.Gyms.ToListAsync(), "Id", "Name");
            ViewData["Services"] = await _context.Services.ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TrainerViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var trainer = new Trainer
                {
                    FirstName = viewModel.FirstName,
                    LastName = viewModel.LastName,
                    Specializations = viewModel.Specializations,
                    WorkDays = viewModel.WorkDays != null && viewModel.WorkDays.Any() 
                        ? string.Join(",", viewModel.WorkDays) 
                        : null,
                    WorkStartTime = viewModel.WorkStartTime,
                    WorkEndTime = viewModel.WorkEndTime,
                    GymId = viewModel.GymId
                };

                _context.Add(trainer);
                await _context.SaveChangesAsync();

                if (viewModel.ServiceIds != null && viewModel.ServiceIds.Any())
                {
                    foreach (var serviceId in viewModel.ServiceIds)
                    {
                        var service = await _context.Services.FindAsync(serviceId);
                        if (service != null)
                        {
                            _context.TrainerServices.Add(new TrainerService
                            {
                                TrainerId = trainer.Id,
                                ServiceId = serviceId
                            });
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }
            ViewData["GymId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.Gyms.ToListAsync(), "Id", "Name", viewModel.GymId);
            ViewData["Services"] = await _context.Services.ToListAsync();
            return View(viewModel);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trainer = await _context.Trainers
                .Include(t => t.TrainerServices)
                    .ThenInclude(ts => ts.Service)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (trainer == null)
            {
                return NotFound();
            }

            var viewModel = new TrainerViewModel
            {
                Id = trainer.Id,
                FirstName = trainer.FirstName,
                LastName = trainer.LastName,
                Specializations = trainer.Specializations,
                WorkDays = !string.IsNullOrEmpty(trainer.WorkDays) 
                    ? trainer.WorkDays.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() 
                    : new List<string>(),
                WorkStartTime = trainer.WorkStartTime,
                WorkEndTime = trainer.WorkEndTime,
                GymId = trainer.GymId,
                ServiceIds = trainer.TrainerServices.Select(ts => ts.ServiceId).ToList()
            };

            ViewData["GymId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.Gyms.ToListAsync(), "Id", "Name", trainer.GymId);
            ViewData["Services"] = await _context.Services.ToListAsync();
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TrainerViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var trainer = await _context.Trainers
                        .Include(t => t.TrainerServices)
                        .FirstOrDefaultAsync(t => t.Id == id);
                    if (trainer == null)
                    {
                        return NotFound();
                    }

                    trainer.FirstName = viewModel.FirstName;
                    trainer.LastName = viewModel.LastName;
                    trainer.Specializations = viewModel.Specializations;
                    trainer.WorkDays = viewModel.WorkDays != null && viewModel.WorkDays.Any() 
                        ? string.Join(",", viewModel.WorkDays) 
                        : null;
                    trainer.WorkStartTime = viewModel.WorkStartTime;
                    trainer.WorkEndTime = viewModel.WorkEndTime;
                    trainer.GymId = viewModel.GymId;

                    var existingServices = trainer.TrainerServices.Select(ts => ts.ServiceId).ToList();
                    var servicesToRemove = existingServices.Except(viewModel.ServiceIds ?? new List<int>()).ToList();
                    var servicesToAdd = (viewModel.ServiceIds ?? new List<int>()).Except(existingServices).ToList();

                    foreach (var serviceId in servicesToRemove)
                    {
                        var trainerService = await _context.TrainerServices
                            .FirstOrDefaultAsync(ts => ts.TrainerId == trainer.Id && ts.ServiceId == serviceId);
                        if (trainerService != null)
                        {
                            _context.TrainerServices.Remove(trainerService);
                        }
                    }

                    foreach (var serviceId in servicesToAdd)
                    {
                        _context.TrainerServices.Add(new TrainerService
                        {
                            TrainerId = trainer.Id,
                            ServiceId = serviceId
                        });
                    }

                    _context.Update(trainer);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    var trainer = await _context.Trainers.FindAsync(id);
                    if (trainer == null)
                    {
                        return NotFound();
                    }
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["GymId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.Gyms.ToListAsync(), "Id", "Name", viewModel.GymId);
            ViewData["Services"] = await _context.Services.ToListAsync();
            return View(viewModel);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trainer = await _context.Trainers
                .Include(t => t.Gym)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (trainer == null)
            {
                return NotFound();
            }

            return View(trainer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // İlgili randevuları sil (randevular fiilen iptal edilmiş olur)
            var relatedAppointments = await _context.Appointments
                .Where(a => a.TrainerId == id)
                .ToListAsync();
            if (relatedAppointments.Any())
            {
                _context.Appointments.RemoveRange(relatedAppointments);
            }

            // Eğitmene bağlı hizmet ilişkilerini sil
            var relatedTrainerServices = await _context.TrainerServices
                .Where(ts => ts.TrainerId == id)
                .ToListAsync();
            if (relatedTrainerServices.Any())
            {
                _context.TrainerServices.RemoveRange(relatedTrainerServices);
            }

            // Eğitmeni sil
            var trainer = await _context.Trainers.FindAsync(id);
            if (trainer != null)
            {
                _context.Trainers.Remove(trainer);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}

