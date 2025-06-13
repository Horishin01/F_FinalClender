/*----------------------------------------------------------
  PublishersController.cs
----------------------------------------------------------*/

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Pit2Hi022999.Data;
using Pit2Hi022999.Models;

namespace Pit2Hi022999.Controllers
{

    //==========================================================
    // PublishersController クラス

    public class PublishersController : Controller
    {
        //--------
        // 基幹処理

        // ApplicationDbContext
        protected virtual ApplicationDbContext Context { get; }

        public PublishersController(ApplicationDbContext context)
        {
            Context = context;
            return;
        }

        //--------
        // アクションメソッド

        public virtual async Task<IActionResult> Index()
        {
            if (!(Context.Publishers is not null)) { return NotFound(); }
            var modelsList = await Context.Publishers
                .Include(m => m.Textbooks)
                .ToListAsync();
            return View(modelsList);
        }

        public virtual async Task<IActionResult> Details(string? id)
        {
            if (!(Context.Publishers is not null)) { return NotFound(); }
            if (!(id is not null)) { return NotFound(); }
            var model = await Context.Publishers
                .Include(m => m.Textbooks)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (!(model is not null)) { return NotFound(); }
            return View(model);
        }

        public virtual Task<IActionResult> Create([BindNever] Publisher? model)
        {
            model ??= new Publisher();
            return Task.FromResult<IActionResult>(View(model));
        }

        [HttpPost, ActionName("Create")]
        [ValidateAntiForgeryToken]
        public virtual async Task<IActionResult> CreateConfirmed(Publisher? model)
        {
            try
            {
                if (!ModelState.IsValid)
                { throw new InvalidDataException(); }
                if (!(model is not null))
                { throw new ArgumentNullException(nameof(model)); }
                Context.Add(model);
                await Context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception e)
            {
                Controllers.AddAllExceptionMessagesToModelError(this, e);
                return await Create(model);
            }
        }

        public virtual async Task<IActionResult> Edit(string? id, [BindNever] Publisher? model)
        {
            if (model is null)
            {
                if (!(Context.Publishers is not null)) { return NotFound(); }
                if (!(id is not null)) { return NotFound(); }
                model = await Context.Publishers.FirstOrDefaultAsync(m => m.Id == id);
            }
            if (!(model is not null)) { return NotFound(); }
            return await Create(model);
        }

        [HttpPost, ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public virtual async Task<IActionResult> EditConfirmed(string? id, Publisher? model)
        {
            try
            {
                if (!ModelState.IsValid)
                { throw new InvalidDataException(); }
                if (!(id is not null))
                { throw new ArgumentNullException(nameof(id)); }
                if (!(model is not null))
                { throw new ArgumentNullException(nameof(model)); }
                if (!(model.Id == id))
                { throw new ArgumentException(string.Empty, nameof(id)); }
                Context.Update(model);
                await Context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception e)
            {
                Controllers.AddAllExceptionMessagesToModelError(this, e);
                return await Edit(id, model);
            }
        }

        public virtual async Task<IActionResult> Delete(string? id)
        {
            return await Details(id);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public virtual async Task<IActionResult> DeleteConfirmed(string? id)
        {
            try
            {
                if (!(Context.Publishers is not null))
                { throw new InvalidOperationException(); }
                if (!(id is not null))
                { throw new ArgumentNullException(nameof(id)); }
                var model = await Context.Publishers.FirstOrDefaultAsync(m => m.Id == id);
                if (!(model is not null))
                { throw new InvalidOperationException(); }
                Context.Remove(model);
                await Context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception e)
            {
                Controllers.AddAllExceptionMessagesToModelError(this, e);
                return RedirectToAction(nameof(Index));
            }
        }

        //--------
        // END
        //--------
    }

    //==========================================================
    // END
    //==========================================================
}
