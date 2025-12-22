// UsersController
// 管理用のユーザー CRUD。UserManager/RoleManager を併用し、メール確認フラグやロール付与を含めて編集できる。

﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using TimeLedger.Data;
using TimeLedger.Models;

namespace TimeLedger.Controllers
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
            var model = await FindUserOrDefaultAsync(id);
            if (model is null) { return NotFound(); }
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
            return await ExecuteWithModelErrorHandlingAsync(
                () => CreateUserInternalAsync(inputModel),
                () => Create(inputModel));
        }


        public async Task<IActionResult> Edit(string? id, [BindNever] InputModel? inputModel)
        {
            if (inputModel is null)
            {
                var model = await FindUserOrDefaultAsync(id);
                if (model is null) { return NotFound(); }
                inputModel = BuildInputModel(model);
            }
            return await Create(inputModel);
        }

        [HttpPost, ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditConfirmed(string? id, InputModel? inputModel)
        {
            return await ExecuteWithModelErrorHandlingAsync(
                () => UpdateUserInternalAsync(id, inputModel),
                () => Edit(id, inputModel));
        }

        public async Task<IActionResult> Delete(string? id)
        {
            var model = await FindUserOrDefaultAsync(id);
            if (model is null) { return NotFound(); }
            return View(model);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string? id)
        {
            return await ExecuteWithModelErrorHandlingAsync(
                () => DeleteUserInternalAsync(id),
                () => Delete(id));
        }

        protected virtual InputModel BuildInputModel(ApplicationUser user)
        {
            return new InputModel()
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed,
            };
        }

        protected virtual ApplicationUser CreateUserFromInput(InputModel inputModel)
        {
            var user = new ApplicationUser();
            ApplyInputModelToUser(inputModel, user);
            return user;
        }

        protected virtual void ApplyInputModelToUser(InputModel inputModel, ApplicationUser user)
        {
            user.Id = inputModel.Id;
            user.UserName = inputModel.UserName;
            user.Email = inputModel.Email;
            user.EmailConfirmed = inputModel.EmailConfirmed;
        }

        protected virtual async Task EnsureEmailUniqueAsync(string email, string? currentUserId = null)
        {
            var normalizedEmail = UserManager.NormalizeEmail(email) ?? email;
            var existingUser = await UserManager.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
            if (existingUser is not null && existingUser.Id != currentUserId)
            {
                throw new ValidationException("The email is already registered.");
            }
        }

        protected virtual async Task<IdentityResult> EnsureDefaultRoleAsync(ApplicationUser user)
        {
            if (!await RoleManager.RoleExistsAsync(RoleNames.User))
            {
                var createRoleResult = await RoleManager.CreateAsync(new IdentityRole(RoleNames.User));
                if (!createRoleResult.Succeeded)
                {
                    return createRoleResult;
                }
            }

            if (await UserManager.IsInRoleAsync(user, RoleNames.User))
            {
                return IdentityResult.Success;
            }

            return await UserManager.AddToRoleAsync(user, RoleNames.User);
        }

        protected virtual async Task<ApplicationUser?> FindUserOrDefaultAsync(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) { return null; }
            return await UserManager.FindByIdAsync(id);
        }

        protected virtual async Task<ApplicationUser> LoadUserOrThrowAsync(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            var user = await UserManager.FindByIdAsync(id);
            if (user is null)
            {
                throw new KeyNotFoundException($"User '{id}' was not found.");
            }

            return user;
        }

        private InputModel RequireInputModel(InputModel? inputModel)
        {
            return inputModel ?? throw new ArgumentNullException(nameof(inputModel));
        }

        private void EnsureModelStateIsValid()
        {
            if (ModelState.IsValid) { return; }
            throw new ValidationException("入力内容をご確認ください。");
        }

        private static void ValidateIdentifierMatches(string? id, string userId)
        {
            if (!string.Equals(userId, id, StringComparison.Ordinal))
            {
                throw new ArgumentException("User id mismatch.", nameof(id));
            }
        }

        private async Task EnsureSucceededAsync(Task<IdentityResult> operation)
        {
            var result = await operation;
            if (result.Succeeded) { return; }

            var description = result.Errors.Any()
                ? string.Join("; ", result.Errors.Select(e => e.Description))
                : result.ToString();
            throw new InvalidOperationException(description);
        }

        private async Task<IActionResult> CreateUserInternalAsync(InputModel? inputModel)
        {
            var model = RequireInputModel(inputModel);
            EnsureModelStateIsValid();

            await EnsureEmailUniqueAsync(model.Email);
            var user = CreateUserFromInput(model);

            await EnsureSucceededAsync(UserManager.CreateAsync(user, model.Password));
            await EnsureSucceededAsync(EnsureDefaultRoleAsync(user));

            return RedirectToIndex();
        }

        private async Task<IActionResult> UpdateUserInternalAsync(string? id, InputModel? inputModel)
        {
            var model = RequireInputModel(inputModel);
            EnsureModelStateIsValid();

            var user = await LoadUserOrThrowAsync(id);
            await EnsureEmailUniqueAsync(model.Email, user.Id);
            ApplyInputModelToUser(model, user);
            ValidateIdentifierMatches(id, user.Id);

            await EnsureSucceededAsync(UserManager.UpdateAsync(user));
            return RedirectToIndex();
        }

        private async Task<IActionResult> DeleteUserInternalAsync(string? id)
        {
            var user = await LoadUserOrThrowAsync(id);
            await EnsureSucceededAsync(UserManager.DeleteAsync(user));
            return RedirectToIndex();
        }

        private async Task<IActionResult> ExecuteWithModelErrorHandlingAsync(
            Func<Task<IActionResult>> action,
            Func<Task<IActionResult>> onError)
        {
            try
            {
                return await action();
            }
            catch (Exception e)
            {
                Controllers.AddAllExceptionMessagesToModelError(this, e);
                return await onError();
            }
        }

        private IActionResult RedirectToIndex() => RedirectToAction(nameof(Index));
    }
}
