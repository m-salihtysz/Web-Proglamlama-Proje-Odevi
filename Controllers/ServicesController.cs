using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessCenter.Web.Data;
using FitnessCenter.Web.Models;
using FitnessCenter.Web.ViewModels;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ServicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var services = await _context.Services
                .ToListAsync();
            return View(services);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var service = await _context.Services
                .FirstOrDefaultAsync(m => m.Id == id);
            if (service == null)
            {
                return NotFound();
            }

            return View(service);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServiceViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var service = new Service
                {
                    Name = viewModel.Name,
                    Description = viewModel.Description,
                    DurationMinutes = viewModel.DurationMinutes,
                    Price = viewModel.Price
                };
                _context.Add(service);
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

            var service = await _context.Services.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }

            var viewModel = new ServiceViewModel
            {
                Id = service.Id,
                Name = service.Name,
                Description = service.Description,
                DurationMinutes = service.DurationMinutes,
                Price = service.Price
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ServiceViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var service = await _context.Services.FindAsync(id);
                    if (service == null)
                    {
                        return NotFound();
                    }

                    service.Name = viewModel.Name;
                    service.Description = viewModel.Description;
                    service.DurationMinutes = viewModel.DurationMinutes;
                    service.Price = viewModel.Price;

                    _context.Update(service);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    var service = await _context.Services.FindAsync(id);
                    if (service == null)
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

            var service = await _context.Services
                .FirstOrDefaultAsync(m => m.Id == id);
            if (service == null)
            {
                return NotFound();
            }

            return View(service);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                // Önce bu hizmete bağlı randevuları sil
                var appointments = await _context.Appointments
                    .Where(a => a.ServiceId == id)
                    .ToListAsync();
                if (appointments.Any())
                {
                    _context.Appointments.RemoveRange(appointments);
                }

                // Sonra bu hizmete bağlı TrainerService ilişkilerini sil
                var trainerServices = await _context.TrainerServices
                    .Where(ts => ts.ServiceId == id)
                    .ToListAsync();
                if (trainerServices.Any())
                {
                    _context.TrainerServices.RemoveRange(trainerServices);
                }

                // En son hizmeti sil
                _context.Services.Remove(service);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}

