using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
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

    private async Task<string> ToBase64ImgTag(string url, string alt, int width = 300)
    {
        try
        {
            using var http = new HttpClient();
            var bytes = await http.GetByteArrayAsync(url);
            var base64 = Convert.ToBase64String(bytes);

            return $@"
            <img 
            src='data:image/jpeg;base64,{base64}'
            alt='{alt}'
            width='{width}'
            style='display:block;height:auto;border:0;outline:none;text-decoration:none;' />";
        }
        catch
        {
            return $@"
            <img 
            src='{url}'
            alt='{alt}'
            width='{width}'
            style='display:block;height:auto;border:0;outline:none;text-decoration:none;' />";
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
                        using var imageHttp = new HttpClient(handler);
                        bytes = await imageHttp.GetByteArrayAsync(carUrl);
                    }
                }
                catch { }

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
                    imgTag = $@" <img src='data:{mime};base64,{b64}' alt='{maker} {model}' width='240' height='180' style='display:block;border:0;outline:none;text-decoration:none;object-fit:contain;' />";                
                }
                else
                {
                    imgTag = $@"
                <img 
                    src='{carUrl}'
                    alt='{maker} {model}'
                        width='240'
                        height='180'
                    style='display:block;border:0;outline:none;text-decoration:none;object-fit:contain;' />";
                }
            }
            else
            {
                imgTag = $@"
                <img
                    src='{carUrl}'
                    alt='{maker} {model}'
                    width='190'
                    height='160'
                    style='display:block;border:0;outline:none;text-decoration:none;object-fit:contain;' />";

            }
        }


        //****************LOGO Kinsen******************
        const string logoUrl = "https://production-job-board-public.s3.amazonaws.com/logos/43021810-0cfb-466e-b00c-46c05fd4b394";
        var logoTag = await ToBase64ImgTag(logoUrl, "Kinsen", 280);


        // ✅ Απόδοση “πλάνου πληρωμής”
        var planText = (request.PaymentPlan == "efapaks") ? "Εφάπαξ" : $"{request.PaymentPlan} Μήνες";
        var interestText = (request.InterestCode == "toko") ? "Με τόκο" : "Χωρίς τόκους";
        var planDisplay = $"{planText} · {interestText}";


        // ================== CRM INTERACTION ======================
        var crmPayload = new
        {
            FlowId = 2401, 
            AccountId = 0,
            CustomFields = Array.Empty<object>(),
            Title = $"Αίτημα προσφοράς – {maker} {model} {(year?.ToString() ?? "-")}",
            Id = 0,
            StatusId = 0,
            Account = new
            {
                Email = request.Email,
                AFM = "",
                PhoneNumber = request.Phone,
                Name = request.FirstName,
                Surname = request.LastName,
                CompanyName = "-",
                CustomerType = "Visitor"
            },
            Comments = $@"
            <b>Αίτημα προσφοράς από επισκέπτη</b> <br><br>
            <b>Ονοματεπώνυμο:</b> {request.FirstName} {request.LastName} <br>
            <b>Email:</b> {request.Email} <br>
            <b>Τηλέφωνο:</b> {request.Phone} <br> 
            <b>Αυτοκίνητο:</b> {maker} {model} ({year}) <br>
            {FormatPriceGr(price)} € 
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


        var companyEmail = "sales@kinsen.gr";
        var companyPhone = "+30 211 190 3000";
        var companyAddress = "Λεωφόρος Αθηνών 71, Αθήνα";
        var cookiesUrl = $"{Request.Scheme}://{Request.Host}/cookies";
        var termsUrl = $"{Request.Scheme}://{Request.Host}/terms";
        
        var customerSubject = "📩 Επιβεβαίωση αιτήματος";
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
                <table role='presentation' width='600' border='0' cellspacing='0' cellpadding='0' style='width:600px;background:#ffffff;margin:0 auto;'>
                    <tr>
                        <td align='center' style='padding:8px 24px 6px 24px;'>
                            <table role='presentation' border='0' cellspacing='0' cellpadding='0' style='margin:0 0 20px 0;'>
                            <tr>
                                <td align='center' style='padding:14px 10px;'>
                                {logoTag}
                                </td>
                            </tr>
                            </table>
                        </td>
                    </tr>
                    <tr>
                    <td align='center' style='padding:0 24px 2px 24px;'> <div style='font-size:18px;line-height:1.2;font-weight:400;color:#39c0c3;;margin:10px;text-align:center;'>Σας ευχαριστούμε για το ενδιαφέρον σας!</div> </td>
                    </tr>

                    <tr>
                        <td align='left' style='padding:0 24px 5px 24px;'>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:14px;line-height:1.7;color:#000000;font-weight:400;'>
                                <div style='margin-bottom:5px;'>Αγαπητέ/ή <b> {request.FirstName} {request.LastName}</b></div>
                                Λάβαμε το αίτημά σας για προσφορά. Θα επικοινωνήσουμε μαζί σας το συντομότερο δυνατόν. 
                                *Οι προσφορές ισχύουν για δέκα (10) ημερολογιακές ημέρες από την ημερομηνία παραλαβής.
                            </div>
                        </td>
                    </tr>

                    <tr><td align='left' style='padding:20px 24px;'>
                        <table role='presentation' border='0' cellspacing='0' cellpadding='0' style='width:100%;max-width:600px;border:1px solid #ccc;border-radius:10px;background:#ffffff;'>
                            <tr>
                                <!-- ΑΡΙΣΤΕΡΑ: ΦΩΤΟ -->
                                <td width='160' valign='top' style='padding:12px;'>
                                    {imgTag}
                                </td>

                                <!-- ΔΕΞΙΑ: ΣΤΟΙΧΕΙΑ -->
                                <td valign='top' text-align:'center' style='padding:12px;font-family:Segoe UI,Roboto,Arial,sans-serif;color:#000000;'>

                                    <div style='font-size:18px;font-weight:700;margin-bottom:10px;color:#023859;'>
                                        {maker} {model}
                                    </div>

                                    <div style='font-size:14px;line-height:1.7;color:#333;margin-top:5px;'>
                                    <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0'>
                                        <tr>
                                        <!-- ΑΡΙΣΤΕΡΗ ΣΤΗΛΗ -->
                                        <td valign='top' style='padding-right:10px;'>
                                            • {(string.IsNullOrWhiteSpace(year) ? "-" : year)}<br>
                                            • {(string.IsNullOrWhiteSpace(cc) ? "-" : cc + " cc")}<br>
                                            • {(string.IsNullOrWhiteSpace(hp) ? "-" : hp + " hp")}<br>
                                        </td>

                                        <!-- ΔΕΞΙΑ ΣΤΗΛΗ -->
                                        <td valign='top' style='padding-left:10px;'>
                                            • {(string.IsNullOrWhiteSpace(km) ? "-" : km + " km")}<br>
                                            • {(string.IsNullOrWhiteSpace(fuel) ? '-' : fuel)}<br>
                                            • {(string.IsNullOrWhiteSpace(color) ? '-' : color)}
                                        </td>
                                        </tr>
                                    </table>
                                    </div>

                                    <div style='font-size:16px;font-weight:600;color:#007c91;margin-top:15px;'>
                                        {FormatPriceGr(price)} €
                                    </div>
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

        var from = "KINSEN <no-reply@kinsen.gr>";

        var customerMsg = new EmailMessage(
            from,
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
