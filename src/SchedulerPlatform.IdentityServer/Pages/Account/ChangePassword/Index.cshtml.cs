using Duende.IdentityServer;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SchedulerPlatform.IdentityServer.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SchedulerPlatform.IdentityServer.Pages.ChangePassword;

[SecurityHeaders]
[AllowAnonymous]
public class Index : PageModel
{
    private readonly IUserService _userService;
    private readonly IEventService _events;
    private readonly ILogger<Index> _logger;

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public Index(
        IUserService userService,
        IEventService events,
        ILogger<Index> logger)
    {
        _userService = userService;
        _events = events;
        _logger = logger;
    }

    public async Task<IActionResult> OnGet(int userId, string? returnUrl)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            UserId = userId,
            ReturnUrl = returnUrl
        };

        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!ValidatePasswordRequirements(Input.NewPassword))
        {
            ModelState.AddModelError(string.Empty, "Password does not meet requirements. Must be at least 16 characters with 2 special characters, 3 numbers, and 1 uppercase letter.");
            return Page();
        }

        var success = await _userService.ChangePasswordAsync(Input.UserId, Input.CurrentPassword, Input.NewPassword);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, "Failed to change password. Please check your current password and ensure the new password hasn't been used recently.");
            return Page();
        }

        var user = await _userService.GetUserByIdAsync(Input.UserId);
        if (user != null)
        {
            await _events.RaiseAsync(new UserLoginSuccessEvent(user.Email, user.Id.ToString(), user.Email));

            var isuser = new IdentityServerUser(user.Id.ToString())
            {
                DisplayName = user.Email
            };

            await HttpContext.SignInAsync(isuser);

            _logger.LogInformation("Password changed successfully for user {UserId}", Input.UserId);
        }

        if (!string.IsNullOrEmpty(Input.ReturnUrl) && Url.IsLocalUrl(Input.ReturnUrl))
        {
            return Redirect(Input.ReturnUrl);
        }

        return Redirect("~/");
    }

    private bool ValidatePasswordRequirements(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 16)
            return false;

        var specialCharCount = Regex.Matches(password, @"[!@#$%^&*()_+\-=\[\]{}|;:,.<>?]").Count;
        if (specialCharCount < 2)
            return false;

        var digitCount = Regex.Matches(password, @"\d").Count;
        if (digitCount < 3)
            return false;

        var uppercaseCount = Regex.Matches(password, @"[A-Z]").Count;
        if (uppercaseCount < 1)
            return false;

        return true;
    }
}

public class InputModel
{
    public int UserId { get; set; }

    [Required]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Confirm Password")]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
