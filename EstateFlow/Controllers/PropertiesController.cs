using System.Security.Claims;
using EstateFlow.Data;
using Microsoft.EntityFrameworkCore;
using EstateFlow.Models;
using EstateFlow.Services;
using EstateFlow.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace EstateFlow.Controllers;

public class PropertiesController : Controller
{
    private const int SqliteConstraint = 19;

    private readonly IDatabase _db;
    private readonly ApplicationDbContext _identityDb;
    private readonly IGeminiPropertyService _gemini;

    public PropertiesController(
        IDatabase db,
        IGeminiPropertyService gemini,
        ApplicationDbContext identityDb)
    {
        _db = db;
        _gemini = gemini;
        _identityDb = identityDb;
    }

    /// <summary>
    /// AI translates natural-language criteria to an
    /// allow-listed, parameterized DB search.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AiSearch(
        [FromForm] string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 500)
        {
            return BadRequest(new
            {
                error = "Въведете заявка до 500 символа."
            });
        }

        var result = await _gemini.GenerateAsync(
            query,
            cancellationToken);

        if (!result.IsSuccess || result.Query is null)
        {
            return BadRequest(new
            {
                error = result.Error ?? "Невалидна заявка."
            });
        }

        try
        {
            var command =
                PropertyQuerySqlTranslator.Translate(result.Query);

            return Json(new
            {
                properties = _db.Search(command),
                explanation = result.Query.Explanation,
                provider = result.Provider,
                usedFallback = result.UsedFallback
            });
        }
        catch (ArgumentException)
        {
            return BadRequest(new
            {
                error = "ИИ върна непозволени критерии за търсене."
            });
        }
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private bool CanManage(Property property) =>
        User.IsInRole(AppRoles.Admin) ||
        (User.IsInRole(AppRoles.Broker) && CurrentUserId is { } userId && property.OwnerId == userId);

    [HttpGet]
    [Authorize(Roles = AppRoles.Broker + "," + AppRoles.Admin)]
    public IActionResult MyProperties()
    {
        var id = CurrentUserId;
        return View(_db.GetAll().Where(p => p.OwnerId == id && id != null).ToList());
    }

    // ==========================================
    // INDEX - PUBLIC
    // ==========================================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Index() => View();

    // JSON за JavaScript таблицата.
    [HttpGet]
    [AllowAnonymous]
    public IActionResult List() => Json(_db.GetAll());

    // ==========================================
    // DETAILS - PUBLIC
    // ==========================================

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var property = _db.GetById(id);
        if (property is null) return NotFound();

        var userId = CurrentUserId;
        ViewData["IsFavorite"] = userId is not null &&
            await _identityDb.FavoriteProperties.AsNoTracking()
                .AnyAsync(f => f.UserId == userId && f.PropertyId == id);

        return View(property);
    }

    // ==========================================
    // CREATE - AUTHORIZED USERS ONLY
    // ==========================================

    [HttpGet]
    [Authorize(Roles = AppRoles.Broker + "," + AppRoles.Admin)]
    public IActionResult Create() => View(new Property());

    [HttpPost]
    [Authorize(Roles = AppRoles.Broker + "," + AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Property property)
    {
        property.OwnerId = CurrentUserId;
        if (property.OwnerId is null) return Forbid();
        if (!ModelState.IsValid ||
            !TrySave(() => _db.Add(property)))
        {
            return View(property);
        }

        TempData["Message"] =
            $"Имотът с реф. номер „{property.ReferenceNumber}“ е добавен успешно.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // EDIT - AUTHORIZED USERS ONLY
    // ==========================================

    [HttpGet]
    [Authorize(Roles = AppRoles.Broker + "," + AppRoles.Admin)]
    public IActionResult Edit(int id)
    {
        var property = _db.GetById(id);
        if (property is null) return NotFound();
        if (!CanManage(property)) return Forbid();
        return View(property);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Broker + "," + AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Property property)
    {
        if (id != property.Id)
        {
            return BadRequest();
        }

        var existing = _db.GetById(id);
        if (existing is null) return NotFound();
        if (!CanManage(existing)) return Forbid();
        property.OwnerId = existing.OwnerId; // never trust a posted owner ID

        if (!ModelState.IsValid ||
            !TrySave(() =>
            {
                var saved = User.IsInRole(AppRoles.Admin)
                    ? _db.Update(property)
                    : _db.UpdateOwned(property, CurrentUserId!);
                if (!saved) throw new InvalidOperationException("Имотът вече не е достъпен за редакция.");
            }))
        {
            return View(property);
        }

        TempData["Message"] =
            $"Имотът с реф. номер „{property.ReferenceNumber}“ е обновен.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // DELETE - AUTHORIZED USERS ONLY
    // ==========================================

    [HttpGet]
    [Authorize(Roles = AppRoles.Broker + "," + AppRoles.Admin)]
    public IActionResult Delete(int id)
    {
        var property = _db.GetById(id);
        if (property is null) return NotFound();
        if (!CanManage(property)) return Forbid();
        return View(property);
    }

    [HttpPost]
    [ActionName("Delete")]
    [Authorize(Roles = AppRoles.Broker + "," + AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        var existing = _db.GetById(id);
        if (existing is null) return NotFound();
        if (!CanManage(existing)) return Forbid();

        var deleted = User.IsInRole(AppRoles.Admin)
            ? _db.Delete(id)
            : _db.DeleteOwned(id, CurrentUserId!);
        if (deleted)
        {
            TempData["Message"] =
                "Имотът е изтрит успешно.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // REMOTE VALIDATION
    // ==========================================

    /// <summary>
    /// Проверява дали референтният номер е свободен.
    /// </summary>
    [AcceptVerbs("GET", "POST")]
    [Authorize(Roles = AppRoles.Broker + "," + AppRoles.Admin)]
    public IActionResult IsReferenceAvailable(string referenceNumber, int id)
    {
        if (id != 0)
        {
            var existing = _db.GetById(id);
            if (existing is null || !CanManage(existing)) return Forbid();
        }
        return Json(string.IsNullOrWhiteSpace(referenceNumber) ||
                    !_db.ReferenceExists(referenceNumber, id));
    }

    // ==========================================
    // DATABASE VALIDATION
    // ==========================================

    /// <summary>
    /// Обработва нарушение на UNIQUE ограничението
    /// при едновременни заявки.
    /// </summary>
    private bool TrySave(Action save)
    {
        try
        {
            save();
            return true;
        }
        catch (SqliteException ex)
            when (ex.SqliteErrorCode == SqliteConstraint)
        {
            ModelState.AddModelError(
                string.Empty,
                "Този референтен номер вече се използва от друг имот.");

            return false;
        }
    }
}
