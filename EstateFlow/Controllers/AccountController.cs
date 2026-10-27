
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using EstateFlow.ViewModels;

namespace EstateFlow.Controllers;

/// <summary>
/// Управлява регистрацията, входа и изхода
/// на потребителите чрез ASP.NET Core Identity.
/// </summary>
public class AccountController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;

    public AccountController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    // ==========================================
    // REGISTER - GET
    // ==========================================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View();
    }

    // ==========================================
    // REGISTER - POST
    // ==========================================

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        // Проверяваме валидността на данните.
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Създаваме потребителски акаунт.
        var user = new IdentityUser
        {
            UserName = model.Email,
            Email = model.Email
        };

        // Identity хешира паролата и записва акаунта в SQLite.
        var result = await _userManager.CreateAsync(
            user,
            model.Password);

        if (result.Succeeded)
        {
            // Автоматичен вход след успешна регистрация.
            await _signInManager.SignInAsync(
                user,
                isPersistent: false);

            return RedirectToAction("Index", "Properties");
        }

        // Показваме грешките от Identity.
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(
                string.Empty,
                error.Description);
        }

        return View(model);
    }

    // ==========================================
    // LOGIN - GET
    // ==========================================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login()
    {
        return View();
    }

    // ==========================================
    // LOGIN - POST
    // ==========================================

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        // Проверяваме валидността на въведените данни.
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Търсим потребителя по имейл.
        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user is not null)
        {
            // Identity проверява паролата и създава сесия.
            var result = await _signInManager.PasswordSignInAsync(
                user,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Properties");
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Профилът е временно заключен. Опитайте по-късно.");

                return View(model);
            }
        }

        // Не уточняваме дали имейлът съществува.
        ModelState.AddModelError(
            string.Empty,
            "Невалиден имейл или парола.");

        return View(model);
    }

    // ==========================================
    // LOGOUT - POST
    // ==========================================

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // Прекратяваме потребителската сесия.
        await _signInManager.SignOutAsync();

        return RedirectToAction("Index", "Properties");
    }
}
