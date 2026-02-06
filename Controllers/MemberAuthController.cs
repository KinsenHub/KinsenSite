using Microsoft.AspNetCore.Mvc; //Για την δημιουργία Web API controller
using Microsoft.Extensions.Logging;
using System.Threading.Tasks; // Για async κλήσεις
using Umbraco.Cms.Core.Security; // παρέχουν τα 
using Umbraco.Cms.Web.Common.Security; // IMemberManager και IMemberSignInManager.
using Umbraco.Cms.Core.Models.Membership;

namespace KinsenOfficial.Controllers
{
    [ApiController] // Λέει στο ASP.NET ότι αυτός ο controller εξυπηρετεί API (JSON)
    [Route("umbraco/api/member")] // το URL για αυτό το controller
    public class MemberAuthController : ControllerBase
    {
        private readonly IMemberSignInManager _memberSignInManager; // Κάνει login/logout
        private readonly IMemberManager _memberManager; //Διαχειρίζεται τα μέλη (εύρεση, έλεγχος κωδικού, ρόλοι κ.λπ.)
        private readonly ILogger<MemberAuthController> _logger;


        public MemberAuthController(IMemberSignInManager memberSignInManager, IMemberManager memberManager, ILogger<MemberAuthController> logger)
        { //👉 Ο constructor παίρνει τις υπηρεσίες από το dependency injection system του Umbraco.
            _memberSignInManager = memberSignInManager;
            _memberManager = memberManager;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, message = "Συμπλήρωσε email και κωδικό." });
            }

            var member = await _memberManager.FindByEmailAsync(request.Email);
            if (member == null)
            {
                return Unauthorized(new { success = false, message = "Λάθος email ή κωδικός." });
            }

            if (!member.IsApproved)
            {
                return Unauthorized(new { success = false, message = "Μη εγκεκριμένος λογαριασμός." });
            }

            // ✅ Ο ΣΩΣΤΟΣ ΤΡΟΠΟΣ LOGIN
            var result = await _memberSignInManager.PasswordSignInAsync(
                member.UserName,      // ⚠️ ΟΧΙ email
                request.Password,
                request.RememberMe,
                lockoutOnFailure: false
            );

            if (!result.Succeeded)
            {
                return Unauthorized(new { success = false, message = "Λάθος email ή κωδικός." });
            }

            var roles = await _memberManager.GetRolesAsync(member);
            var groupName = roles.FirstOrDefault() ?? "Visitor";

            return Ok(new
            {
                success = true,
                message = "Login successful!",
                group = groupName
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _memberSignInManager.SignOutAsync();
            return Ok(new { success = true, message = "Logout successful!" });
        }

        public class LoginRequest
        {
            public string Email { get; set; }
            public string Password { get; set; }
            public bool RememberMe { get; set; }
        }
    }
}
