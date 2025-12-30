using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

[Route("umbraco/api/[controller]")]
public class ModalOfferMemberApiController : UmbracoApiController
{
    private readonly IEmailSender _emailSender;

    public ModalOfferMemberApiController(IEmailSender emailSender)
    {
        _emailSender = emailSender;
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

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] OfferRequest request)
    {
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

              <div style='font-size:18px;font-weight:700;margin-bottom:6px;'>
                {c.Maker} {c.Model}
              </div>

              <table role='presentation' border='0' cellspacing='0' cellpadding='0' style='width:100%;'>
                <tr>

                  <!-- Αριστερή στήλη -->
                  <td valign='top'
                      style='width:50%;font-size:13px;color:#333;line-height:1.5;padding-right:10px;'>       
                    • {(c.Year.HasValue ? c.Year.Value.ToString() : "-")} <br>
                    • {(c.Cc.HasValue ? c.Cc + " cc" : "-")} <br>
                    • {(c.Hp.HasValue ? c.Hp + " hp" : "-")}
                  </td>

                  <!-- Δεξιά στήλη -->
                  <td valign='top'
                      style='width:50%;font-size:13px;color:#333;line-height:1.5;padding-left:10px;'>         
                    • {(c.Km.HasValue ? c.Km + " km" : "-")} <br>
                    • {(string.IsNullOrWhiteSpace(c.Fuel) ? "-" : c.Fuel)} <br>
                    • {(string.IsNullOrWhiteSpace(c.Color) ? "-" : c.Color)}
                  </td>

                </tr>
              </table>

              <div style='font-size:16px;font-weight:600;color:#007c91;margin-top:15px;'>
                {c.PriceText} €
              </div>

            </td>
          </tr>
        </table>
        ")));

        // === Email προς Πελάτη ===
        var companyEmail = "sales@kinsen.gr";
        var companyPhone = "+30 211 190 3000";
        var companyAddress = "Λεωφόρος Αθηνών 71, Αθήνα";
        var cookiesUrl = $"{Request.Scheme}://{Request.Host}/cookies";
        var termsUrl = $"{Request.Scheme}://{Request.Host}/terms";

        var subjectCustomer = "Λάβαμε το αίτημά σας – Kinsen";
        var bodyCustomer = $@"
        <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0' 
              style='padding:20px 0;'>
          <tr><td align='center'>
            <table role='presentation' width='600' border='0' cellspacing='0' cellpadding='0'
                  style='width:600px;'>
              <tr><td align='center' style='padding:10px;'>{logoTag}</td></tr>
              <tr><td align='center' style='padding:5px;'>
                <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;
                            font-size:20px;font-weight:400;color:#39c0c3;'>
                  Σας ευχαριστούμε για το ενδιαφέρον σας!
                </div>
              </td></tr>
              <tr><td align='left' style='padding:15px 30px;'>
                <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;
                            font-size:14px;line-height:1.6;color:#000000;'>
                  Αγαπητέ/ή <b> {request.FirstName} {request.LastName} </b>,<br/>
                  Λάβαμε το αίτημά σας για προσφορά. Ετοιμάσαμε αναλυτικά τα στοιχεία του οχήματος που επιλέξατε. 
                  Θα επικοινωνήσουμε μαζί σας το συντομότερο δυνατόν. <br>
                  *Η προσφορά ισχύει για δέκα (10) ημερολογιακές ημέρες από την ημερομηνία παραλαβής της.
                </div>
              </td></tr>
              <tr><td align='center' style='padding:20px;'>
                {carCardsHtml}
              </td></tr>
            </table>
          </td></tr>

          <tr>
            <td align='center' style='padding:20px;color:#000000;
                                      font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:13px;line-height:1.6;'>
              <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:700;font-size:16px;line-height:1.7;color:#000000;margin:8px 0 10px 0;'>
                  Παραμένουμε στη διάθεσή σας!
              </div>
              <div style='margin-bottom:6px; font-size:15px; color:#023859;'><b>Kinsen</b></div>
              <div style='margin-bottom:6px;'>{companyAddress}</div>
              <div style='margin-bottom:6px;'>📞 {companyPhone}</div>
              <div style='margin-bottom:6px;'>✉️ <a href='mailto:{companyEmail}' style='color:#000000;text-decoration:none;'>{companyEmail}</a></div>
              <div style='margin-top:10px;font-size:11px;'>
                <a href='{termsUrl}' style='color:#000000;text-decoration:underline;margin-right:8px;'>Όροι & Προϋποθέσεις</a>
                <a href='{cookiesUrl}' style='color:#000000;text-decoration:underline;'>Πολιτική Cookies</a>
              </div>
            </td>
          </tr>
        </table>";

            var msgCustomer = new EmailMessage(
                from: null,
                to: new[] { request.Email },
                cc: null, bcc: null,
                replyTo: new[] { "sales@kinsen.gr" },
                subjectCustomer,
                bodyCustomer,
                true,
                attachments: null
            );
            await _emailSender.SendAsync(msgCustomer, "OfferCustomerConfirmation");

            // ==**************= Email προς Kinsen =*********************==
            var subject = $"Αίτημα Προσφοράς: {request.FirstName} {request.LastName}";
            var body = $@"
              <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0'>
                <tr>
                  <td align='left'>
                    <table role='presentation' border='0' cellspacing='0' cellpadding='0'>
                      <tr>
                        <td align='left' style='padding:10px;'>
                          {logoTag}
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
              <h2 style='color:#39c0c3; font-weight:400;'>Νέο αίτημα προσφοράς</h2>
              <p><strong>Πελάτης:</strong> {request.FirstName} {request.LastName}</p>
              <p><strong>Email:</strong> {request.Email}</p>
              <p><strong>Τηλέφωνο:</strong> {request.Phone}</p>
              <hr/>
              <h4>Αυτοκίνητα στο καλάθι</h4>
              {carCardsHtml}
            ";

            var msgKinsen = new EmailMessage(
                null,
                new[] { "Eirini.Skliri@kinsen.gr" },
                null, null,
                new[] { request.Email },
                subject,
                body,
                true,
                null // attachments προαιρετικά
            );

            await _emailSender.SendAsync(msgKinsen, "OfferNotification");

            return Ok(new { ok = true });
        }
    }