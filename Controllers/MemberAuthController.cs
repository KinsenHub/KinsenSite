using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Security;
using Umbraco.Cms.Core.Services;

namespace Kinsen.Web.Api
{
    [Route("umbraco/api/member")]
    public class MemberAuthController : UmbracoApiController
    {
        private readonly IMemberSignInManager _signInManager;
        private readonly IMemberManager _memberManager;
        private readonly IMemberService _memberService;
        private readonly ILogger<MemberAuthController> _logger;

        public MemberAuthController(
            IMemberSignInManager signInManager,
            IMemberManager memberManager,
            IMemberService memberService,
            ILogger<MemberAuthController> logger)
        {
            _signInManager = signInManager;
            _memberManager = memberManager;
            _memberService = memberService;
            _logger = logger;
        }

        // =========================
        // LOGIN
        // =========================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            _logger.LogWarning("🔐 LOGIN HIT | Email={Email}", request?.Email);

            if (request == null ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Missing credentials");
            }

            var memberIdentity = await _memberManager.FindByEmailAsync(request.Email);
            if (memberIdentity == null)
                return Unauthorized("Invalid credentials");

            var umbMember = _memberService.GetByKey(memberIdentity.Key);
            if (umbMember == null || !umbMember.IsApproved)
                return Unauthorized("Member not approved");

            // ✅ ΤΟ ΜΟΝΟ ΣΩΣΤΟ LOGIN
            var result = await _signInManager.PasswordSignInAsync(
                memberIdentity.UserName,   // ΟΧΙ email
                request.Password,
                request.RememberMe,
                lockoutOnFailure: false
            );

            if (!result.Succeeded)
                return Unauthorized("Invalid credentials");

            // 🔍 επιβεβαίωση
            var current = await _memberManager.GetCurrentMemberAsync();
            _logger.LogWarning(
                "LOGIN OK | CurrentMember={Member}",
                current != null ? $"Id={current.Id}" : "NULL"
            );

            return Ok(new { success = true });
        }

        // =========================
        // LOGOUT
        // =========================
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogWarning("🚪 LOGOUT OK");
            return Ok(new { success = true });
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public bool RememberMe { get; set; }
    }
}
