using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;
using System.Globalization;
using Umbraco.Cms.Web.Common.Security;
using Umbraco.Cms.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using Microsoft.Extensions.Logging;


[ApiController]
[Route("umbraco/api/modaloffermemberapi")]
public class ModalOfferMemberApiController : Controller
{
  private readonly IEmailSender _emailSender;
  private readonly IMemberService _memberService;
  private readonly MemberManager _memberManager;
  private readonly ILogger<ModalOfferMemberApiController> _logger;

  public ModalOfferMemberApiController(IEmailSender emailSender, IMemberService memberService, MemberManager memberManager, ILogger<ModalOfferMemberApiController> logger)
  {
    _emailSender = emailSender;
    _memberService = memberService;
    _memberManager = memberManager;
    _logger = logger;
  }

  public class OfferRequest
  {
    public string FirstName { get; set; } = "";
    public string LastName  { get; set; } = "";
    public string Email     { get; set; } = "";
    public string Phone     { get; set; } = "";
    public List<CartItem> Cars { get; set; } = new();
  }

  public class CartItem
  {
    public string Id { get; set; } = "";
    public string Maker { get; set; } = "";
    public string Model { get; set; } = "";
    public string PriceText { get; set; } = "";
    public string Img { get; set; } = "";
    public int? Year { get; set; }
    public int? Km { get; set; }
    public string Fuel { get; set; } = "";
    public int? Cc { get; set; }
    public int? Hp { get; set; }
    public string Color { get; set; } = "";
  }

  // Helper για Base64 εικόνα
    private async Task<string> BuildCarImageTag(string imageUrl, string alt, int maxWidth = 300)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return "";

        // ABSOLUTE URL
        var carUrl = Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute)
            ? imageUrl
            : $"{Request.Scheme}://{Request.Host}{imageUrl}";

        bool isLocal = string.Equals(Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase);

        if (isLocal)
        {
            string relative = carUrl;
            if (Uri.TryCreate(carUrl, UriKind.Absolute, out var abs))
                relative = abs.LocalPath;

            string localPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
            );

            byte[]? bytes = null;

            try
            {
                if (System.IO.File.Exists(localPath))
                {
                    bytes = await System.IO.File.ReadAllBytesAsync(localPath);
                }
                else
                {
                    using var http = new HttpClient();
                    bytes = await http.GetByteArrayAsync(carUrl);
                }
            }
            catch { }

            if (bytes != null && bytes.Length > 0)
            {
                string ext = Path.GetExtension(relative).ToLowerInvariant();
                string mime = ext switch
                {
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    _ => "image/jpeg"
                };

                var b64 = Convert.ToBase64String(bytes);
                return
                    $"<img src='data:{mime};base64,{b64}' alt='{alt}' " +
                    $"style='display:block;width:100%;max-width:{maxWidth}px;height:auto;border:0;outline:none;text-decoration:none;' />";
            }
        }

        // PRODUCTION ή fallback
        return
            $"<img src='{carUrl}' alt='{alt}' " +
            $"style='display:block;width:100%;max-width:{maxWidth}px;height:auto;border:0;outline:none;text-decoration:none;' />";
    }
  
  private static string FormatPriceGr(string? raw)
  {
    if (string.IsNullOrWhiteSpace(raw)) return "-";

    // κράτα μόνο ψηφία (βγάζει €, τελείες, κόμματα κλπ)
    var digits = new string(raw.Where(char.IsDigit).ToArray());

    if (!long.TryParse(digits, out var value))
        return raw;

    return value.ToString("N0", new CultureInfo("el-GR")); // 30.500
  }

  [HttpPost("send")]
  public async Task<IActionResult> Send([FromBody] OfferRequest request)
  {
    var identityMember = await _memberManager.GetCurrentMemberAsync();
    if (identityMember == null)
        return Unauthorized("No logged-in member");

    var umbracoMember = _memberService.GetByKey(identityMember.Key);
    if (umbracoMember == null)
        return Unauthorized("Member not found");

    // ✅ ΣΩΣΤΑ ΣΤΟΙΧΕΙΑ
    var firstName = identityMember.UserName ?? ""; 
    var lastName = umbracoMember.GetValue<string>("lastName") ?? "";
    var phone = umbracoMember.GetValue<string>("phone") ?? "";
    var afm = umbracoMember.GetValue<string>("afm") ?? "";
    var companyName = umbracoMember.GetValue<string>("companyName") ?? "-";
    var email = identityMember.Email ?? "";
    var customerFullName = $"{firstName} {lastName}".Trim();

    var imgTag = string.Empty;
        
    if (request == null || request.Cars == null || request.Cars.Count == 0)
        return BadRequest("Το καλάθι είναι άδειο.");

    // === LOGO Kinsen ===
    const string logoUrl = "https://production-job-board-public.s3.amazonaws.com/logos/43021810-0cfb-466e-b00c-46c05fd4b394";
    var logoTag = await BuildCarImageTag(logoUrl, "Kinsen", 250);

    // === Κάρτες αυτοκινήτων ===
    var carCardsHtml = string.Join("",
        await Task.WhenAll(request.Cars.Select(async c => $@"
        <table role='presentation' border='0' cellspacing='0' cellpadding='0' align='center'
              style='margin:15px auto;width:100%;max-width:600px;border:1px solid #ddd;
                      border-radius:10px;overflow:hidden;background:#ffffff;'>
          <tr>

            <!-- Εικόνα αριστερά -->
            <td width='240' align='center' style='height:180px;'>
              {await BuildCarImageTag(c.Img, $"{c.Maker} {c.Model}", 220)}
            </td>

            <!-- Στοιχεία δεξιά -->
            <td style='padding:12px;vertical-align:top;
                      font-family:Segoe UI,Roboto,Arial,sans-serif;color:#000000;'>

              <div style='font-size:18px;font-weight:700;margin-bottom:6px;color:#023859;'>
                {c.Maker} {c.Model}
              </div>

              <table role='presentation' border='0' cellspacing='0' cellpadding='0' style='width:100%;'>
                <tr>

                  <!-- Αριστερή στήλη -->
                  <td valign='top' style='width:50%;font-size:13px;color:#333;line-height:1.5;padding-right:10px;'>       
                    • {(c.Year.HasValue ? c.Year.Value.ToString() : "-")} <br>
                    • {(c.Cc.HasValue ? c.Cc + " cc" : "-")} <br>
                    • {(c.Hp.HasValue ? c.Hp + " hp" : "-")}
                  </td>

                  <!-- Δεξιά στήλη -->
                  <td valign='top' style='width:50%;font-size:13px;color:#333;line-height:1.5;padding-left:10px;'>         
                    • {(c.Km.HasValue ? c.Km + " km" : "-")} <br>
                    • {(string.IsNullOrWhiteSpace(c.Fuel) ? "-" : c.Fuel)} <br>
                    • {(string.IsNullOrWhiteSpace(c.Color) ? "-" : c.Color)}
                  </td>

                </tr>
              </table>

              <div style='font-size:16px;font-weight:600;color:#007c91;margin-top:15px;'>
                {FormatPriceGr(c.PriceText)} €
              </div>

            </td>
          </tr>
        </table>
      ")));

    // ============ Email προς Πελάτη ================
    var companyEmail = "sales@kinsen.gr";
    var companyPhone = "+30 211 190 3000";
    var companyAddress = "Λεωφόρος Αθηνών 71, Αθήνα";
    var cookiesUrl = $"{Request.Scheme}://{Request.Host}/cookies";
    var termsUrl = $"{Request.Scheme}://{Request.Host}/terms";

    var customerSubject = "📩 Επιβεβαίωση αιτήματος";
    var customerBody = $@"
    <!doctype html>
    <html xmlns='http://www.w3.org/1999/xhtml'>
    <head>
      <meta http-equiv='Content-Type' content='text/html; charset=UTF-8' />
      <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
      <title>Kinsen</title>
    </head>

    <body style='margin:0;padding:0;background:#ffffff;color:#000000;'>

      <!-- OUTER WRAPPER -->
      <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0' style='background:#ffffff;padding:0;'>
        <tr>
          <td align='center'>

            <!-- MAIN CONTAINER -->
            <table role='presentation' width='600' border='0' cellspacing='0' cellpadding='0'
                  style='width:600px;max-width:600px;background:#ffffff;margin:0 auto;'>

              <!-- LOGO -->
              <tr>
                <td align='center' style='padding:0;'>
                  {logoTag}
                </td>
              </tr>

              <!-- TITLE -->
              <tr>
                <td align='center' style='padding:0 24px 10px 24px;'>
                  <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;
                              font-size:18px;
                              line-height:1.3;
                              font-weight:400;
                              color:#39c0c3;
                              text-align:center;'>
                    Σας ευχαριστούμε για το ενδιαφέρον σας!
                  </div>
                </td>
              </tr>

              <!-- TEXT -->
              <tr>
                <td align='left' style='padding:15px 30px;'>
                  <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;
                              font-size:14px;
                              line-height:1.6;
                              color:#000000;'>
                    Αγαπητέ/ή <b>{firstName} {lastName}</b>,<br/><br/>
                    Λάβαμε το αίτημά σας για προσφορά. Θα επικοινωνήσουμε μαζί σας το συντομότερο δυνατόν.<br/>
                    <span style='font-size:12px;color:#555555;'>
                      *Οι προσφορές ισχύουν για δέκα (10) ημερολογιακές ημέρες από την ημερομηνία παραλαβής.
                    </span>
                  </div>
                </td>
              </tr>

              <!-- CARS -->
              <tr>
                <td align='center' style='padding:20px 20px 10px 20px;'>
                  {carCardsHtml}
                </td>
              </tr>

              <!-- FOOTER -->
              <tr>
                <td align='center' style='padding:20px;
                                          font-family:Segoe UI,Roboto,Arial,sans-serif;
                                          font-size:13px;
                                          line-height:1.6;
                                          color:#000000;'>

                  <div style='font-weight:700;
                              font-size:16px;
                              color:#023859;
                              margin-bottom:10px;'>
                    Παραμένουμε στη διάθεσή σας!
                  </div>

                  <div style='font-size:16px;color:#023859;margin-bottom:6px;'>
                    <b>Kinsen</b>
                  </div>

                  <div style='margin-bottom:6px;'>{companyAddress}</div>
                  <div style='margin-bottom:6px;'>📞 {companyPhone}</div>
                  <div style='margin-bottom:6px;'>
                    ✉️ <a href='mailto:{companyEmail}'
                          style='color:#000000;text-decoration:none;'>
                          {companyEmail}
                        </a>
                  </div>

                  <div style='margin-top:10px;font-size:11px;'>
                    <a href='{termsUrl}'
                      style='color:#000000;text-decoration:underline;margin-right:8px;'>
                      Όροι & Προϋποθέσεις
                    </a>
                    <a href='{cookiesUrl}'
                      style='color:#000000;text-decoration:underline;'>
                      Πολιτική Cookies
                    </a>
                  </div>

                </td>
              </tr>

            <!-- END MAIN CONTAINER -->
            </table>

          </td>
        </tr>
      </table>

    </body>
    </html>";

    var from = "KINSEN <no-reply@kinsen.gr>";

    var msgCustomer = new EmailMessage(
        from,
        to: new[] { email },
        cc: null, bcc: null,
        replyTo: new[] { "sales@kinsen.gr" },
        customerSubject,
        customerBody,
        true,
        attachments: null
    );
    await _emailSender.SendAsync(msgCustomer, "OfferCustomerConfirmation");


  // ================================== CRM INTERACTION =====================================
  if (request.Cars == null || !request.Cars.Any())
      throw new InvalidOperationException("No cars provided for CRM interaction");

  // var carTitles = request.Cars
  //     .Select(c => $"{c.Maker} {c.Model}")
  //     .ToList();

  // var titleText =
  //     carTitles.Count == 1
  //         ? $"Αίτημα προσφοράς – {carTitles[0]}"
  //         : $"Αίτημα προσφοράς – {carTitles.Count} οχήματα";


  var carsDetailsText = string.Join("\n\n",
    request.Cars.Select((c, i) => $@"
      <b>Όχημα #{i + 1}: </b> {c.Maker} {c.Model} ({(c.Year?.ToString() ?? "-")}) <br>
      {FormatPriceGr(c.PriceText ?? "-")} € <br>
    "));

    var crmPayload = new
    {
      FlowId = 2401,
      Title = $"Αίτημα προσφοράς – {customerFullName}",
      Id = 0,
      StatusId = 0,
      Account = new
      {
        Email = email,
        AFM = afm,
        PhoneNumber = phone,
        Name = firstName,      
        Surname = lastName,    
        CompanyName = companyName,
        CustomerType = "Member"
      },
      Comments = $@"
      <b>Αίτημα προσφοράς από Μέλος</b><br><br>
      <b>Ονοματεπώνυμο:</b> {customerFullName}<br>
      <b>Email:</b> {email}<br>
      <b>Τηλέφωνο:</b> {phone}<br>
      <b>Εταιρεία:</b> {companyName} <br>
      <hr>
      {carsDetailsText}
      "
    };

    using var http = new HttpClient
    {
        BaseAddress = new Uri("https://kineticsuite.saracakis.gr/")
    };

    var crmResponse = await http.PostAsJsonAsync(
        "api/InteractionAPI/CreateInteraction",
        crmPayload
    );

    if (!crmResponse.IsSuccessStatusCode)
    {
      var err = await crmResponse.Content.ReadAsStringAsync();
      Console.WriteLine("CRM ERROR: " + err);
    }

      return Ok(new { ok = true });
    }
}