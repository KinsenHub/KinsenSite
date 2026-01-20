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
using Umbraco.Cms.Core.Models; 
using System.Globalization;                        


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
        if (request == null || request.CarId <= 0)
            return BadRequest("Invalid request.");

        using var contextRef = _contextFactory.EnsureUmbracoContext();
        var umb = contextRef.UmbracoContext;

        var salesPage = umb.Content.GetAtRoot()
            .SelectMany(x => x.DescendantsOrSelf())
            .FirstOrDefault(x => x.ContentType.Alias == "usedCarSalesPage");

        if (salesPage == null) return NotFound("Sales page not found.");

        var carBlocks = salesPage.Value<IEnumerable<BlockListItem>>("carCardBlock");
        var car = carBlocks?.Select(x => x.Content)
            .FirstOrDefault(x => x.Value<int>("carID") == request.CarId);

        var gallery = new List<string>();

        var tenPhotosBlocks = car.Value<IEnumerable<BlockListItem>>("TenPhotosForUsedCarSales");

        if (tenPhotosBlocks != null)
        {
            foreach (var block in tenPhotosBlocks)
            {
                var content = block.Content;
                if (content == null) continue;

                for (int i = 1; i <= 10; i++)
                {
                    var img = content.Value<IPublishedContent>($"img{i}");
                    if (img != null)
                    {
                        gallery.Add(img.Url());
                    }
                }
            }
        }  
            
        if (car == null) return NotFound($"Car with ID {request.CarId} not found.");
        
        var media = car.Value<MediaWithCrops>("carPic");

        var result = new
        {
            id = request.CarId,
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
            imageUrl = media?.GetCropUrl() ?? media?.MediaUrl() ?? "",
            gallery = gallery
        };

        return Ok(result);
    }

    public class CarRequest
    {
        public int CarId { get; set; }
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

    private static string FormatPriceGr(string? raw)
    {
      if (string.IsNullOrWhiteSpace(raw)) return "-";

      // κράτα μόνο ψηφία (βγάζει €, τελείες, κόμματα κλπ)
      var digits = new string(raw.Where(char.IsDigit).ToArray());

      if (!long.TryParse(digits, out var value))
          return raw;

      return value.ToString("N0", new CultureInfo("el-GR")); // 30.500
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
        var imageUrl = car.Value<MediaWithCrops>("carPic")?.Url();

        // ---- LOGGING ----
        Console.WriteLine("=== VISITOR DEBUG START ===");

        // 1. Όλα τα properties του block
        foreach (var p in car.Properties)
        {
            Console.WriteLine($"Property: {p.Alias} => {p.GetValue()}");
        }

        // 2. Raw τιμή του carPic
        var rawPic = car.Value("carPic");
        Console.WriteLine("RAW carPic value = " + rawPic);

        // 3. Type του raw carPic
        Console.WriteLine("RAW carPic TYPE = " + rawPic?.GetType().FullName);

        // 4. MediaWithCrops attempt
        var mediaCrops = car.Value<MediaWithCrops>("carPic");
        Console.WriteLine("MediaWithCrops found? " + (mediaCrops != null ? "YES" : "NO"));
        Console.WriteLine("MediaWithCrops Url: " + (mediaCrops?.Url() ?? "null"));

        // 5. IPublishedContent attempt
        var mediaIPC = car.Value<IPublishedContent>("carPic");
        Console.WriteLine("IPublishedContent found? " + (mediaIPC != null ? "YES" : "NO"));
        Console.WriteLine("IPublishedContent Url: " + (mediaIPC?.Url() ?? "null"));

        // 6. Strong typed attempts on crop URLs
        Console.WriteLine("GetCropUrl(): " + (mediaCrops?.GetCropUrl() ?? "null"));
        Console.WriteLine("MediaUrl():   " + (mediaCrops?.MediaUrl() ?? "null"));

        Console.WriteLine("=== VISITOR DEBUG END ===");

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


        //****************LOGO Kinsen******************
        const string logoUrl = "https://production-job-board-public.s3.amazonaws.com/logos/43021810-0cfb-466e-b00c-46c05fd4b394";
        var logoTag = await ToBase64ImgTag(logoUrl, "Kinsen", 250);


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
                        <td valign='middle'
                            style='padding:0 12px 0 0;margin:0;'>
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
                            Νέο αίτημα προσφοράς:
                        </span>
                        </td>
                    </tr>
                    </table>
                </td>
                </tr>

                <!-- SPACER -->
                <tr><td height='15' style='line-height:15px;font-size:0;'>&nbsp;</td></tr>

                <!-- CUSTOMER INFO -->
                <tr>
                <td align='left'
                    style='font-family:Segoe UI,Roboto,Arial,sans-serif;
                            font-size:14px;
                            color:#000;
                            padding:0;margin:0;'>
                    <p style='margin:0 0 6px 0;'><strong>Πελάτης:</strong> {request.FirstName} {request.LastName}</p>
                    <p style='margin:0 0 6px 0;'><strong>Email:</strong> {request.Email}</p>
                    <p style='margin:0 0 6px 0;'><strong>Κινητό:</strong> {request.Phone}</p>
                    <p style='margin:0;'><strong>Πλάνο Πληρωμής:</strong> {planDisplay}</p>
                </td>
                </tr>

                <!-- DIVIDER -->
                <tr>
                <td style='padding:15px 0;'>
                    <hr style='border:none;border-top:1px solid #ddd;margin:0;'>
                </td>
                </tr>

                <!-- CAR INFO -->
                <tr><td align='center' style='padding:20px;'>
                    <table role='presentation' border='0' cellspacing='0' cellpadding='0' align='center'
                        style='margin:15px auto;width:100%;max-width:600px;border:1px solid #ccc;border-radius:10px;overflow:hidden;background:#ffffff;'>
                        <tr>
                            <!-- Εικόνα αριστερά -->
                            <td width='240' align='center' style='height:180px;'>
                                {imgTag}
                            </td>

                            <!-- Στοιχεία δεξιά -->
                            <td style='padding:12px;vertical-align:top;font-family:Segoe UI,Roboto,Arial,sans-serif;color:#000000;'>
                            <div style='font-size:18px;font-weight:700;margin-bottom:6px;color:#023859;'>{maker} {model}</div>

                            <table role='presentation' border='0' cellspacing='0' cellpadding='0' style='width:100%;'>
                                <tr>
                                <!-- Αριστερή στήλη (3) -->
                                <td valign='top' style='width:50%;font-size:13px;color:#333;line-height:1.5;padding-right:10px;'>
                                    • {(string.IsNullOrWhiteSpace(year) ? "-" : year)}<br>
                                    • {(string.IsNullOrWhiteSpace(cc) ? "-" : cc + " cc")}<br>
                                    • {(string.IsNullOrWhiteSpace(hp) ? "-" : hp + " hp")}
                                </td>

                                <!-- Δεξιά στήλη (3) -->
                                <td valign='top' style='width:50%;font-size:13px;color:#333;line-height:1.5;padding-left:10px;'>
                                    • {(string.IsNullOrWhiteSpace(km) ? "-" : km + " km")}<br>
                                    • {(string.IsNullOrWhiteSpace(fuel) ? "-" : fuel)}<br>
                                    • {(string.IsNullOrWhiteSpace(color) ? "-" : color)}
                                </td>
                                </tr>
                            </table>

                            <div style='font-size:16px;font-weight:600;color:#007c91;margin-top:15px;'>{FormatPriceGr(price)} €</div>
                            </td>
                        </tr>
                    </table>
                    </td></tr>
            </table>

            </td>
        </tr>
        </table>

        </body>
        </html>";

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

        var companyEmail = "sales@kinsen.gr";
        var companyPhone = "+30 211 190 3000";
        var companyAddress = "Λεωφόρος Αθηνών 71, Αθήνα";
        var cookiesUrl = $"{Request.Scheme}://{Request.Host}/cookies";
        var termsUrl = $"{Request.Scheme}://{Request.Host}/terms";
        
        var customerSubject = "Λάβαμε το αίτημά σας – Kinsen";
        var customerBody =  $@"
        <!doctype html>
        <html xmlns='http://www.w3.org/1999/xhtml'>
        <head>
            <meta http-equiv='Content-Type' content='text/html; charset=UTF-8' />
            <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
            <title>Kinsen</title>
        </head>
        <body style='margin:0;padding:0;background:#ffffff;color:#000000;'>
            <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0' style='padding:8px 0 20px 0;background:#ffffff;'>
            <tr><td align='center'>

                <table role='presentation' width='600' border='0' cellspacing='0' cellpadding='0' style='width:600px;background:#ffffff;'>

                    <tr>
                        <td align='center' style='padding:8px 24px 6px 24px;'>
                            <table role='presentation' border='0' cellspacing='0' cellpadding='0' style='margin:0 auto; margin-bottom:20px;'>
                                <tr>
                                    <td align='left' style='padding:10px;'>{logoTag}</td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <tr>
                    <td align='center' style='padding:0 24px 2px 24px;'> <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:18px;line-height:1.2;font-weight:400;color:#39c0c3;;margin:10px;'>Σας ευχαριστούμε για το ενδιαφέρον σας!</div> </td>
                    </tr>

                    <tr>
                        <td align='left' style='padding:0 24px 5px 24px;'>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:14px;line-height:1.7;color:#000000;font-weight:400;'>
                                <div style='margin-bottom:5px;'>Αγαπητέ/ή <b> {request.FirstName} {request.LastName}</b></div>
                                Λάβαμε το αίτημά σας για προσφορά. Ετοιμάσαμε αναλυτικά τα στοιχεία του οχήματος που επιλέξατε. 
                                Η προσφορά ισχύει για δέκα (10) ημερολογιακές ημέρες από την ημερομηνία παραλαβής της.
                            </div>
                        </td>
                    </tr>

                    <tr><td align='center' style='padding:20px;'>
                        <table role='presentation' border='0' cellspacing='0' cellpadding='0' align='center'
                            style='margin:15px auto;width:100%;max-width:600px;border:1px solid #ccc;border-radius:10px;overflow:hidden;background:#ffffff;'>
                        <tr>
                            <!-- Εικόνα αριστερά -->
                            <td width='240' align='center' style='height:180px;'>
                                {imgTag}
                            </td>

                            <!-- Στοιχεία δεξιά -->
                            <td style='padding:12px;vertical-align:top;font-family:Segoe UI,Roboto,Arial,sans-serif;color:#000000;'>
                            <div style='font-size:18px;font-weight:700;margin-bottom:6px;color:#023859;'>{maker} {model}</div>

                            <table role='presentation' border='0' cellspacing='0' cellpadding='0' style='width:100%;'>
                                <tr>
                                <!-- Αριστερή στήλη (3) -->
                                <td valign='top' style='width:50%;font-size:13px;color:#333;line-height:1.5;padding-right:10px;'>
                                    • {(string.IsNullOrWhiteSpace(year) ? "-" : year)}<br>
                                    • {(string.IsNullOrWhiteSpace(cc) ? "-" : cc + " cc")}<br>
                                    • {(string.IsNullOrWhiteSpace(hp) ? "-" : hp + " hp")}
                                </td>

                                <!-- Δεξιά στήλη (3) -->
                                <td valign='top' style='width:50%;font-size:13px;color:#333;line-height:1.5;padding-left:10px;'>
                                    • {(string.IsNullOrWhiteSpace(km) ? "-" : km + " km")}<br>
                                    • {(string.IsNullOrWhiteSpace(fuel) ? "-" : fuel)}<br>
                                    • {(string.IsNullOrWhiteSpace(color) ? "-" : color)}
                                </td>
                                </tr>
                            </table>

                            <div style='font-size:16px;font-weight:600;color:#007c91;margin-top:15px;'>{FormatPriceGr(price)} €</div>
                            </td>
                        </tr>
                        </table>
                    </td></tr>

                    <tr>
                        <td align='center' style='padding:10px 24px 20px 24px;'>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:700;font-size:16px;line-height:1.7;color:#023859;margin:8px 0 10px 0;'>
                                Παραμένουμε στη διάθεσή σας!
                            </div>
                            <div style='margin-bottom:6px; font-size:16px; color:#023859;'><b>Kinsen</b></div>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:400;font-size:14px;line-height:1.9;color:#000000;margin:8px 0;'>
                                ✉️ <a href='mailto:{companyEmail}' style='color:#000000;text-decoration:none;'>{companyEmail}</a>
                            </div>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:400;font-size:14px;line-height:1.9;color:#000000;margin:8px 0;'>
                                📞 <a href='tel:{companyPhone}' style='color:#000000;text-decoration:none;'>{companyPhone}</a>
                            </div>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:400;font-size:14px;line-height:1.9;color:#000000;margin:8px 0;'>
                                {companyAddress}
                            </div>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:400;font-size:12px;line-height:1.9;color:#000000;margin-top:10px;'>
                                <a href='{cookiesUrl}' style='color:#000000;text-decoration:underline;'>Πολιτική Cookies</a> |
                                <a href='{termsUrl}' style='color:#000000;text-decoration:underline;'>Όροι &amp; Προϋποθέσεις</a>
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
