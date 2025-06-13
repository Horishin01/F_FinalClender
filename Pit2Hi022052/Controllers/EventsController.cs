using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.ExpressionTranslators.Internal;


namespace Pit2Hi022052.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;
        protected virtual UserManager<ApplicationUser> UserManager { get; }
        public EventsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            UserManager = userManager;
        }

        // カレンダーの表示用アクション
        [Authorize(Roles = "admin,user")]
        public IActionResult Index()
        {
            return View();
        }

        // イベントデータを提供するAPIエンドポイント
        [HttpGet]
        public async Task<JsonResult> GetEvents()
        {
            var currentUser = await UserManager.GetUserAsync(User);
            var events = _context.Events.Select(e => new
            {
                id = e.Id,
                UserId = e.UserId,
                title = e.Title,
                start = e.StartDate.ToString("s"),
                end = e.EndDate.ToString("s"),
                description = e.Description
            }).Where(m => m.UserId == currentUser.Id)
            .ToList();

            return new JsonResult(events);
        }

        // イベント作成ページ (GET)
        [Authorize(Roles = "admin,user")]
        [HttpGet]
        public async Task<IActionResult> Create(string startDate = null, string endDate = null)
        {
            var model = new Event();
            model.Id = Guid.NewGuid().ToString("N");
            var currentUser = await UserManager.GetUserAsync(User);
            model.UserId = currentUser.Id;
            // クエリ文字列で日時が渡された場合、モデルにデフォルト値を設定
            if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var parsedStartDate))
            {
                model.StartDate = parsedStartDate;
            }
            if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var parsedEndDate))
            {
                model.EndDate = parsedEndDate;
            }

            return View(model);
        }

        // イベント作成処理 (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Event model)
        {
            if (ModelState.IsValid)
            {
                // モデルの登録
                _context.Events.Add(model);
                _context.SaveChanges();

                // 一覧ページにリダイレクト
                return RedirectToAction("Index");
            }

            // バリデーションエラー時、入力内容を保持したまま再表示
            return View(model);
        }

        // イベント編集処理
        // GET: Event/Edit/5
        [Authorize(Roles = "admin,user")]

        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var author = await _context.Events.FindAsync(id);
            if (author == null)
            {
                return NotFound();
            }
            return View(author);
        }

        // イベント編集処理 (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Event model)
        {
            if (id != model.Id)
            {
                return BadRequest("IDが一致しません。");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // データベースの状態を更新
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Events.Any(e => e.Id == id))
                    {
                        return NotFound($"指定されたID: {id} のイベントが存在しません。");
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // バリデーションエラー時、編集ページを再表示
            return View(model);
        }

        // イベント詳細ページ
        [Authorize(Roles = "admin,user")]
        public IActionResult Details(string id)
        {
            // IDがnullまたは空の場合、404エラーを返す
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("イベントIDが指定されていません。");
            }

            // 指定されたIDのイベントをデータベースから取得
            var eventDetails = _context.Events.FirstOrDefault(e => e.Id == id);

            // イベントが見つからない場合、404エラーを返す
            if (eventDetails == null)
            {
                return NotFound($"指定されたID: {id} のイベントは存在しません。");
            }

            // ビューにイベントデータを渡して表示
            return View(eventDetails);
        }

        // イベント削除ページ
        [HttpGet]
        public IActionResult Delete(string id)
        {
            var eventToDelete = _context.Events.FirstOrDefault(e => e.Id == id);
            if (eventToDelete == null)
            {
                return NotFound();
            }
            return View(eventToDelete);
        }

        // イベント削除処理
        [Authorize(Roles = "admin,user")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string id, bool confirm)
        {
            var eventToDelete = _context.Events.FirstOrDefault(e => e.Id == id);
            if (eventToDelete != null)
            {
                _context.Events.Remove(eventToDelete);
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }
    }
}