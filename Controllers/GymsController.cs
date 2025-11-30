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

        // GET: Gyms
        public async Task<IActionResult> Index()
        {
            var gyms = await _context.Gyms
                .Include(g => g.Services)
                .Include(g => g.Trainers)
                .ToListAsync();
            return View(gyms);
        }

        // GET: Gyms/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var gym = await _context.Gyms
                .Include(g => g.Services)
                .Include(g => g.Trainers)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (gym == null)
            {
                return NotFound();
            }

            return View(gym);
        }

        // GET: Gyms/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Gyms/Create
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

        // GET: Gyms/Edit/5
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

        // POST: Gyms/Edit/5
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
                    if (!GymExists(viewModel.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(viewModel);
        }

        // GET: Gyms/Delete/5
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

        // POST: Gyms/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var gym = await _context.Gyms.FindAsync(id);
            if (gym != null)
            {
                _context.Gyms.Remove(gym);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool GymExists(int id)
        {
            return _context.Gyms.Any(e => e.Id == id);
        }
    }
}

