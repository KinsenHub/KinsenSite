using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;
using Umbraco.Cms.Core.Models;   // για MediaWithCrops
using Umbraco.Extensions;                           // Url(...)
using Umbraco.Cms.Core;                             // UrlMode
using Umbraco.Cms.Core.Models.PublishedContent;  

[Route("umbraco/api/[controller]")]
public class CarApiVisitorController : UmbracoApiController
{
    private readonly IUmbracoContextFactory _contextFactory;
    private readonly IEmailSender _emailSender;
    private readonly IWebHostEnvironment _env;

    public CarApiVisitorController(IUmbracoContextFactory contextFactory, IEmailSender emailSender, IWebHostEnvironment env)
    {
        _contextFactory = contextFactory;
        _emailSender = emailSender;
        _env = env;
    }

    // ====== Υπάρχον action: ΜΟΝΟ data (όπως το έχεις) ======
    [HttpPost("getcarbyid")]
    public IActionResult GetCarById([FromBody] CarRequest request)
    {
        if (request == null || request.Id <= 0)
            return BadRequest("Invalid request.");

        using var contextRef = _contextFactory.EnsureUmbracoContext();
        var umb = contextRef.UmbracoContext;

        var salesPage = umb.Content.GetAtRoot()
            .SelectMany(x => x.DescendantsOrSelf())
            .FirstOrDefault(x => x.ContentType.Alias == "usedCarSalesPage");

        if (salesPage == null) return NotFound("Sales page not found.");

        var carBlocks = salesPage.Value<IEnumerable<BlockListItem>>("carCardBlock");
        var car = carBlocks?.Select(x => x.Content).FirstOrDefault(x => x.Value<int>("carID") == request.Id);
        if (car == null) return NotFound($"Car with ID {request.Id} not found.");

        var result = new
        {
            id = request.Id,
            maker = car.Value<string>("maker"),
            model = car.Value<string>("model"),
            price = car.Value<string>("price"),
            year = car.Value<string>("yearRelease"),
            km = car.Value<string>("km"),
            cc = car.Value<string>("cc"),
            hp = car.Value<string>("hp"),
            fuel = car.Value<string>("fuel"),
            color = car.Value<string>("color"),
            transmission = car.Value<string>("transmissionType"),
            offer = car.Value<string>("typeOfDiscount"),
            typeOfCar = car.Value<string>("typeOfCar"),
            imageUrl = car.Value<IPublishedContent>("carPic")?.Url()
        };

        return Ok(result);
    }

    // ====== ΝΕΟ action: SubmitOffer (email με BRAVO SMTP) ======
    public class OfferRequest
    {
        public int CarId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? PaymentPlan { get; set; } // "efapaks" ή "6/12/..."
        public string? InterestCode { get; set; }
    }

    [HttpPost("submitofferVisitor")] // POST /umbraco/api/carapi/submitoffer
    public async Task<IActionResult> SubmitofferVisitor([FromBody] OfferRequest request)
    {
        if (request == null || request.CarId <= 0)
            return BadRequest("Invalid CarId.");
        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest("Missing required fields.");

        using var contextRef = _contextFactory.EnsureUmbracoContext();
        var umb = contextRef.UmbracoContext;

        var salesPage = umb.Content.GetAtRoot()
            .SelectMany(x => x.DescendantsOrSelf())
            .FirstOrDefault(x => x.ContentType.Alias == "usedCarSalesPage");
        if (salesPage == null) return NotFound("Sales page not found.");

        var carBlocks = salesPage.Value<IEnumerable<BlockListItem>>("carCardBlock");
        var car = carBlocks?.Select(x => x.Content).FirstOrDefault(x => x.Value<int>("carID") == request.CarId);
        if (car == null) return NotFound($"Car with ID {request.CarId} not found.");

        var maker = car.Value<string>("maker");
        var model = car.Value<string>("model");
        var price = car.Value<string>("price");
        var year = car.Value<string>("yearRelease");
        var km = car.Value<string>("km");
        var fuel = car.Value<string>("fuel");
        var color = car.Value<string>("color");
        var cc = car.Value<string>("cc");
        var hp = car.Value<string>("hp");
        var imageUrl = car.Value<IPublishedContent>("carPic")?.Url(); // π.χ. /media/xxx/car.jpg


        // ---------- Base64 embed (inline) ----------
        string imgTag = string.Empty;

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            // ABSOLUTE URL (όπως στο logo)
            var carUrl = Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute)
                ? imageUrl
                : $"{Request.Scheme}://{Request.Host}{imageUrl}";

            // Local ή Production;
            bool isLocal = string.Equals(Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase);

            if (isLocal)
            {
                // DEV/LOCAL: Base64 inline (όπως στο logo)
                string relative = carUrl;
                if (Uri.TryCreate(carUrl, UriKind.Absolute, out var abs))
                    relative = abs.LocalPath;

                string localPath = Path.Combine(
                    _env.WebRootPath,
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
                        var handler = new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator // μόνο για dev
                        };
                        using var http = new HttpClient(handler);
                        bytes = await http.GetByteArrayAsync(carUrl);
                    }
                }
                catch { /* optional: log */ }

                if (bytes != null && bytes.Length > 0)
                {
                    // mime-type από επέκταση (αν δεν βρούμε, πάμε jpeg)
                    string ext = Path.GetExtension(relative ?? string.Empty).ToLowerInvariant();
                    string mime = ext switch
                    {
                        ".png" => "image/png",
                        ".gif" => "image/gif",
                        ".webp" => "image/webp",
                        ".jpg" or ".jpeg" => "image/jpeg",
                        _ => "image/jpeg"
                    };

                    var b64 = Convert.ToBase64String(bytes);
                    imgTag =
                        $"<img src='data:{mime};base64,{b64}' alt='{maker} {model}' width='560' " +
                        "style='display:block;width:100%;max-width:560px;height:auto;border:0;outline:none;text-decoration:none;' />";
                }
                else
                {
                    // Fallback: absolute URL
                    imgTag =
                        $"<img src='{carUrl}' alt='{maker} {model}' width='560' " +
                        "style='display:block;width:100%;max-width:560px;height:auto;border:0;outline:none;text-decoration:none;' />";
                }
            }
            else
            {
                // PRODUCTION: remote image (όπως στο logo)
                imgTag =
                    $"<img src='{carUrl}' alt='{maker} {model}' width='560' " +
                    "style='display:block;width:100%;max-width:560px;height:auto;border:0;outline:none;text-decoration:none;' />";
            }
        }

        //*********Αφορά την εικόνα του αυτοκινήτου που στέλνεται στη KINSEN*********//
        var carImageUrlAbs = (!string.IsNullOrWhiteSpace(imageUrl) && Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
            ? imageUrl
            : (!string.IsNullOrWhiteSpace(imageUrl) ? $"{Request.Scheme}://{Request.Host}{imageUrl}" : "");

        // Τι εικόνα θα βάλουμε τελικά στο email που πάει στη Sales
        var salesImageHtml = !string.IsNullOrEmpty(imgTag)
            ? imgTag // προτιμάμε το ήδη παραγμένο Base64 (inline)
            : (string.IsNullOrWhiteSpace(carImageUrlAbs) 
                ? "" 
                : $"<img src='{carImageUrlAbs}' alt='{maker} {model}' width='480' style='display:block;width:100%;max-width:480px;height:auto;border:0;outline:none;text-decoration:none;'/>");

        // ✅ Απόδοση “πλάνου πληρωμής”
        var planText = (request.PaymentPlan == "efapaks") ? "Εφάπαξ" : $"{request.PaymentPlan} Μήνες";
        var interestText = (request.InterestCode == "toko") ? "Με τόκο" : "Χωρίς τόκους";
        var planDisplay = $"{planText} · {interestText}";

        // ================== EMAIL προς Sales (μπορείς να κρατήσεις attachment όπως πριν ή και τίποτα) ==================
        var subject = $"Αίτημα Προσφοράς: {request.FirstName} {request.LastName} για {maker} {model}";
        var body = $@"
            <h2>Νέο αίτημα προσφοράς:</h2>
            <p><strong>Πελάτης:</strong> {request.FirstName} {request.LastName}</p>
            <p><strong>Email:</strong> {request.Email}</p>
            <p><strong>Κινητό:</strong> {request.Phone}</p>
            <p><strong>Πλάνο Πληρωμής:</strong> {planDisplay}</p>
            <hr/>
            <h4>Αυτοκίνητο</h4>
            <ul>
                <li><strong>ID:</strong> {request.CarId}</li>
                <li><strong>Μάρκα/Μοντέλο:</strong> {maker} {model}</li>
                <li><strong>Τιμή:</strong> {price} €</li>
                <li><strong>Έτος:</strong> {year}</li>
                <li><strong>Χιλιόμετρα:</strong> {km} χλμ.</li>
                <li><strong>Καύσιμο:</strong> {fuel}</li>
                <li><strong>Χρώμα:</strong> {color}</li>
            </ul>
            <div style='margin-top:12px'>{salesImageHtml}</div>
        ";

        var msg = new EmailMessage(
            null,
            new[] { "Eirini.Skliri@kinsen.gr" },
            null, null,
            new[] { request.Email },
            subject,
            body,
            true,
            null // attachments προαιρετικά
        );
        await _emailSender.SendAsync(msg, "Offer");

        //********************* Kinsen Logo (robust) **************************
        using var cref = _contextFactory.EnsureUmbracoContext();
        var umbr = cref.UmbracoContext;

        IPublishedContent? settingsNode = umbr.Content.GetAtRoot()
            .SelectMany(x => x.DescendantsOrSelf())
            // Βρες τον ΠΡΩΤΟ κόμβο που έχει συμπληρωμένο το kinsenLogo (single media picker)
            .FirstOrDefault(x => x.HasProperty("kinsenLogo") && x.Value<IPublishedContent>("kinsenLogo") != null);

        string? logoUrl = null;
        if (settingsNode != null)
        {
            var logoMedia = settingsNode.Value<IPublishedContent>("kinsenLogo");
            logoUrl = logoMedia?.Url(mode: UrlMode.Absolute); // absolute URL για email
        }

        // Αν είσαι σε localhost, κάνε Base64 inline (ώστε να φαίνεται 100%)
        string logoTag = string.Empty;
        if (!string.IsNullOrWhiteSpace(logoUrl))
        {
            bool isLocal = string.Equals(Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase);

            if (isLocal)
            {
                // Δοκίμασε να το διαβάσεις από δίσκο, αλλιώς κατέβασέ το με HTTP και κάνε Base64
                string relative = logoUrl;
                if (Uri.TryCreate(logoUrl, UriKind.Absolute, out var abs)) relative = abs.LocalPath;

                string localPath = Path.Combine(_env.WebRootPath, relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                byte[]? bytes = null;

                try
                {
                    if (System.IO.File.Exists(localPath))
                    {
                        bytes = await System.IO.File.ReadAllBytesAsync(localPath);
                    }
                    else
                    {
                        var handler = new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator // μόνο για dev
                        };
                        using var http = new HttpClient(handler);
                        bytes = await http.GetByteArrayAsync(logoUrl);
                    }
                }
                catch { /* log αν θέλεις */ }

                if (bytes != null && bytes.Length > 0)
                {
                    string b64 = Convert.ToBase64String(bytes);
                    logoTag = $"<img src='data:image/png;base64,{b64}' alt='Kinsen' width='220' style='display:block;width:200px;height:auto;border:0;outline:none;text-decoration:none;' />";
                }
            }
            else
            {
                // Production: ελαφρύ remote image
                logoTag = $"<img src='{logoUrl}' alt='Kinsen' width='220' style='display:block;width:200px;height:auto;border:0;outline:none;text-decoration:none;margin-bottom:12px;' />";
            }
        }


        // ================== EMAIL προς Πελάτη (INLINE Base64) ==================
        var companyEmail = "sales@kinsen.gr";
        var companyPhone = "+30 211 190 3000";
        var companyAddress = "Λεωφόρος Αθηνών 71, Αθήνα";
        var cookiesUrl = $"{Request.Scheme}://{Request.Host}/cookies";
        var termsUrl = $"{Request.Scheme}://{Request.Host}/terms";

        var hpHtml = string.IsNullOrWhiteSpace(hp)
            ? ""
            : $"<p style='margin:0 0 8px 0;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:13px;line-height:1.7;color:#023859;'><strong>Ιπποδύναμη:</strong> {hp} hp</p>";

        var customerSubject = "Λάβαμε το αίτημά σας – Kinsen";
        var customerBody = $@"
            <!doctype html>
            <html xmlns='http://www.w3.org/1999/xhtml'>
            <head>
                <meta http-equiv='Content-Type' content='text/html; charset=UTF-8' />
                <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
                <title>Kinsen</title>
            </head>
            <body style='margin:0;padding:0;background:#007c91;'>
                <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0' style='background:#007c91;padding:8px 0 20px 0;'>
                <tr><td align='center'>

                    <table role='presentation' width='600' border='0' cellspacing='0' cellpadding='0' style='width:600px;'>

                        <tr>
                            <td align='center' style='padding:8px 24px 6px 24px;'>
                                <!-- Logo σε capsule με #007c91 -->
                                <table role='presentation' border='0' cellspacing='0' cellpadding='0' style='margin:0 auto;'>
                                <tr>
                                    <td align='center'>
                                    {logoTag}
                                    </td>
                                </tr>
                                </table>
                            </td>
                        </tr>

                        <tr>
                        <td align='center' style='padding:0 24px 2px 24px;'>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:800;font-size:18px;line-height:1.2;color:#ffffff;margin:10px;'>Σας ευχαριστούμε για το ενδιαφέρον σας!</div>
                        </td>
                        </tr>

                        <tr>
                            <td align='left' style='padding:0 24px 5px 24px;'>
                                <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:14px;line-height:1.7;color:#ffffff;font-weight:300;'>
                                <div style='margin-bottom:5px;'><b>Αγαπητέ/ή {request.FirstName} {request.LastName}</b></div>
                                Λάβαμε το αίτημά σας για προσφορά. Ετοιμάσαμε αναλυτικά τα στοιχεία του οχήματος που επιλέξατε. Η προσφορά ισχύει για δέκα (10) ημερολογιακές ημέρες από την ημερομηνία παραλαβής της.
                                </div>
                            </td>
                        </tr>

                        <tr>
                        <tr>
                        <td style='padding:5px 24px 1px 24px;'>
                            <!-- Κεφαλίδα (πάνω μέρος κουτιού) με border #023859 -->
                            <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0'
                                style='background:#ffffff;border:10px solid #023859;border-bottom:none;border-radius:6px 6px 0 0;'>
                            <tr>
                                <td align='center' style='padding:14px 12px;font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:600;font-size:16px;line-height:1.3;color:#023859;'>
                                <span style='color:#007c91;text-decoration:none;'>{maker} {model}</span>
                                </td>
                            </tr>
                            </table>
                        </td>
                        </tr>

                        <tr>
                        <td style='padding:0 24px;'>
                            <!-- Σώμα (κάτω μέρος κουτιού) με border #023859 -->
                            <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0'
                                style='background:#ffffff;border:10px solid #023859;border-top:none;border-radius:0 0 6px 6px;'>
                            
                            <tr>
                                <td align='center' style='padding:24px;'>{imgTag}</td>
                            </tr>

                            <tr>
                                <td style='border-top:1px solid #023859;border-bottom:1px solid #023859;'>
                                <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0'>
                                    <tr>
                                    <td width='33.33%' align='center'
                                        style='background:#ffffff;border-right:1px solid #023859;padding:14px 8px;font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:700;font-size:14px;line-height:1.2;color:#023859;'>
                                        {price} €
                                    </td>
                                    <td width='33.33%' align='center'
                                        style='background:#ffffff;border-right:1px solid #023859;padding:14px 8px;font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:700;font-size:14px;line-height:1.2;color:#023859;'>
                                        {km} km
                                    </td>
                                    <td width='33.33%' align='center'
                                        style='background:#ffffff;padding:14px 8px;font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:700;font-size:14px;line-height:1.2;color:#023859;'>
                                        {cc} cc
                                    </td>
                                    </tr>
                                </table>
                                </td>
                            </tr>

                            <tr>
                                <td style='padding:16px 20px 18px 20px;'>
                                <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0'>
                                    <tr>
                                    <td width='50%' valign='top' style='padding-right:18px;'>
                                        <p style='margin:0 0 8px 0;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:13px;line-height:1.7;color:#023859;'><strong>Μοντέλο:</strong> {maker} {model}</p>
                                        <p style='margin:0 0 8px 0;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:13px;line-height:1.7;color:#023859;'><strong>Έτος:</strong> {year}</p>
                                        <p style='margin:0 0 8px 0;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:13px;line-height:1.7;color:#023859;'><strong>Καύσιμο:</strong> {fuel}</p>
                                        <p style='margin:0 0 8px 0;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:13px;line-height:1.7;color:#023859;'><strong>Χρώμα:</strong> {color}</p>
                                        {hpHtml}
                                    </td>
                                    <td width='50%' valign='top' style='padding-left:18px;'>
                                        <p style='margin:0 0 8px 0;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:13px;line-height:1.7;color:#023859;'><strong>Χιλιόμετρα:</strong> {km} km</p>
                                        <p style='margin:0 0 8px 0;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:13px;line-height:1.7;color:#023859;'><strong>Κυβικά:</strong> {cc} cc</p>
                                        <p style='margin:0 0 8px 0;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:13px;line-height:1.7;color:#023859;'><strong>Πλάνο Πληρωμής:</strong> {planDisplay}</p>
                                        <p style='margin:0 0 8px 0;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:13px;line-height:1.7;color:#023859;'><strong>Τιμή:</strong> {price} €</p>
                                    </td>
                                    </tr>
                                </table>
                                </td>
                            </tr>

                            </table>
                        </td>
                        </tr>
                        <tr>
                        <td style='padding:0 24px 12px 24px;'>
                            <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0' style='border:1px solid #007c91;border-top:none;'><tr><td style='font-size:0;line-height:0'>&nbsp;</td></tr></table>
                        </td>
                        </tr>

                        <tr>
                        <td align='center' style='padding:4px 24px 22px 24px;'>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:700;font-size:16px;line-height:1.7;color:#ffffff;margin:8px 0 10px 0;'>Παραμένουμε στη διάθεσή σας!</div>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:400;font-size:14px;line-height:1.9;color:#ffffff;margin:8px 0;'>✉️ <a href='mailto:{companyEmail}' style='color:#ffffff;text-decoration:none;'>{companyEmail}</a></div>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:400;font-size:14px;line-height:1.9;color:#ffffff;margin:8px 0;'>📞 <a href='tel:{companyPhone}' style='color:#ffffff;text-decoration:none;'>{companyPhone}</a></div>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:400;font-size:14px;line-height:1.9;color:#ffffff;margin:8px 0;'>{companyAddress}</div>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:400;font-size:12px;line-height:1.9;color:#ffffff;margin-top:10px;'>
                            <a href='{cookiesUrl}' style='color:#ffffff;text-decoration:underline;'>Πολιτική Cookies</a> |
                            <a href='{termsUrl}' style='color:#ffffff;text-decoration:underline;'>Όροι &amp; Προϋποθέσεις</a>
                            </div>
                        </td>
                        </tr>

                    </table>

                </td></tr>
                </table>
            </body>
            </html>";

        var customerMsg = new EmailMessage(
            null,
            new[] { request.Email },
            null, null,
            new[] { "Eirini.Skliri@kinsen.gr" },
            customerSubject,
            customerBody,
            true,
            null
        );

        try { await _emailSender.SendAsync(customerMsg, "OfferCustomerConfirmation"); }
        catch { /* optional: log */ }

        return Ok(new { ok = true });
    }
        
}

// Μοντέλο για το GetCarById
public class CarRequest
{
    public int Id { get; set; }
}
