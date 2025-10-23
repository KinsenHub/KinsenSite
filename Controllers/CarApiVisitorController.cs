using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;

[Route("umbraco/api/[controller]")]
public class CarApiVisitorController : UmbracoApiController
{
    private readonly IUmbracoContextFactory _contextFactory;
    private readonly IEmailSender _emailSender;
    private readonly IWebHostEnvironment _env;

    public CarApiVisitorController(
        IUmbracoContextFactory contextFactory,
        IEmailSender emailSender,
        IWebHostEnvironment env)
    {
        _contextFactory = contextFactory;
        _emailSender = emailSender;
        _env = env;
    }

    // ---------- Models ----------
    public class CarRequest
    {
        public int Id { get; set; }
    }

    // Ταιριάζει στο JSON του CarStock
    public class CarStockDto
    {
        [JsonPropertyName("id")]            public int    Id            { get; set; }
        [JsonPropertyName("maker")]         public string? Maker        { get; set; }
        [JsonPropertyName("model")]         public string? Model        { get; set; }
        [JsonPropertyName("price")]         public string? Price        { get; set; }
        [JsonPropertyName("yearRelease")]   public string? YearRelease  { get; set; }
        [JsonPropertyName("km")]            public string? Km           { get; set; }
        [JsonPropertyName("fuel")]          public string? Fuel         { get; set; }
        [JsonPropertyName("color")]         public string? Color        { get; set; }

        // Μπορεί να έρθει είτε number είτε string -> δέξου και τα δύο
        [JsonPropertyName("cc")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? Cc { get; set; }

        [JsonPropertyName("hp")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? Hp { get; set; }

        [JsonPropertyName("transmission")]  public string? Transmission { get; set; }
        [JsonPropertyName("typeOfCar")]     public string? TypeOfCar    { get; set; }
        [JsonPropertyName("consumption")]   public string? Consumption  { get; set; }
        [JsonPropertyName("carPicUrl")]     public string? CarPicUrl    { get; set; }
    }

    // ---------- Helper προς CarStock ----------
    private async Task<CarStockDto?> FetchFromCarStockAsync(int id)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var url = $"{baseUrl}/umbraco/api/carstock/getcarbyid"; // ίδιο endpoint που χτυπάς κι από JS

        var handler = new HttpClientHandler();
        if (string.Equals(Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            // dev: δέξου self-signed certs
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        using var http = new HttpClient(handler);

        // Το CarStock endpoint είναι POST με { id }
        var resp = await http.PostAsJsonAsync(url, new { id });
        if (!resp.IsSuccessStatusCode) return null;

        // Δεν χρειάζεται custom converter – μόνο number-handling στα properties
        return await resp.Content.ReadFromJsonAsync<CarStockDto>();
    }

    // ---------- API προς το front για τα στοιχεία του οχήματος ----------
    [HttpPost("getcarbyid")]
    public async Task<IActionResult> GetCarById([FromBody] CarRequest request)
    {
        if (request == null || request.Id <= 0)
            return BadRequest("Invalid request.");

        var car = await FetchFromCarStockAsync(request.Id);
        if (car == null) return NotFound($"Car with ID {request.Id} not found.");

        return Ok(new
        {
            id = car.Id,
            maker = car.Maker,
            model = car.Model,
            price = car.Price,
            year = car.YearRelease,
            km = car.Km,
            fuel = car.Fuel,
            color = car.Color,
            cc = car.Cc,                 // int?
            hp = car.Hp,                 // int?
            transmission = car.Transmission,
            typeOfCar = car.TypeOfCar,
            consumption = car.Consumption,
            imageUrl = car.CarPicUrl
        });
    }

    // ---------- Submit Offer (emails) ----------
    public class OfferRequest
    {
        public int CarId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? PaymentPlan { get; set; }   // "efapaks" ή "6/12/..."
        public string? InterestCode { get; set; }  // "toko" / "efapaks"
    }

    [HttpPost("submitofferVisitor")]
    public async Task<IActionResult> SubmitofferVisitor([FromBody] OfferRequest request)
    {
        if (request == null || request.CarId <= 0)
            return BadRequest("Invalid CarId.");
        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest("Missing required fields.");

        // Φέρε στοιχεία αυτοκινήτου από CarStock (όχι από Umbraco)
        var stock = await FetchFromCarStockAsync(request.CarId);
        if (stock == null) return NotFound($"Car with ID {request.CarId} not found.");

        var maker   = stock.Maker ?? "";
        var model   = stock.Model ?? "";
        var price   = stock.Price ?? "";
        var year    = stock.YearRelease ?? "";
        var km      = stock.Km ?? "";
        var fuel    = stock.Fuel ?? "";
        var color   = stock.Color ?? "";
        var cc      = stock.Cc;     // int?
        var hp      = stock.Hp;     // int?
        var imageUrl = stock.CarPicUrl;

        // ---- εικόνα (inline base64 σε local, remote σε prod) ----
        string imgTag = string.Empty;
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            var carUrl = Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute)
                ? imageUrl
                : $"{Request.Scheme}://{Request.Host}{imageUrl}";

            bool isLocal = string.Equals(Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase);

            if (isLocal)
            {
                string relative = carUrl;
                if (Uri.TryCreate(carUrl, UriKind.Absolute, out var abs)) relative = abs.LocalPath;

                string localPath = Path.Combine(_env.WebRootPath,
                    relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                byte[]? bytes = null;
                try
                {
                    if (System.IO.File.Exists(localPath))
                        bytes = await System.IO.File.ReadAllBytesAsync(localPath);
                    else
                    {
                        var h = new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback =
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        };
                        using var http2 = new HttpClient(h);
                        bytes = await http2.GetByteArrayAsync(carUrl);
                    }
                }
                catch { /* ignore */ }

                if (bytes is { Length: > 0 })
                {
                    var ext = Path.GetExtension(relative ?? string.Empty).ToLowerInvariant();
                    var mime = ext switch
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
                    imgTag =
                        $"<img src='{carUrl}' alt='{maker} {model}' width='560' " +
                        "style='display:block;width:100%;max-width:560px;height:auto;border:0;outline:none;text-decoration:none;' />";
                }
            }
            else
            {
                imgTag =
                    $"<img src='{carUrl}' alt='{maker} {model}' width='560' " +
                    "style='display:block;width:100%;max-width:560px;height:auto;border:0;outline:none;text-decoration:none;' />";
            }
        }

        var carImageUrlAbs = (!string.IsNullOrWhiteSpace(imageUrl) && Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
            ? imageUrl
            : (!string.IsNullOrWhiteSpace(imageUrl) ? $"{Request.Scheme}://{Request.Host}{imageUrl}" : "");

        var salesImageHtml = !string.IsNullOrEmpty(imgTag)
            ? imgTag
            : (string.IsNullOrWhiteSpace(carImageUrlAbs)
                ? ""
                : $"<img src='{carImageUrlAbs}' alt='{maker} {model}' width='480' style='display:block;width:100%;max-width:480px;height:auto;border:0;outline:none;text-decoration:none;'/>");

        var planText     = (request.PaymentPlan == "efapaks") ? "Εφάπαξ" : $"{request.PaymentPlan} Μήνες";
        var interestText = (request.InterestCode == "toko") ? "Με τόκο" : "Χωρίς τόκους";
        var planDisplay  = $"{planText} · {interestText}";

        // ---------- email προς Sales ----------
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

        // ---------- Logo για email προς πελάτη ----------
        using var cref = _contextFactory.EnsureUmbracoContext();
        var umbr = cref.UmbracoContext;

        IPublishedContent? settingsNode = umbr.Content.GetAtRoot()
            .SelectMany(x => x.DescendantsOrSelf())
            .FirstOrDefault(x => x.HasProperty("kinsenLogo") && x.Value<IPublishedContent>("kinsenLogo") != null);

        string logoTag = string.Empty;
        string? logoUrl = settingsNode?.Value<IPublishedContent>("kinsenLogo")?.Url(mode: UrlMode.Absolute);
        if (!string.IsNullOrWhiteSpace(logoUrl))
        {
            bool isLocal = string.Equals(Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase);
            if (isLocal)
            {
                string relative = logoUrl;
                if (Uri.TryCreate(logoUrl, UriKind.Absolute, out var abs)) relative = abs.LocalPath;

                string localPath = Path.Combine(_env.WebRootPath, relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                byte[]? bytes = null;
                try
                {
                    if (System.IO.File.Exists(localPath))
                        bytes = await System.IO.File.ReadAllBytesAsync(localPath);
                    else
                    {
                        var h = new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback =
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        };
                        using var http2 = new HttpClient(h);
                        bytes = await http2.GetByteArrayAsync(logoUrl);
                    }
                }
                catch { }

                if (bytes is { Length: > 0 })
                {
                    string b64 = Convert.ToBase64String(bytes);
                    logoTag = $"<img src='data:image/png;base64,{b64}' alt='Kinsen' width='220' style='display:block;width:200px;height:auto;border:0;outline:none;text-decoration:none;' />";
                }
            }
            else
            {
                logoTag = $"<img src='{logoUrl}' alt='Kinsen' width='220' style='display:block;width:220px;height:auto;border:0;outline:none;text-decoration:none;margin-bottom:12px;' />";
            }
        }

        // ---------- email προς πελάτη ----------
        var companyEmail   = "sales@kinsen.gr";
        var companyPhone   = "+30 211 190 3000";
        var companyAddress = "Λεωφόρος Αθηνών 71, Αθήνα";
        var cookiesUrl     = $"{Request.Scheme}://{Request.Host}/cookies";
        var termsUrl       = $"{Request.Scheme}://{Request.Host}/terms";

        var hpHtml = hp.HasValue
            ? $"<p style='margin:0 0 8px 0;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:13px;line-height:1.7;color:#023859;'><strong>Ιπποδύναμη:</strong> {hp} hp</p>"
            : "";

        var customerSubject = "Λάβαμε το αίτημά σας – Kinsen";
        var customerBody = $@"
        <!doctype html>
        <html xmlns='http://www.w3.org/1999/xhtml'>
        <head>
        <meta http-equiv='Content-Type' content='text/html; charset=UTF-8' />
        <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
        <title>Kinsen</title>
        </head>
        <body style='margin:0;padding:0;'>
        <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0' style='padding:0;margin:0;'>
            <tr>
            <td align='center'>

                <!-- Wrapper -->
                <table role='presentation' width='600' border='0' cellspacing='0' cellpadding='0' style='width:600px;'>

                <!-- Logo -->
                <tr>
                    <td align='center' style='padding:24px;'>
                    {logoTag}
                    </td>
                </tr>

                <!-- Title -->
                <tr>
                    <td align='center' style='padding:0 24px 10px 24px;'>
                    <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:400;
                                font-size:18px;line-height:1.4;color:#39c0c3;;margin:10px 0;'>
                        Σας ευχαριστούμε για το ενδιαφέρον σας!
                    </div>
                    </td>
                </tr>

                <!-- Greeting -->
                <tr>
                    <td align='left' style='padding:0 24px 20px 24px;'>
                    <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;
                                font-size:14px;line-height:1.6;color:#000;font-weight:300;'>
                        <div style='margin-bottom:8px;'>
                        <b>Αγαπητέ/ή {request.FirstName} {request.LastName}</b>
                        </div>
                        Λάβαμε το αίτημά σας για προσφορά. Ετοιμάσαμε αναλυτικά τα στοιχεία του οχήματος
                        που επιλέξατε. Η προσφορά ισχύει για δέκα (10) ημερολογιακές ημέρες από την
                        ημερομηνία παραλαβής της.
                    </div>
                    </td>
                </tr>

                <!-- Car Card -->
                <tr>
                    <td align='center' style='padding:0 24px 30px 24px;'>
                    <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0'
                            style='border-radius:8px;overflow:hidden;'>
                        <tr>
                        <td align='center' style='padding:20px;'>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:16px;font-weight:600;color:#023859;margin-bottom:6px;'>
                            {maker} {model}
                            </div>
                            {imgTag}
                            <div style='margin-top:10px;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:14px;line-height:1.5;color:#023859;'>
                            {year} · {km} km · {fuel}
                            </div>
                            <div style='margin-top:8px;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:16px;font-weight:700;color:#007c91;'>
                            {price} €
                            </div>
                        </td>
                        </tr>
                    </table>
                    </td>
                </tr>

                <!-- Footer -->
                <tr>
                    <td align='center' style='padding:20px;color:#000;
                                            font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:13px;line-height:1.6;'>
                    <div style='margin-bottom:6px;'>Kinsen - Όμιλος Σαρακάκη</div>
                    <div style='margin-bottom:6px;'>{companyAddress}</div>
                    <div style='margin-bottom:6px;'>📞 {companyPhone}</div>
                    <div style='margin-bottom:6px;'>✉️ <a href='mailto:{companyEmail}' style='color:#ffffff;text-decoration:none;'>{companyEmail}</a></div>
                    <div style='margin-top:10px;font-size:11px;'>
                        <a href='{termsUrl}' style='color:#000;text-decoration:underline;margin-right:8px;'>Όροι & Προϋποθέσεις</a>
                        <a href='{cookiesUrl}' style='color:#000;text-decoration:underline;'>Πολιτική Cookies</a>
                    </div>
                    </td>
                </tr>
            </td>
            </tr>
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
