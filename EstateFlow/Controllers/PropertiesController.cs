
using EstateFlow.Data;
using EstateFlow.Models;
using Microsoft.AspNetCore.Mvc;

namespace EstateFlow.Controllers;

public class PropertiesController : Controller
{
    private readonly IDatabase _database;

    public PropertiesController(IDatabase database)
    {
        _database = database;
    }

    public IActionResult Index(string? city, decimal? maxPrice)
    {
        var properties = _database.GetProperties(city, maxPrice);
        return View(properties);
    }

    // GET: /Properties/Create
    // Отваря празната форма.
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    // POST: /Properties/Create
    // Получава данните от формата и записва имота.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Property property)
    {
        if (!ModelState.IsValid)
        {
            return View(property);
        }

        _database.AddProperty(property);

        TempData["Message"] = "Имотът е добавен успешно.";

        return RedirectToAction(nameof(Index));
    }
}