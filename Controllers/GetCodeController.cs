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

    private async Task<string> ToBase64ImgTag(string url, string alt, int maxWidth = 300)
    {
        try
        {
            using var http = new HttpClient();
            var bytes = await http.GetByteArrayAsync(url);
            var base64 = Convert.ToBase64String(bytes);
            return $"<img src=\"data:image/jpeg;base64,{base64}\" alt=\"{alt}\" " +
                $"style=\"display:block;width:100%;max-width:{maxWidth}px;height:auto;margin:0 auto;border:0;outline:none;\" />";
        }
        catch
        {
            // fallback αν αποτύχει
            return $"<img src=\"{url}\" alt=\"{alt}\" style=\"display:block;width:100%;max-width:{maxWidth}px;height:auto;margin:0 auto;border:0;outline:none;\" />";
        }
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

        //****************LOGO Kinsen******************
        const string logoUrl = "https://production-job-board-public.s3.amazonaws.com/logos/43021810-0cfb-466e-b00c-46c05fd4b394";
        var logoTag = await ToBase64ImgTag(logoUrl, "Kinsen", 250);

        var subject = "Ανάκτηση Κωδικού";
        var body = $@"
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset='utf-8'>
        <meta name='viewport' content='width=device-width'>
        </head>

        <body style='margin:0;padding:0;background:#ffffff;'>

        <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0'
            style='border-collapse:collapse;background:#ffffff;'>
        <tr>
            <td align='left' style='padding:0;margin:0;'>

            <!-- OUTER FIXED WRAPPER -->
            <table role='presentation' width='600' border='0' cellspacing='0' cellpadding='0'
                    style='border-collapse:collapse;width:600px;max-width:600px;margin:0;'>

                <!-- HEADER : LOGO + TITLE -->
                <tr>
                <td align='left' style='padding:0;margin:0;'>
                    <table role='presentation' border='0' cellspacing='0' cellpadding='0'
                        style='border-collapse:collapse;margin:0;'>
                    
                    <!-- LOGO -->
                    <tr>
                        <td valign='middle' style='padding:0 12px 0 0; margin:0;'>
                        {logoTag}
                        </td>
                    </tr>

                    <!-- TITLE -->
                    <tr>
                        <td valign='middle'
                            style='padding-top:30px;margin:20;'>
                        <span style='font-family:Segoe UI,Roboto,Arial,sans-serif;
                                    font-size:22px;
                                    font-weight:400;
                                    color:#39c0c3;
                                    line-height:1;
                                    white-space:nowrap; margin-top:30px;'>
                            Ανάκτηση Κωδικού Πρόσβασης
                        </span>
                        </td>
                    </tr>
                    </table>
                </td>
                </tr>

                    <!-- TEXT -->
                    <tr>
                        <td align='left'
                            style='font-family:Segoe UI,Roboto,Arial,sans-serif;
                                font-size:14px;
                                color:#000;
                                line-height:1.6;
                                padding:14px 0;
                                margin:0;'>
                            <p style='margin:0 0 8px 0;'>Γεια σας,</p>
                            <p style='margin:0;'>
                                Λάβαμε αίτημα για ανάκτηση του κωδικού πρόσβασής σας στον λογαριασμό
                                <strong>Kinsen</strong>.
                            </p>
                        </td>
                    </tr>

                    <!-- CODE BOX -->
                    <tr>
                        <td align='left' style='padding:0;margin:0;'>
                            <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0'
                                style='border-collapse:collapse;border:1px solid #39c0c3;background:#f4fcfd;'>
                                <tr>
                                    <td align='left'
                                        style='padding:14px 16px;
                                            font-family:Segoe UI,Roboto,Arial,sans-serif;'>
                                        <div style='font-size:14px;color:#000;margin:0 0 6px 0;'>
                                            Ο <strong>προσωρινός κωδικός</strong> σας είναι:
                                        </div>
                                        <div style='font-size:20px;font-weight:700;color:#39c0c3;letter-spacing:1px;'>
                                            {tempCode}
                                        </div>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- INFO -->
                    <tr>
                        <td align='left'
                            style='font-family:Segoe UI,Roboto,Arial,sans-serif;
                                font-size:13px;
                                color:#333;
                                line-height:1.5;
                                padding:14px 0 0 0;
                                margin:0;'>
                            <p style='margin:0;'>
                                Χρησιμοποιήστε τον παραπάνω κωδικό για να ολοκληρώσετε τη διαδικασία
                                και να ορίσετε νέο κωδικό πρόσβασης.
                            </p>
                            <p style='margin:8px 0 0 0;color:#777;'>
                                Αν δεν κάνατε εσείς αυτό το αίτημα, μπορείτε να αγνοήσετε το παρόν email.
                            </p>
                        </td>
                    </tr>

                    <!-- SIGNATURE -->
                    <tr>
                        <td align='left'
                            style='font-family:Segoe UI,Roboto,Arial,sans-serif;
                                font-size:13px;
                                color:#000;
                                padding:18px 0 0 0;
                                margin:0;'>
                            <p style='margin:0;'>Με εκτίμηση,</p>
                            <p style='margin:4px 0 0 0;'><strong>Kinsen Hellas</strong></p>
                        </td>
                    </tr>

                </table>
                <!-- /INNER WRAPPER -->

            </td>
            </tr>
            </table>

            </body>
            </html>";

            var from = "KINSEN <no-reply@kinsen.gr>";

        // ✅ Στέλνουμε ΑΠΕΥΘΕΙΑΣ στο email που έγραψε ο χρήστης
        var msg = new EmailMessage(
            from,                   
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
