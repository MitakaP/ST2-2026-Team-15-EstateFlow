
using EstateFlow.Data;
using EstateFlow.Models;
using EstateFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace EstateFlow.Controllers;

public class PropertiesController : Controller
{
    private const int SqliteConstraint = 19;

    private readonly IDatabase _db;
    private readonly IGeminiPropertyService _gemini;

    public PropertiesController(
        IDatabase db,
        IGeminiPropertyService gemini)
    {
        _db = db;
        _gemini = gemini;
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
                explanation = result.Query.Explanation
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
    public IActionResult Details(int id) =>
        _db.GetById(id) is { } property
            ? View(property)
            : NotFound();

    // ==========================================
    // CREATE - AUTHORIZED USERS ONLY
    // ==========================================

    [HttpGet]
    [Authorize]
    public IActionResult Create() => View(new Property());

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Property property)
    {
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
    [Authorize]
    public IActionResult Edit(int id) =>
        _db.GetById(id) is { } property
            ? View(property)
            : NotFound();

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Property property)
    {
        if (id != property.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid ||
            !TrySave(() => _db.Update(property)))
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
    [Authorize]
    public IActionResult Delete(int id) =>
        _db.GetById(id) is { } property
            ? View(property)
            : NotFound();

    [HttpPost]
    [ActionName("Delete")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        if (_db.Delete(id))
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
    [Authorize]
    public IActionResult IsReferenceAvailable(
        string referenceNumber,
        int id) =>
        Json(
            string.IsNullOrWhiteSpace(referenceNumber) ||
            !_db.ReferenceExists(referenceNumber, id)
        );

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
