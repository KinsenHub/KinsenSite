using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Security;

[ApiController]
[Route("umbraco/api/forgotpassword")]
public class ForgotPasswordController : ControllerBase
{
    private readonly IMemberService _memberService;
    private readonly ILogger<ForgotPasswordController> _logger;
    private readonly IMemberManager _memberManager;

    public ForgotPasswordController(
        IMemberService memberService,
        IMemberManager memberManager,
        ILogger<ForgotPasswordController> logger)
    {
        _memberService = memberService;
        _memberManager = memberManager;
        _logger = logger;
    }

    // =========================
    // VERIFY RESET CODE
    // =========================
    [HttpPost("verify")]
    public IActionResult Verify([FromForm] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { success = false, message = "Code missing" });

        var member = _memberService
            .GetAllMembers()
            .FirstOrDefault(m =>
                (m.GetValue<string>("passwordResetCode") ?? "") == code
            );

        if (member == null)
        {
            _logger.LogWarning("Invalid reset code attempt: {Code}", code);
            return BadRequest(new { success = false });
        }

        _logger.LogInformation("Reset code verified for member {Email}", member.Email);

        return Ok(new { success = true });
    }

    // ==========================================
    // Στέλνουμε το νέο PASSWORD στο BackOffice!
    // ==========================================
    [HttpPost("reset")]
    public async Task<IActionResult> Reset(
        [FromForm] string code,
        [FromForm] string newPassword)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(newPassword))
            return BadRequest(new { success = false, message = "Missing data" });

        // 🔍 Βρες member από reset code
        var member = _memberService
            .GetAllMembers()
            .FirstOrDefault(m =>
                (m.GetValue<string>("passwordResetCode") ?? "") == code
            );

        if (member == null)
        {
            _logger.LogWarning("Invalid reset code used");
            return BadRequest(new { success = false, message = "Invalid code" });
        }

        // 🔐 Identity member
        var identityMember = await _memberManager.FindByIdAsync(member.Key.ToString());
        if (identityMember == null)
            return BadRequest(new { success = false, message = "Member not found" });

        // 🔑 Generate token
        var token = await _memberManager.GeneratePasswordResetTokenAsync(identityMember);

        // 🔁 Reset password
        var result = await _memberManager.ResetPasswordAsync(
            identityMember,
            token,
            newPassword
        );

        if (!result.Succeeded)
        {
            var errorMessage = string.Join("<br>",
                result.Errors.Select(e => e.Description));

            _logger.LogWarning("Password reset failed: {Errors}", errorMessage);

            return BadRequest(new
            {
                success = false,
                message = errorMessage
            });
        }

        // 🧹 Καθάρισε reset code
        member.SetValue("passwordResetCode", null);
        _memberService.Save(member);

        return Ok(new { success = true });
    }
}