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
    private readonly PropertyImageService _images;

    public PropertiesController(
        IDatabase db,
        IGeminiPropertyService gemini,
        ApplicationDbContext identityDb,
        PropertyImageService images)
    {
        _db = db;
        _gemini = gemini;
        _identityDb = identityDb;
        _images = images;
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
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create(Property property, CancellationToken cancellationToken)
    {
        property.OwnerId = CurrentUserId;
        if (property.OwnerId is null) return Forbid();
        // ImagePath is assigned by the server only; files cannot be selected by posting a path.
        property.ImagePath = null;
        if (!ModelState.IsValid) return View(property);
        var uploaded = await UploadPhotoAsync(property, cancellationToken);
        if (!ModelState.IsValid) return View(property);
        property.ImagePath = uploaded;
        var saved = false;
        try { saved = TrySave(() => _db.Add(property)); }
        finally { if (!saved) _images.Delete(uploaded); }
        if (!saved)
        {
            property.ImagePath = null;
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
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Edit(int id, Property property, CancellationToken cancellationToken)
    {
        if (id != property.Id)
        {
            return BadRequest();
        }

        var existing = _db.GetById(id);
        if (existing is null) return NotFound();
        if (!CanManage(existing)) return Forbid();
        property.OwnerId = existing.OwnerId; // never trust a posted owner ID

        property.ImagePath = existing.ImagePath;
        if (!ModelState.IsValid) return View(property);
        var uploaded = await UploadPhotoAsync(property, cancellationToken);
        if (!ModelState.IsValid) return View(property);
        property.ImagePath = uploaded ?? (property.RemovePhoto ? null : existing.ImagePath);
        var saved = false;
        try
        {
            saved = TrySave(() =>
            {
                var updated = User.IsInRole(AppRoles.Admin)
                    ? _db.Update(property)
                    : _db.UpdateOwned(property, CurrentUserId!);
                if (!updated) throw new InvalidOperationException("Имотът вече не е достъпен за редакция.");
            });
        }
        finally { if (!saved) _images.Delete(uploaded); }
        if (!saved)
        {
            property.ImagePath = existing.ImagePath;
            return View(property);
        }
        if (property.ImagePath != existing.ImagePath) _images.Delete(existing.ImagePath);

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
            _images.Delete(existing.ImagePath);
            TempData["Message"] =
                "Имотът е изтрит успешно.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> UploadPhotoAsync(Property property, CancellationToken cancellationToken)
    {
        if (property.Photo is null) return null;
        try { return await _images.SaveAsync(property.Photo, cancellationToken); }
        catch (ArgumentException ex) { ModelState.AddModelError(nameof(Property.Photo), ex.Message); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ModelState.AddModelError(nameof(Property.Photo), "Снимката не може да бъде запазена. Опитайте отново.");
        }
        return null;
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
