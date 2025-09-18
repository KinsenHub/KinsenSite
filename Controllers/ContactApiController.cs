using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;
using System.Net;
using System.Net.Mail;

[Route("umbraco/api/contactapi")]
public class ContactApiController : UmbracoApiController
{
    private readonly IEmailSender _emailSender;

    public ContactApiController(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public class ContactRequest
    {
        public string FirstName { get; set; } = "";
        public string LastName  { get; set; } = "";
        public string Email     { get; set; } = "";
        public string Message   { get; set; } = "";
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] ContactRequest req)
    {
        if (req is null) return BadRequest("Invalid request.");

        if (string.IsNullOrWhiteSpace(req.FirstName) ||
            string.IsNullOrWhiteSpace(req.LastName)  ||
            string.IsNullOrWhiteSpace(req.Email)     ||
            string.IsNullOrWhiteSpace(req.Message))
        {
            return BadRequest("Missing required fields.");
        }

        // απλός έλεγχος email
        try { _ = new MailAddress(req.Email); }
        catch { return BadRequest("Invalid email."); }

        // ---- συνθέτουμε το email προς Kinsen (βάλε ΕΔΩ το test email σου)
        var to = new[] { "your-test-email@kinsen.gr" }; // TODO: άλλαξέ το για δοκιμή

        var subject = $"Επικοινωνία: {req.FirstName} {req.LastName}";
        var body = $@"
            <h2>Νέο μήνυμα από τη φόρμα επικοινωνίας της KINSEN</h2>
            <p><strong>Όνομα:</strong> {WebUtility.HtmlEncode(req.FirstName)} {WebUtility.HtmlEncode(req.LastName)}</p>
            <p><strong>Email:</strong> {WebUtility.HtmlEncode(req.Email)}</p>
            <p><strong>Μήνυμα:</strong><br/>{WebUtility.HtmlEncode(req.Message).Replace("\n", "<br/><br/>")}</p>";

        var msg = new EmailMessage(
            null,
            new[] { "Eirini.Skliri@kinsen.gr" },
            null, null,
            new[] {req.Email},
            subject,
            body,
            true,
            null 
        );

        try
        {
            await _emailSender.SendAsync(msg, "ContactForm");
            return Ok(new { ok = true });
        }
        catch
        {
            return StatusCode(500, "Send failed.");
        }
    }
}
