using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Pit2Hi022999.Data;
using Pit2Hi022999.Models;
using Microsoft.AspNetCore.Authorization;


namespace Pit2Hi022999.Controllers
{
    public class RolesController : Controller
    {
        protected virtual ApplicationDbContext Context { get; }
        protected virtual UserManager<ApplicationUser> UserManager { get; }
        protected virtual RoleManager<IdentityRole> RoleManager { get; }

        public RolesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            Context = context;
            UserManager = userManager;
            RoleManager = roleManager;
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Index()
        {
            var modelsList = await RoleManager.Roles.ToListAsync();
            return View(modelsList);
        }

        public async Task<IActionResult> Details(string? id)
        {
            if (!(id is not null)) { return NotFound(); }
            var model = await RoleManager.FindByIdAsync(id);
            return View(model);
        }

        [Authorize(Roles = "admin")]
        public virtual Task<IActionResult> Create([BindNever] IdentityRole? model)
        {
            model ??= new IdentityRole();
            return Task.FromResult<IActionResult>(View(model));
        }

        [Authorize(Roles = "admin")]
        [HttpPost, ActionName("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateConfirmed(IdentityRole? model)
        {
            try
            {
                if (!ModelState.IsValid) { throw new InvalidDataException(); }
                if (!(model is not null)) { throw new ArgumentNullException(nameof(model)); }
                var result = await RoleManager.CreateAsync(model);
                if (!result.Succeeded) { throw new InvalidOperationException(result.ToString()); }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception e)
            {
                Controllers.AddAllExceptionMessagesToModelError(this, e);
                return await Create(model);
            }
        }


        public async Task<IActionResult> Edit(string? id, [BindNever] IdentityRole? model)
        {
            if (model is null)
            {
                if (!(id is not null)) { return NotFound(); }
                model = await RoleManager.FindByIdAsync(id);
            }

            if (!(model is not null)) { return NotFound(); }
            return await Create(model);
        }

        [HttpPost, ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditConfirmed(string? id, IdentityRole? model)
        {
            try
            {
                if (!(id is not null)) { throw new ArgumentNullException(nameof(id)); }
                if (!(model is not null)) { throw new ArgumentNullException(nameof(model)); }
                if (!(model.Id == id)) { throw new ArgumentException(nameof(id)); }

                var result = await RoleManager.UpdateAsync(model);
                if (!result.Succeeded) { throw new InvalidOperationException(result.ToString()); }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception e)
            {
                Controllers.AddAllExceptionMessagesToModelError(this, e);
                return await Edit(id, model);
            }
        }

        public async Task<IActionResult> Delete(string? id)
        {
            return await Details(id);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string? id)
        {
            try
            {
                if (!(id is not null)) { throw new ArgumentNullException(nameof(id)); }
                var model = await RoleManager.FindByIdAsync(id);
                var result = await RoleManager.DeleteAsync(model);
                if (!result.Succeeded) { throw new InvalidOperationException(result.ToString()); }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception e)
            {
                Controllers.AddAllExceptionMessagesToModelError(this, e);
                return await Delete(id);
            }
        }
    }
}
