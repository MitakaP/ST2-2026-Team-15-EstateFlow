
using EstateFlow.Data;
using EstateFlow.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace EstateFlow.Controllers;

public class PropertiesController : Controller
{
    private const int SqliteConstraint = 19;

    private readonly IDatabase _db;

    public PropertiesController(IDatabase db) => _db = db;

    public IActionResult Index() => View();

    // JSON за JavaScript таблицата (филтър и пагинация се правят в браузъра).
    [HttpGet]
    public IActionResult List() => Json(_db.GetAll());

    public IActionResult Details(int id) =>
        _db.GetById(id) is { } property ? View(property) : NotFound();

    public IActionResult Create() => View(new Property());

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(Property property)
    {
        if (!ModelState.IsValid || !TrySave(() => _db.Add(property)))
        {
            return View(property);
        }
        TempData["Message"] = $"Имотът с реф. номер „{property.ReferenceNumber}“ е добавен успешно.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id) =>
        _db.GetById(id) is { } property ? View(property) : NotFound();

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Property property)
    {
        if (id != property.Id)
        {
            return BadRequest();
        }
        if (!ModelState.IsValid || !TrySave(() => _db.Update(property)))
        {
            return View(property);
        }
        TempData["Message"] = $"Имотът с реф. номер „{property.ReferenceNumber}“ е обновен.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Delete(int id) =>
        _db.GetById(id) is { } property ? View(property) : NotFound();

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        if (_db.Delete(id))
        {
            TempData["Message"] = "Имотът е изтрит успешно.";
        }
        return RedirectToAction(nameof(Index));
    }

    // Remote валидация (client-side) – връща true, ако стойността е свободна.
    // Параметърът трябва да съвпада по име със свойството в модела (referenceNumber)
    [AcceptVerbs("GET", "POST")]
    public IActionResult IsReferenceAvailable(string referenceNumber, int id) =>
        Json(string.IsNullOrWhiteSpace(referenceNumber) || !_db.ReferenceExists(referenceNumber, id));

    // Ако две заявки минат валидацията едновременно, UNIQUE ограничението в БД ще откаже втората.
    private bool TrySave(Action save)
    {
        try
        {
            save();
            return true;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraint)
        {
            ModelState.AddModelError(string.Empty, "Този референтен номер вече се използва от друг имот.");
            return false;
        }
    }
}