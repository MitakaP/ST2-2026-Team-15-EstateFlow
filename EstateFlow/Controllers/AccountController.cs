using EstateFlow.Data;
using EstateFlow.Models;
using EstateFlow.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EstateFlow.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public AccountController(UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        ApplicationDbContext db, IWebHostEnvironment environment)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _environment = environment;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View();

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var nickname = model.Nickname.Trim();
        if (await _db.UserProfiles.AnyAsync(p => p.Nickname.ToLower() == nickname.ToLower()))
        {
            ModelState.AddModelError(nameof(model.Nickname), "Nickname вече се използва.");
            return View(model);
        }

        var user = new IdentityUser { UserName = model.Email, Email = model.Email };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View(model);
        }

        try
        {
            _db.UserProfiles.Add(new UserProfile { UserId = user.Id, Nickname = nickname });
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(nameof(model.Nickname), "Nickname вече се използва. Изберете друг.");
            return View(model);
        }
        await _signInManager.SignInAsync(user, false);
        return RedirectToAction("Index", "Properties");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login() => View();

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is not null)
        {
            var result = await _signInManager.PasswordSignInAsync(user, model.Password,
                model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded) return RedirectToAction("Index", "Properties");
            if (result.IsLockedOut)
            {
                ModelState.AddModelError("", "Профилът е временно заключен.");
                return View(model);
            }
        }
        ModelState.AddModelError("", "Невалиден имейл или парола.");
        return View(model);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Properties");
    }

    [HttpGet, Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await CurrentUserAsync();
        if (user is null) return Challenge();
        var p = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == user.Id);
        return View(new ProfileDisplayViewModel
        {
            Nickname = p?.Nickname ?? user.Email?.Split('@')[0] ?? "Потребител",
            Email = user.Email ?? "",
            FullName = string.Join(" ", new[] { p?.FirstName, p?.MiddleName, p?.LastName }
                .Where(x => !string.IsNullOrWhiteSpace(x))),
            PhoneNumber = user.PhoneNumber,
            Bio = p?.Bio,
            ProfileImagePath = p?.ProfileImagePath
        });
    }

    [HttpGet, Authorize]
    public async Task<IActionResult> EditProfile()
    {
        var user = await CurrentUserAsync();
        if (user is null) return Challenge();
        var p = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == user.Id);
        return View(new ProfileEditViewModel
        {
            Nickname = p?.Nickname ?? "",
            FirstName = p?.FirstName,
            MiddleName = p?.MiddleName,
            LastName = p?.LastName,
            PhoneNumber = user.PhoneNumber,
            Bio = p?.Bio,
            ExistingImagePath = p?.ProfileImagePath
        });
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> EditProfile(ProfileEditViewModel model)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Challenge();
        var p = await _db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == user.Id);
        model.ExistingImagePath = p?.ProfileImagePath;
        if (!ModelState.IsValid) return View(model);
        var nickname = model.Nickname.Trim();
        if (await _db.UserProfiles.AnyAsync(x => x.UserId != user.Id && x.Nickname.ToLower() == nickname.ToLower()))
        {
            ModelState.AddModelError(nameof(model.Nickname), "Nickname вече се използва.");
            return View(model);
        }

        string? savedImage = null;
        if (model.Photo is { Length: > 0 } photo)
        {
            if (photo.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(model.Photo), "Снимката е твърде голяма. Максималният размер е 5 MB.");
                return View(model);
            }
            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            var supported = ext is ".jpg" or ".jpeg" or ".png" or ".webp";
            if (!supported)
            {
                ModelState.AddModelError(nameof(model.Photo), "Позволени са JPG, PNG и WebP.");
                return View(model);
            }
            using var ms = new MemoryStream();
            await photo.CopyToAsync(ms);
            var bytes = ms.ToArray();
            bool valid = ((ext == ".jpg" || ext == ".jpeg") && bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                || (ext == ".png" && bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                || (ext == ".webp" && bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF"
                    && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP");
            if (!valid)
            {
                ModelState.AddModelError(nameof(model.Photo), "Файлът не е валидно изображение.");
                return View(model);
            }
            var directory = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
            Directory.CreateDirectory(directory);
            var filename = $"{Guid.NewGuid():N}{ext}";
            await System.IO.File.WriteAllBytesAsync(Path.Combine(directory, filename), bytes);
            savedImage = $"/uploads/profiles/{filename}";
        }

        p ??= new UserProfile { UserId = user.Id };
        p.Nickname = nickname;
        p.FirstName = model.FirstName?.Trim();
        p.MiddleName = model.MiddleName?.Trim();
        p.LastName = model.LastName?.Trim();
        p.Bio = model.Bio?.Trim();
        if (savedImage is not null) p.ProfileImagePath = savedImage;
        if (_db.Entry(p).State == EntityState.Detached) _db.UserProfiles.Add(p);
        user.PhoneNumber = model.PhoneNumber?.Trim();
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(model);
        }
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(model.Nickname), "Nickname вече се използва.");
            return View(model);
        }
        TempData["Message"] = "Профилът е актуализиран успешно.";
        return RedirectToAction(nameof(Profile));
    }

    private async Task<IdentityUser?> CurrentUserAsync()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return id is null ? null : await _userManager.FindByIdAsync(id);
    }
}
