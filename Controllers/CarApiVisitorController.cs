// using System.Net.Http;
// using System.Net.Http.Json;
// using System.Text.Json;
// using System.Text.Json.Serialization;
// using Microsoft.AspNetCore.Mvc;
// using Umbraco.Cms.Core.Mail;
// using Umbraco.Cms.Core.Models.Email;
// using Umbraco.Cms.Core.Models.PublishedContent;
// using Umbraco.Cms.Core.Web;
// using Umbraco.Cms.Web.Common.Controllers;
// using Microsoft.AspNetCore.Authorization;

// [Route("umbraco/api/[controller]")]
// public class CarApiVisitorController : UmbracoApiController
// {
//     private readonly IUmbracoContextFactory _contextFactory;
//     private readonly IEmailSender _emailSender;
//     private readonly IWebHostEnvironment _env;

//     public CarApiVisitorController(
//         IUmbracoContextFactory contextFactory,
//         IEmailSender emailSender,
//         IWebHostEnvironment env)
//     {
//         _contextFactory = contextFactory;
//         _emailSender = emailSender;
//         _env = env;
//     }

//     // ---------- Models ----------
//     public class CarRequest
//     {
//         public int Id { get; set; }
//     }

//     // Ταιριάζει στο JSON του CarStock
//     public class CarStockDto
//     {
//         [JsonPropertyName("id")]            public int    Id            { get; set; }
//         [JsonPropertyName("maker")]         public string? Maker        { get; set; }
//         [JsonPropertyName("model")]         public string? Model        { get; set; }
//         [JsonPropertyName("price")]         public string? Price        { get; set; }
//         [JsonPropertyName("yearRelease")]   public string? YearRelease  { get; set; }
//         [JsonPropertyName("km")]            public string? Km           { get; set; }
//         [JsonPropertyName("fuel")]          public string? Fuel         { get; set; }
//         [JsonPropertyName("color")]         public string? Color        { get; set; }

//         // Μπορεί να έρθει είτε number είτε string -> δέξου και τα δύο
//         [JsonPropertyName("cc")]
//         [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
//         public int? Cc { get; set; }

//         [JsonPropertyName("hp")]
//         [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
//         public int? Hp { get; set; }

//         [JsonPropertyName("transmissionType")]  public string? TransmissionType { get; set; }
//         [JsonPropertyName("typeOfCar")]     public string? TypeOfCar    { get; set; }
//         [JsonPropertyName("carPicUrl")]     public string? CarPicUrl    { get; set; }
//     }

//     // ---------- Helper προς CarStock ----------
//     private async Task<CarStockDto?> FetchFromCarStockAsync(int id)
//     {
//         var baseUrl = $"{Request.Scheme}://{Request.Host}";
//         var url = $"{baseUrl}/umbraco/api/carstock/getcarbyid"; // ίδιο endpoint που χτυπάς κι από JS

//         var handler = new HttpClientHandler();
//         if (string.Equals(Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase))
//         {
//             // dev: δέξου self-signed certs
//             handler.ServerCertificateCustomValidationCallback =
//                 HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
//         }

//         using var http = new HttpClient(handler);

//         // Το CarStock endpoint είναι POST με { id }
//         var resp = await http.PostAsJsonAsync(url, new { id });
//         if (!resp.IsSuccessStatusCode) return null;

//         // Δεν χρειάζεται custom converter – μόνο number-handling στα properties
//         return await resp.Content.ReadFromJsonAsync<CarStockDto>();
//     }

//     // ---------- API προς το front για τα στοιχεία του οχήματος ----------
//     [HttpPost("getcarbyid")]
//     [AllowAnonymous]
//     [IgnoreAntiforgeryToken]
//     [Consumes("application/json")]
//     public IActionResult GetCarById([FromBody] CarRequest request)
//     {
//         if (request == null || request.Id <= 0)
//             return BadRequest("Invalid request.");

//         using var cref = _contextFactory.EnsureUmbracoContext();
//         var umb = cref.UmbracoContext;

//         var salesPage = umb.Content.GetAtRoot()
//             .SelectMany(x => x.DescendantsOrSelf())
//             .FirstOrDefault(x => x.ContentType.Alias == "usedCarSalesPage");

//         if (salesPage == null)
//             return NotFound("usedCarSalesPage not found.");

//         var json = salesPage.Value<string>("carCardBlock"); // <-- alias property του Block List
//         if (string.IsNullOrWhiteSpace(json))
//             return NotFound("No cars.");

//         using var doc = JsonDocument.Parse(json);
//         if (!doc.RootElement.TryGetProperty("contentData", out var contentData) ||
//             contentData.ValueKind != JsonValueKind.Array)
//             return NotFound("contentData missing.");

//         static string S(JsonElement e, string name)
//             => e.TryGetProperty(name, out var v)
//                 ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString())
//                 : "";

//         static float F(JsonElement e, string name)
//             => (e.TryGetProperty(name, out var v) && v.TryGetSingle(out var f)) ? f : 0f;

//         foreach (var card in contentData.EnumerateArray())
//         {
//             // ----- Διάβασε carId (string ή number). Fallback σε carID για παλιά δεδομένα -----
//             int cid = 0;
//             if (card.TryGetProperty("carId", out var vId) || card.TryGetProperty("carID", out vId))
//             {
//                 if (vId.ValueKind == JsonValueKind.Number) vId.TryGetInt32(out cid);
//                 else if (vId.ValueKind == JsonValueKind.String) int.TryParse(vId.GetString(), out cid);
//             }
//             if (cid != request.Id) continue;

//             var result = new
//             {
//                 id = cid,
//                 maker = S(card, "maker"),
//                 model = S(card, "model"),
//                 price = S(card, "price"),
//                 year = S(card, "yearRelease"),
//                 km = S(card, "km"),
//                 fuel = S(card, "fuel"),
//                 color = S(card, "color"),
//                 cc = F(card, "cc"),
//                 hp = F(card, "hp"),
//                 transmission = S(card, "transmissionType"),
//                 typeOfCar = S(card, "typeOfCar"),
//                 imageUrl = S(card, "carPicUrl")
//             };
//             return Ok(result);
//         }

//         return NotFound($"Car with ID {request.Id} not found.");
//     }

//     // ---------- Submit Offer (emails) ----------
//     public class OfferRequest
//     {
//         public int CarId { get; set; }
//         public string FirstName { get; set; } = "";
//         public string LastName { get; set; } = "";
//         public string Email { get; set; } = "";
//         public string Phone { get; set; } = "";
//         public string? PaymentPlan { get; set; }   // "efapaks" ή "6/12/..."
//         public string? InterestCode { get; set; }  // "toko" / "efapaks"
//     }

//     [HttpPost("submitofferVisitor")]
//     public async Task<IActionResult> SubmitofferVisitor([FromBody] OfferRequest request)
//     {
//         if (request == null || request.CarId <= 0)
//             return BadRequest("Invalid CarId.");
//         if (string.IsNullOrWhiteSpace(request.FirstName) ||
//             string.IsNullOrWhiteSpace(request.LastName) ||
//             string.IsNullOrWhiteSpace(request.Email) ||
//             string.IsNullOrWhiteSpace(request.Phone))
//             return BadRequest("Missing required fields.");

//         // Φέρε στοιχεία αυτοκινήτου από CarStock (όχι από Umbraco)
//         var stock = await FetchFromCarStockAsync(request.CarId);
//         if (stock == null) return NotFound($"Car with ID {request.CarId} not found.");

//         var maker   = stock.Maker ?? "";
//         var model   = stock.Model ?? "";
//         var price   = stock.Price ?? "";
//         var year    = stock.YearRelease ?? "";
//         var km      = stock.Km ?? "";
//         var fuel    = stock.Fuel ?? "";
//         var color   = stock.Color ?? "";
//         var cc      = stock.Cc;     // int?
//         var hp      = stock.Hp;     // int?
//         var imageUrl = stock.CarPicUrl;

//         // ---- εικόνα (inline base64 σε local, remote σε prod) ----
//         string imgTag = string.Empty;
//         if (!string.IsNullOrWhiteSpace(imageUrl))
//         {
//             var carUrl = Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute)
//                 ? imageUrl
//                 : $"{Request.Scheme}://{Request.Host}{imageUrl}";

//             bool isLocal = string.Equals(Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase);

//             if (isLocal)
//             {
//                 string relative = carUrl;
//                 if (Uri.TryCreate(carUrl, UriKind.Absolute, out var abs)) relative = abs.LocalPath;

//                 string localPath = Path.Combine(_env.WebRootPath,
//                     relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

//                 byte[]? bytes = null;
//                 try
//                 {
//                     if (System.IO.File.Exists(localPath))
//                         bytes = await System.IO.File.ReadAllBytesAsync(localPath);
//                     else
//                     {
//                         var h = new HttpClientHandler
//                         {
//                             ServerCertificateCustomValidationCallback =
//                                 HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
//                         };
//                         using var http2 = new HttpClient(h);
//                         bytes = await http2.GetByteArrayAsync(carUrl);
//                     }
//                 }
//                 catch { /* ignore */ }

//                 if (bytes is { Length: > 0 })
//                 {
//                     var ext = Path.GetExtension(relative ?? string.Empty).ToLowerInvariant();
//                     var mime = ext switch
//                     {
//                         ".png" => "image/png",
//                         ".gif" => "image/gif",
//                         ".webp" => "image/webp",
//                         ".jpg" or ".jpeg" => "image/jpeg",
//                         _ => "image/jpeg"
//                     };
//                     var b64 = Convert.ToBase64String(bytes);
//                     imgTag =
//                         $"<img src='data:{mime};base64,{b64}' alt='{maker} {model}' width='560' " +
//                         "style='display:block;width:100%;max-width:560px;height:auto;border:0;outline:none;text-decoration:none;' />";
//                 }
//                 else
//                 {
//                     imgTag =
//                         $"<img src='{carUrl}' alt='{maker} {model}' width='560' " +
//                         "style='display:block;width:100%;max-width:560px;height:auto;border:0;outline:none;text-decoration:none;' />";
//                 }
//             }
//             else
//             {
//                 imgTag =
//                     $"<img src='{carUrl}' alt='{maker} {model}' width='560' " +
//                     "style='display:block;width:100%;max-width:560px;height:auto;border:0;outline:none;text-decoration:none;' />";
//             }
//         }

//         var carImageUrlAbs = (!string.IsNullOrWhiteSpace(imageUrl) && Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
//             ? imageUrl
//             : (!string.IsNullOrWhiteSpace(imageUrl) ? $"{Request.Scheme}://{Request.Host}{imageUrl}" : "");

//         var salesImageHtml = !string.IsNullOrEmpty(imgTag)
//             ? imgTag
//             : (string.IsNullOrWhiteSpace(carImageUrlAbs)
//                 ? ""
//                 : $"<img src='{carImageUrlAbs}' alt='{maker} {model}' width='480' style='display:block;width:100%;max-width:480px;height:auto;border:0;outline:none;text-decoration:none;'/>");

//         var planText     = (request.PaymentPlan == "efapaks") ? "Εφάπαξ" : $"{request.PaymentPlan} Μήνες";
//         var interestText = (request.InterestCode == "toko") ? "Με τόκο" : "Χωρίς τόκους";
//         var planDisplay  = $"{planText} · {interestText}";

//         // ---------- email προς Sales ----------
//         var subject = $"Αίτημα Προσφοράς: {request.FirstName} {request.LastName} για {maker} {model}";
//         var body = $@"
//             <h2>Νέο αίτημα προσφοράς:</h2>
//             <p><strong>Πελάτης:</strong> {request.FirstName} {request.LastName}</p>
//             <p><strong>Email:</strong> {request.Email}</p>
//             <p><strong>Κινητό:</strong> {request.Phone}</p>
//             <p><strong>Πλάνο Πληρωμής:</strong> {planDisplay}</p>
//             <hr/>
//             <h4>Αυτοκίνητο</h4>
//             <ul>
//                 <li><strong>ID:</strong> {request.CarId}</li>
//                 <li><strong>Μάρκα/Μοντέλο:</strong> {maker} {model}</li>
//                 <li><strong>Τιμή:</strong> {price} €</li>
//                 <li><strong>Έτος:</strong> {year}</li>
//                 <li><strong>Χιλιόμετρα:</strong> {km} χλμ.</li>
//                 <li><strong>Καύσιμο:</strong> {fuel}</li>
//                 <li><strong>Χρώμα:</strong> {color}</li>
//             </ul>
//             <div style='margin-top:12px'>{salesImageHtml}</div>
//         ";

//         var msg = new EmailMessage(
//             null,
//             new[] { "Eirini.Skliri@kinsen.gr" },
//             null, null,
//             new[] { request.Email },
//             subject,
//             body,
//             true,
//             null // attachments προαιρετικά
//         );
//         await _emailSender.SendAsync(msg, "Offer");

//         // ---------- Logo για email προς πελάτη ----------
//         using var cref = _contextFactory.EnsureUmbracoContext();
//         var umbr = cref.UmbracoContext;

//         IPublishedContent? settingsNode = umbr.Content.GetAtRoot()
//             .SelectMany(x => x.DescendantsOrSelf())
//             .FirstOrDefault(x => x.HasProperty("kinsenLogo") && x.Value<IPublishedContent>("kinsenLogo") != null);

//         string logoTag = string.Empty;
//         string? logoUrl = settingsNode?.Value<IPublishedContent>("kinsenLogo")?.Url(mode: UrlMode.Absolute);
//         if (!string.IsNullOrWhiteSpace(logoUrl))
//         {
//             bool isLocal = string.Equals(Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase);
//             if (isLocal)
//             {
//                 string relative = logoUrl;
//                 if (Uri.TryCreate(logoUrl, UriKind.Absolute, out var abs)) relative = abs.LocalPath;

//                 string localPath = Path.Combine(_env.WebRootPath, relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
//                 byte[]? bytes = null;
//                 try
//                 {
//                     if (System.IO.File.Exists(localPath))
//                         bytes = await System.IO.File.ReadAllBytesAsync(localPath);
//                     else
//                     {
//                         var h = new HttpClientHandler
//                         {
//                             ServerCertificateCustomValidationCallback =
//                                 HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
//                         };
//                         using var http2 = new HttpClient(h);
//                         bytes = await http2.GetByteArrayAsync(logoUrl);
//                     }
//                 }
//                 catch { }

//                 if (bytes is { Length: > 0 })
//                 {
//                     string b64 = Convert.ToBase64String(bytes);
//                     logoTag = $"<img src='data:image/png;base64,{b64}' alt='Kinsen' width='220' style='display:block;width:200px;height:auto;border:0;outline:none;text-decoration:none;' />";
//                 }
//             }
//             else
//             {
//                 logoTag = $"<img src='{logoUrl}' alt='Kinsen' width='220' style='display:block;width:220px;height:auto;border:0;outline:none;text-decoration:none;margin-bottom:12px;' />";
//             }
//         }

//         // ---------- email προς πελάτη ----------
//         var companyEmail   = "sales@kinsen.gr";
//         var companyPhone   = "+30 211 190 3000";
//         var companyAddress = "Λεωφόρος Αθηνών 71, Αθήνα";
//         var cookiesUrl     = $"{Request.Scheme}://{Request.Host}/cookies";
//         var termsUrl       = $"{Request.Scheme}://{Request.Host}/terms";

//         var hpHtml = hp.HasValue
//             ? $"<p style='margin:0 0 8px 0;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:13px;line-height:1.7;color:#023859;'><strong>Ιπποδύναμη:</strong> {hp} hp</p>"
//             : "";

//         var customerSubject = "Λάβαμε το αίτημά σας – Kinsen";
//         var customerBody = $@"
//         <!doctype html>
//         <html xmlns='http://www.w3.org/1999/xhtml'>
//         <head>
//         <meta http-equiv='Content-Type' content='text/html; charset=UTF-8' />
//         <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
//         <title>Kinsen</title>
//         </head>
//         <body style='margin:0;padding:0;'>
//         <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0' style='padding:0;margin:0;'>
//             <tr>
//             <td align='center'>

//                 <!-- Wrapper -->
//                 <table role='presentation' width='600' border='0' cellspacing='0' cellpadding='0' style='width:600px;'>

//                 <!-- Logo -->
//                 <tr>
//                     <td align='center' style='padding:24px;'>
//                     {logoTag}
//                     </td>
//                 </tr>

//                 <!-- Title -->
//                 <tr>
//                     <td align='center' style='padding:0 24px 10px 24px;'>
//                     <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:400;
//                                 font-size:18px;line-height:1.4;color:#39c0c3;;margin:10px 0;'>
//                         Σας ευχαριστούμε για το ενδιαφέρον σας!
//                     </div>
//                     </td>
//                 </tr>

//                 <!-- Greeting -->
//                 <tr>
//                     <td align='left' style='padding:0 24px 20px 24px;'>
//                     <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;
//                                 font-size:14px;line-height:1.6;color:#000;font-weight:300;'>
//                         <div style='margin-bottom:8px;'>
//                         <b>Αγαπητέ/ή {request.FirstName} {request.LastName}</b>
//                         </div>
//                         Λάβαμε το αίτημά σας για προσφορά. Ετοιμάσαμε αναλυτικά τα στοιχεία του οχήματος
//                         που επιλέξατε. Η προσφορά ισχύει για δέκα (10) ημερολογιακές ημέρες από την
//                         ημερομηνία παραλαβής της.
//                     </div>
//                     </td>
//                 </tr>

//                 <!-- Car Card -->
//                 <tr>
//                     <td align='center' style='padding:0 24px 30px 24px;'>
//                     <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0'
//                             style='border-radius:8px;overflow:hidden;'>
//                         <tr>
//                         <td align='center' style='padding:20px;'>
//                             <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:16px;font-weight:600;color:#023859;margin-bottom:6px;'>
//                             {maker} {model}
//                             </div>
//                             {imgTag}
//                             <div style='margin-top:10px;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:14px;line-height:1.5;color:#023859;'>
//                             {year} · {km} km · {fuel}
//                             </div>
//                             <div style='margin-top:8px;font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:16px;font-weight:700;color:#007c91;'>
//                             {price} €
//                             </div>
//                         </td>
//                         </tr>
//                     </table>
//                     </td>
//                 </tr>

//                 <!-- Footer -->
//                 <tr>
//                     <td align='center' style='padding:20px;color:#000;
//                                             font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:13px;line-height:1.6;'>
//                     <div style='margin-bottom:6px;'>Kinsen - Όμιλος Σαρακάκη</div>
//                     <div style='margin-bottom:6px;'>{companyAddress}</div>
//                     <div style='margin-bottom:6px;'>📞 {companyPhone}</div>
//                     <div style='margin-bottom:6px;'>✉️ <a href='mailto:{companyEmail}' style='color:#ffffff;text-decoration:none;'>{companyEmail}</a></div>
//                     <div style='margin-top:10px;font-size:11px;'>
//                         <a href='{termsUrl}' style='color:#000;text-decoration:underline;margin-right:8px;'>Όροι & Προϋποθέσεις</a>
//                         <a href='{cookiesUrl}' style='color:#000;text-decoration:underline;'>Πολιτική Cookies</a>
//                     </div>
//                     </td>
//                 </tr>
//             </td>
//             </tr>
//         </table>
//         </body>
//         </html>";

//         var customerMsg = new EmailMessage(
//             null,
//             new[] { request.Email },
//             null, null,
//             new[] { "Eirini.Skliri@kinsen.gr" },
//             customerSubject,
//             customerBody,
//             true,
//             null
//         );

//         try { await _emailSender.SendAsync(customerMsg, "OfferCustomerConfirmation"); }
//         catch { /* optional: log */ }

//         return Ok(new { ok = true });
//     }
// }

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
            imageUrl = media?.GetCropUrl() ?? media?.MediaUrl() ?? ""
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

        // //********************* Kinsen Logo (robust) **************************
        // using var cref = _contextFactory.EnsureUmbracoContext();
        // var umbr = cref.UmbracoContext;

        // IPublishedContent? settingsNode = umbr.Content.GetAtRoot()
        //     .SelectMany(x => x.DescendantsOrSelf())
        //     // Βρες τον ΠΡΩΤΟ κόμβο που έχει συμπληρωμένο το kinsenLogo (single media picker)
        //     .FirstOrDefault(x => x.HasProperty("kinsenLogo") && x.Value<IPublishedContent>("kinsenLogo") != null);

        // string? logoUrl = null;
        // if (settingsNode != null)
        // {
        //     var logoMedia = settingsNode.Value<IPublishedContent>("kinsenLogo");
        //     logoUrl = logoMedia?.Url(mode: UrlMode.Absolute); // absolute URL για email
        // }

        // // Αν είσαι σε localhost, κάνε Base64 inline (ώστε να φαίνεται 100%)
        // string logoTag = string.Empty;
        // if (!string.IsNullOrWhiteSpace(logoUrl))
        // {
        //     bool isLocal = string.Equals(Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase);

        //     if (isLocal)
        //     {
        //         // Δοκίμασε να το διαβάσεις από δίσκο, αλλιώς κατέβασέ το με HTTP και κάνε Base64
        //         string relative = logoUrl;
        //         if (Uri.TryCreate(logoUrl, UriKind.Absolute, out var abs)) relative = abs.LocalPath;

        //         string localPath = Path.Combine(_env.WebRootPath, relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        //         byte[]? bytes = null;

        //         try
        //         {
        //             if (System.IO.File.Exists(localPath))
        //             {
        //                 bytes = await System.IO.File.ReadAllBytesAsync(localPath);
        //             }
        //             else
        //             {
        //                 var handler = new HttpClientHandler
        //                 {
        //                     ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator // μόνο για dev
        //                 };
        //                 using var http = new HttpClient(handler);
        //                 bytes = await http.GetByteArrayAsync(logoUrl);
        //             }
        //         }
        //         catch { /* log αν θέλεις */ }

        //         if (bytes != null && bytes.Length > 0)
        //         {
        //             string b64 = Convert.ToBase64String(bytes);
        //             logoTag = $"<img src='data:image/png;base64,{b64}' alt='Kinsen' width='220' style='display:block;width:200px;height:auto;border:0;outline:none;text-decoration:none;' />";
        //         }
        //     }
        //     else
        //     {
        //         // Production: ελαφρύ remote image
        //         logoTag = $"<img src='{logoUrl}' alt='Kinsen' width='220' style='display:block;width:200px;height:auto;border:0;outline:none;text-decoration:none;margin-bottom:12px;' />";
        //     }
        // }

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
                                <td align='center'>
                                {logoTag}
                                </td>
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
                            <td width='240' align='center' style='padding:10px;background:#f9f9f9;height:180px;'>
                                {await ToBase64ImgTag(imgTag, $"{maker} {model}", 220)}
                            </td>

                            <!-- Στοιχεία δεξιά -->
                            <td style='padding:12px;vertical-align:top;font-family:Segoe UI,Roboto,Arial,sans-serif;color:#000000;'>
                                <div style='font-size:18px;font-weight:700;margin-bottom:6px;'>{maker} {model}</div>
                                <div style='font-size:13px;color:#333;margin-bottom:8px;'>
                                   • {(string.IsNullOrWhiteSpace(year) ? "-" : year)} <br> •
                                    {(string.IsNullOrWhiteSpace(km) ? "-" : km + " km")} <br> • {fuel} <br>
                                </div>
                                <div style='font-size:16px;font-weight:600;color:#007c91;margin-bottom:8px;'>{price} €</div>
                            </td>
                        </tr>
                        </table>
                    </td></tr>

                    <tr>
                        <td align='center' style='padding:10px 24px 20px 24px;'>
                            <div style='font-family:Segoe UI,Roboto,Arial,sans-serif;font-weight:700;font-size:16px;line-height:1.7;color:#000000;margin:8px 0 10px 0;'>
                                Παραμένουμε στη διάθεσή σας!
                            </div>
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