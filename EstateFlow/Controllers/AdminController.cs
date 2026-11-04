using EstateFlow.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstateFlow.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class AdminController : Controller
{
    private readonly UserManager<IdentityUser> _users;

    public AdminController(UserManager<IdentityUser> users) => _users = users;

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var users = await _users.Users.OrderBy(u => u.Email).ToListAsync();
        var model = new List<AdminUserRow>();
        foreach (var user in users)
        {
            model.Add(new AdminUserRow(user.Id, user.Email ?? "", (await _users.GetRolesAsync(user)).ToList()));
        }
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetBroker(string userId, bool isBroker)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest();
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return NotFound();
        if (await _users.IsInRoleAsync(user, AppRoles.Admin)) return Forbid();

        var target = isBroker ? AppRoles.Broker : AppRoles.User;
        var roles = await _users.GetRolesAsync(user);
        var current = roles.Where(r => r == AppRoles.User || r == AppRoles.Broker).ToArray();
        var remove = await _users.RemoveFromRolesAsync(user, current);
        if (!remove.Succeeded)
        {
            TempData["Error"] = "Неуспешна промяна на роля.";
            return RedirectToAction(nameof(Users));
        }
        var result = await _users.AddToRoleAsync(user, target);
        if (!result.Succeeded)
        {
            // Best-effort rollback; never silently claim success.
            await _users.RemoveFromRoleAsync(user, target);
            if (current.Length > 0) await _users.AddToRolesAsync(user, current);
            TempData["Error"] = "Неуспешна промяна на роля.";
            return RedirectToAction(nameof(Users));
        }
        TempData["Message"] = "Потребителската роля е променена.";
        return RedirectToAction(nameof(Users));
    }
}

public sealed record AdminUserRow(string Id, string Email, IReadOnlyList<string> Roles);
