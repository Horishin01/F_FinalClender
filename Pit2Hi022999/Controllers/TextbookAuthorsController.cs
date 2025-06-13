using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pit2Hi022999.Controllers;
using Pit2Hi022999.Data;
using Pit2Hi022999.Models;

namespace Pit2Hi022999.Controllers
{
    public class TextbookAuthorsController : Controller
    {
        //private readonly ApplicationDbContext _context;
        protected virtual ApplicationDbContext Context { get; }

        public TextbookAuthorsController(ApplicationDbContext context)
        {
            Context = context;
        }

        // GET: TextbookAuthors
        public async Task<IActionResult> Index()
        {
            if (!(Context.TextbookAuthors is not null)) { return NotFound(); }

            var modelsList = await Context.TextbookAuthors
                .Include(m => m.Textbook)
                .Include(m => m.Author)
                .ToListAsync();

            return View(modelsList);
        }

        // GET: TextbookAuthors/Details/5
        public async Task<IActionResult> Details(string? id, string? id2)
        {
            if (!(Context.TextbookAuthors is not null)) { return NotFound(); }
            if (!(id is not null)) { return NotFound(); }
            if (!(id2 is not null)) { return NotFound(); }

            var textbookId = id;
            var authorId = id2;

            var model = await Context.TextbookAuthors
                .Include(m => m.Textbook)
                .Include(m => m.Author)
                .FirstOrDefaultAsync(m => m.TextbookId == textbookId && m.AuthorId == authorId);
            if (!(model is not null)) { return NotFound(); }
            return View(model);

        }

        // GET: TextbookAuthors/Create
        public virtual Task<IActionResult> Create([BindNever] TextbookAuthor? model)
        {
            ViewData[nameof(TextbookAuthor.TextbookId)] = new SelectList(Context.Textbooks, nameof(Textbook.Id), nameof(Textbook.Name));
            ViewData[nameof(TextbookAuthor.AuthorId)] = new SelectList(Context.Authors, nameof(Author.Id), nameof(Author.Name));
            model ??= new TextbookAuthor();
            return Task.FromResult<IActionResult>(View(model));
        }



        // POST: TextbookAuthors/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.

        [HttpPost, ActionName("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateConfirmed(TextbookAuthor? model)
        {
            try
            {
                if (!ModelState.IsValid) { throw new InvalidDataException(); }
                if (!(model is not null)) { throw new ArgumentNullException(nameof(model)); }
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



        // GET: TextbookAuthors/Edit/5
        public async Task<IActionResult> Edit(string? id, string? id2, [BindNever] TextbookAuthor? model)
        {
            if (model is null)
            {
                if (!(Context.TextbookAuthors is not null))
                {
                    return NotFound();
                }
                if (!(id is not null))
                {
                    return NotFound();
                }
                if (!(id2 is not null))
                {
                    return NotFound();
                }

                var textbookId = id;
                var authorId = id2;

                model = await Context.TextbookAuthors
                    .Include(m => m.Textbook)
                    .Include(m => m.Author)
                    .FirstOrDefaultAsync(m => m.TextbookId == textbookId && m.AuthorId == authorId);
            }
            if (!(model is not null)) { return NotFound(); }
            return await Create(model);
        }


        [HttpPost, ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditConfirmed(string? id, string? id2, TextbookAuthor? model)
        {
            try
            {
                if (!ModelState.IsValid) { throw new InvalidDataException(); }
                if (!(id is not null)) { throw new ArgumentNullException(nameof(id)); }
                if (!(id2 is not null)) { throw new ArgumentNullException(nameof(id2)); }
                if (!(model is not null)) { throw new ArgumentNullException(nameof(model)); }


                var textbookId = id;
                var authorId = id2;

                if (!(model.TextbookId == textbookId)) { throw new ArgumentException(string.Empty, nameof(id)); }
                if (!(model.AuthorId == authorId)) { throw new ArgumentException(string.Empty, nameof(id2)); }

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

        // GET: TextbookAuthors/Delete/5
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
                if (!(Context.TextbookAuthors is not null)) { throw new InvalidOperationException(); }
                if (!(id is not null)) { throw new ArgumentNullException(nameof(id)); }
                if (!(id2 is not null)) { throw new ArgumentNullException(nameof(id2)); }

                var textbookId = id;
                var authorId = id2;

                var model = await Context.TextbookAuthors
                    .FirstOrDefaultAsync(m => m.TextbookId == textbookId && m.AuthorId == authorId);
                if (!(model is not null)) { throw new InvalidOperationException(); }

                Context.Remove(model);
                await Context.SaveChangesAsync();
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
