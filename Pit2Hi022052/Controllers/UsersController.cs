using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;

namespace Pit2Hi022052.Controllers
{
    public class UsersController : Controller
    {
        protected virtual ApplicationDbContext Context { get; }
        protected virtual UserManager<ApplicationUser> UserManager { get; }
        protected virtual RoleManager<IdentityRole> RoleManager { get; }

        public UsersController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            Context = context;
            UserManager = userManager;
            RoleManager = roleManager;
        }

        public class InputModel
        {
            [Required]
            [Display(Name = "ID")]
            public string Id { get; set; } = string.Empty;

            [Required]
            [Display(Name = "ユーザ名")]
            public string UserName { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            [Display(Name = "メールアドレス")]
            public string Email { get; set; } = string.Empty;

            [Required]
            [Display(Name = "メールアドレスを確認済みにする")]
            public bool EmailConfirmed { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "{0}は{2}文字以上{1}文字以下です．", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "パスワード")]
            public string Password { get; set; } = "Dummy_Password_0000";

            [DataType(DataType.Password)]
            [Display(Name = "パスワード(確認入力)")]
            [Compare("Password", ErrorMessage = "入力されたパスワードが一致しません．")]
            public string ConfirmPassword { get; set; } = "Dummy_Password_0000";

        }


        public async Task<IActionResult> Index()
        {
            var modelsList = await UserManager.Users.ToListAsync();
            return View(modelsList);
        }

        public async Task<IActionResult> Details(string? id)
        {
            if (!(id is not null)) { return NotFound(); }

            var model = await UserManager.FindByIdAsync(id);
            return View(model);
        }

        public virtual Task<IActionResult> Create([BindNever] InputModel? inputModel)
        {
            inputModel ??= new InputModel();
            return Task.FromResult<IActionResult>(View(inputModel));
        }


        [HttpPost, ActionName("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateConfirmed(InputModel? inputModel)
        {
            try
            {
                if (!ModelState.IsValid) { throw new InvalidDataException(); }
                if (!(inputModel is not null)) { throw new ArgumentNullException(nameof(inputModel)); }
                
                if (UserManager.Users.Any(u => u.Email == inputModel.Email))
                {
                    ModelState.AddModelError(string.Empty,
                        "The email is already registered.");
                    return await Create(inputModel);
                }

                var model = new ApplicationUser()
                {
                    Id = inputModel.Id,
                    UserName = inputModel.UserName,
                    Email = inputModel.Email,
                    EmailConfirmed = inputModel.EmailConfirmed,
                };
                var result = await UserManager.CreateAsync(model, inputModel.Password);
                if (!result.Succeeded) { throw new InvalidOperationException(result.ToString()); }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception e)
            {
                Controllers.AddAllExceptionMessagesToModelError(this, e);
                return await Create(inputModel);
            }
        }


        public async Task<IActionResult> Edit(string? id, [BindNever] InputModel? inputModel)
        {
            if (inputModel is null)
            {
                if (!(id is not null)) { return NotFound(); }
                var model = await UserManager.FindByIdAsync(id);
                inputModel = new InputModel()
                {
                    Id = model.Id,
                    UserName = model.UserName,
                    Email = model.Email,
                    EmailConfirmed = model.EmailConfirmed,
                };
            }
            if (!(inputModel is not null)) { return NotFound(); }
            return await Create(inputModel);
        }

        [HttpPost, ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditConfirmed(string? id, InputModel? inputModel)
        {
            try
            {
                if (!ModelState.IsValid) { throw new InvalidDataException(); }
                if (!(id is not null)) { throw new ArgumentNullException(nameof(id)); }
                if (!(inputModel is not null)) { throw new ArgumentNullException(nameof(inputModel)); }

                var model = await UserManager.FindByIdAsync(id);
                model.Id = inputModel.Id;
                model.UserName = inputModel.UserName;
                model.Email = inputModel.Email;
                model.EmailConfirmed = inputModel.EmailConfirmed;
                if (!(model.Id == id)) { throw new ArgumentException(string.Empty, nameof(id)); }

                var result = await UserManager.UpdateAsync(model);
                if (!result.Succeeded) { throw new InvalidOperationException(result.ToString()); }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception e)
            {
                Controllers.AddAllExceptionMessagesToModelError(this, e);
                return await Edit(id, inputModel);
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
                var model = await UserManager.FindByIdAsync(id);
                var result = await UserManager.DeleteAsync(model);
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
