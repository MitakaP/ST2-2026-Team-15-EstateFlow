using System.Security.Claims;
using EstateFlow.Data;
using EstateFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstateFlow.Controllers;

[Authorize]
public class FavoritesController : Controller
{
    private readonly ApplicationDbContext _identityDb;
    private readonly IDatabase _properties;

    public FavoritesController(ApplicationDbContext identityDb, IDatabase properties)
    {
        _identityDb = identityDb;
        _properties = properties;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var ids = await _identityDb.FavoriteProperties.AsNoTracking()
            .Where(f => f.UserId == CurrentUserId)
            .OrderByDescending(f => f.AddedAtUtc)
            .Select(f => f.PropertyId).ToListAsync();

        // Old favorites whose property was removed aren't displayed.
        var properties = ids.Select(id => _properties.GetById(id))
            .Where(p => p is not null).Cast<Property>().ToList();
        return View(properties);
    }

    [HttpGet]
    public async Task<IActionResult> Ids()
    {
        var ids = await _identityDb.FavoriteProperties.AsNoTracking()
            .Where(f => f.UserId == CurrentUserId)
            .Select(f => f.PropertyId).ToListAsync();
        return Json(ids);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int propertyId, string? returnUrl = null)
    {
        if (propertyId <= 0 || _properties.GetById(propertyId) is null)
            return NotFound();

        var favorite = await _identityDb.FavoriteProperties.FindAsync(CurrentUserId, propertyId);
        bool isFavorite;
        if (favorite is null)
        {
            _identityDb.FavoriteProperties.Add(new FavoriteProperty
            {
                UserId = CurrentUserId,
                PropertyId = propertyId
            });
            isFavorite = true;
        }
        else
        {
            _identityDb.FavoriteProperties.Remove(favorite);
            isFavorite = false;
        }
        await _identityDb.SaveChangesAsync();

        if (Request.Headers.Accept.Any(x => x?.Contains("application/json") == true))
            return Json(new { propertyId, isFavorite });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction("Details", "Properties", new { id = propertyId });
    }
}
