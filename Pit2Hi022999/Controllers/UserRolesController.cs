using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pit2Hi022999.Data;
using Pit2Hi022999.Models;

namespace Pit2Hi022999.Controllers
{
    public class UserRolesController : Controller
    {
        protected virtual ApplicationDbContext Context { get; }
        protected virtual UserManager<ApplicationUser> UserManager { get; }
        protected virtual RoleManager<IdentityRole> RoleManager { get; }

        public UserRolesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            Context = context;
            UserManager = userManager;
            RoleManager = roleManager;
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Index()
        {
            var modelsList = await Context.UserRoles.ToListAsync();
            return View(modelsList);
        }

        public async Task<IActionResult> Details(string? id, string? id2)
        {
            if (!(id is not null)) { return NotFound(); }
            if (!(id2 is not null)) { return NotFound(); }

            var userId = id;
            var roleId = id2;

            var model = await Context.UserRoles.FirstOrDefaultAsync(m => m.UserId == userId && m.RoleId == roleId);
            return View(model);
        }

        public virtual Task<IActionResult> Create([BindNever] IdentityUserRole<string>? model)
        {
            ViewData[nameof(IdentityUserRole<string>.UserId)] =
                new SelectList(UserManager.Users, nameof(ApplicationUser.Id), nameof(ApplicationUser.UserName));
            ViewData[nameof(IdentityUserRole<string>.RoleId)] =
                new SelectList(RoleManager.Roles, nameof(IdentityRole.Id), nameof(IdentityRole.Name));

            model ??= new IdentityUserRole<string>();
            return Task.FromResult<IActionResult>(View(model));
        }


        [HttpPost, ActionName("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateConfirmed(IdentityUserRole<string>? model)
        {
            try
            {
                if (!ModelState.IsValid) { throw new InvalidDataException(); }
                if (!(model is not null)) { throw new ArgumentNullException(nameof(model)); }

                var user = await UserManager.FindByIdAsync(model.UserId);
                var role = await RoleManager.FindByIdAsync(model.RoleId);

                var result = await UserManager.AddToRoleAsync(user, role.Name);
                if (!result.Succeeded) { throw new InvalidOperationException(result.ToString()); }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception e)
            {
                Controllers.AddAllExceptionMessagesToModelError(this, e);
                return await Create(model);
            }
        }


        public async Task<IActionResult> Edit(string? id, string? id2, [BindNever] IdentityUserRole<string>? model)
        {
            if (model is null)
            {
                if (!(id is not null)) { return NotFound(); }
                if (!(id2 is not null)) { return NotFound(); }

                var userId = id;
                var roleId = id2;

               model = await Context.UserRoles.FirstOrDefaultAsync(m => m.UserId == userId && m.RoleId == roleId);
            }
            if (!(model is not null)) { return NotFound(); }
            return await Create(model);
        }

        [HttpPost, ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditConfirmed(string? id, string? id2, IdentityUserRole<string>? model)
        {
            try
            {
                if (!ModelState.IsValid) { throw new InvalidDataException(); }
                if (!(id is not null)) { throw new ArgumentNullException(nameof(id)); }
                if (!(id2 is not null)) { throw new ArgumentNullException(nameof(id2)); }
                if (!(model is not null)) { throw new ArgumentNullException(nameof(model)); }

                var userId = id;
                var roleId = id2;

                if (!(model.UserId == userId)) { throw new ArgumentException(string.Empty, nameof(id)); }
                if (!(model.RoleId == roleId)) { throw new ArgumentException(string.Empty, nameof(id2)); }

                Context.Update(model);
                await Context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception e)
            {
                Controllers.AddAllExceptionMessagesToModelError(this, e);
                return await Edit(id, id2, model);
            }
        }

        public async Task<IActionResult> Delete(string? id, string? id2)
        {
            return await Details(id, id2);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string? id, string? id2)
        {
            try
            {
                if (!(id is not null)) { throw new ArgumentNullException(nameof(id)); }
                if (!(id2 is not null)) { throw new ArgumentNullException(nameof(id2)); }

                var userId = id;
                var roleId = id2;

                var user = await UserManager.FindByIdAsync(userId);
                var role = await RoleManager.FindByIdAsync(roleId);

                var result = await UserManager.RemoveFromRoleAsync(user, role.Name);
                if (!result.Succeeded) { throw new InvalidOperationException(result.ToString()); }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception e)
            {
                Controllers.AddAllExceptionMessagesToModelError(this, e);
                return await Delete(id, id2);
            }
        }
    }
}
