using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Security;
using System.Text.Json;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;
using Umbraco.Cms.Core.Services;

[ApiController]
[Route("umbraco/api/getcode")]
public class GetCodeController : ControllerBase
{
    private readonly IEmailSender _emailSender;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GetCodeController> _logger;
    private readonly IMemberManager _memberManager;
    private readonly IMemberService _memberService;

    public GetCodeController(
        IEmailSender emailSender,
        IHttpClientFactory httpClientFactory,
        ILogger<GetCodeController> logger,
        IMemberManager memberManager,
        IMemberService memberService)
    {
        _emailSender = emailSender;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _memberManager = memberManager;
        _memberService = memberService;
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromForm] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { success = false, message = "Email is required" });

        // 🔍 1️⃣ ΈΛΕΓΧΟΣ: Υπάρχει member με αυτό το email;
        var identityMember = await _memberManager.FindByEmailAsync(email);

        if (identityMember == null)
        {
            _logger.LogWarning("Password reset requested for NON existing email: {Email}", email);

            return BadRequest(new
            {
                success = false,
                message = "Το email δεν είναι καταχωρημένο στο σύστημα"
            });
        }

         // 🧠 2️⃣ Πάρε το Umbraco Member
        var member = _memberService.GetByKey(identityMember.Key);
        if (member == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Ο λογαριασμός δεν βρέθηκε"
            });
        }

        var tempCode = await GetRandomCodeFromApiAsync();
        _logger.LogInformation("tempcode: " + tempCode);

        // 💾 4️⃣ Αποθήκευση κωδικού στο member
        member.SetValue("passwordResetCode", tempCode);
        _memberService.Save(member);

        var subject = "Ανάκτηση Κωδικού – Kinsen";
        var body = $@"
            <p>Γεια σας,</p>
            <p>Λάβαμε αίτημα ανάκτησης κωδικού.</p>
            <p><strong>Προσωρινός κωδικός:</strong> {tempCode}</p>
            <br/>
            <p>Kinsen Hellas</p>";

        // ✅ Στέλνουμε ΑΠΕΥΘΕΙΑΣ στο email που έγραψε ο χρήστης
        var msg = new EmailMessage(
            null,                   
            new[] { email },
            null, null,             
            null,                   
            subject,
            body,
            true,
            null
        );

        await _emailSender.SendAsync(msg, "ForgotPassword");

        _logger.LogInformation("Forgot password email sent to {Email} with code {Code}", email, tempCode);

        return Ok(new { success = true, message = "Εάν το email υπάρχει, στάλθηκαν οδηγίες ανάκτησης." });
    }

    private async Task<string> GetRandomCodeFromApiAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient();

            // το endpoint που έδωσες
            var url = "https://www.randomnumberapi.com/api/v1.0/randomstring?min=10&max=15&count=1";

            using var res = await client.GetAsync(url);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync();

            // Περιμένουμε ["CODE"]
            var arr = JsonSerializer.Deserialize<string[]>(json);
            var code = arr?.Length > 0 ? arr[0] : null;

            return string.IsNullOrWhiteSpace(code)
                ? Guid.NewGuid().ToString("N")[..10].ToUpper()
                : code;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Random code API failed, using fallback code");
            return Guid.NewGuid().ToString("N")[..10].ToUpper();
        }
    }
}
