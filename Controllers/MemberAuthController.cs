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

        [HttpPost("login")] // Κάνει POST στο /umbraco/api/member/login

        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        { // Δέχεται δεδομένα JSON ([FromBody]) με email & password
            var member = await _memberManager.FindByEmailAsync(request.Email); // Ψάχνει τον χρήστη με βάση το email
            if (member == null) // Αν δεν υπάρχει ο χρήστης
            {
                return Unauthorized(new { success = false, message = "Ο χρήστης δεν υπάρχει." });
            }

            if (!member.IsApproved) // Αν ο λογαριασμός είναι μη εγκεκριμένος, μπλοκάρει την είσοδο.
            {
                return Unauthorized(new { success = false, message = "Μη εγκεκριμένος λογαριασμός." });
            }

            // Αν ο λογαριασμός είναι μη εγκεκριμένος, μπλοκάρει την είσοδο.
            var isValidPassword = await _memberManager.CheckPasswordAsync(member, request.Password);
            if (!isValidPassword)
            {
                return Unauthorized(new { success = false, message = "Λάθος email ή κωδικός." });
            }

            // Αν όλα είναι ΟΚ, κάνει sign in το χρήστη
            // false: αμα κλεισει ο browser, αποσυνδέει τον χρήστη
            await _memberSignInManager.SignInAsync(member, false);

            // Παίρνει τον τρέχον χρήστη και επιστρέφει το group/ρόλο του
            var currentMember = await _memberManager.GetCurrentMemberAsync();
            var roles = await _memberManager.GetRolesAsync(currentMember);
            var groupName = roles.FirstOrDefault() ?? "Visitor";

            Response.Cookies.Append("JustRegistered", "true", new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddMinutes(5), 
                HttpOnly = false
            });

            // Επιστρέφει JSON απάντηση με επιτυχία και group (π.χ. για αποθήκευση στο localStorage).
            return Ok(new
            {
                success = true,
                message = "Login successful!",
                group = groupName
                // email = model.Email
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
        }
    }
}
