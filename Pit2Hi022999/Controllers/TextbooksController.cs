/*----------------------------------------------------------
  TextbooksController.cs
----------------------------------------------------------*/

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Pit2Hi022999.Data;
using Pit2Hi022999.Models;

namespace Pit2Hi022999.Controllers;

//==========================================================
// TextbooksController クラス

public class TextbooksController : Controller
{
    //--------
    // 基幹処理
    //--------

    // ApplicationDbContext
    protected virtual ApplicationDbContext Context { get; }

    public TextbooksController(ApplicationDbContext context)
    {
        Context = context;
        return;
    }

    //--------
    // アクションメソッド

    public virtual async Task<IActionResult> Index()
    {
        if (!(Context.Textbooks is not null)) { return NotFound(); }
        var modelsList = await Context.Textbooks
            .Include(m => m.Publisher)
            .ToListAsync();
        return View(modelsList);
    }

    public virtual async Task<IActionResult> Details(string? id)
    {
        if (!(Context.Textbooks is not null)) { return NotFound(); }
        if (!(id is not null)) { return NotFound(); }
        var model = await Context.Textbooks
            .Include(m => m.Publisher)
            .Include(m => m.TextbookAuthors!)
            .ThenInclude(m => m.Author)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (!(model is not null)) { return NotFound(); }
        return View(model);
    }

    public virtual Task<IActionResult> Create([BindNever] Textbook? model)
    {
        ViewData[nameof(Textbook.PublisherId)] = new SelectList(Context.Publishers, nameof(Publisher.Id), nameof(Publisher.IdName));
        model ??= new Textbook();
        return Task.FromResult<IActionResult>(View(model));
    }

    [HttpPost, ActionName("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateConfirmed(Textbook? model)
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

    public async Task<IActionResult> Edit(string? id, [BindNever] Textbook? model)
    {
        if (model is null)
        {
            if (!(Context.Textbooks is not null)) { return NotFound(); }
            if (!(id is not null)) { return NotFound(); }
            model = await Context.Textbooks
                .FirstOrDefaultAsync(m => m.Id == id);
        }
        if (!(model is not null)) { return NotFound(); }
        return await Create(model);
    }

    [HttpPost, ActionName("Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditConfirmed(string? id, Textbook? model)
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
            if (!(Context.Textbooks is not null))
            { throw new InvalidOperationException(); }
            if (!(id is not null))
            { throw new ArgumentNullException(nameof(id)); }
            var model = await Context.Textbooks
                .FirstOrDefaultAsync(m => m.Id == id);
            if (!(model is not null))
            { throw new InvalidOperationException(); }
            Context.Remove(model);
            await Context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        catch (Exception e)
        {
            Controllers.AddAllExceptionMessagesToModelError(this, e);
            return await Delete(id);
        }
    }


    //--------
    // END
    //--------
}

//==========================================================
// END
//==========================================================
