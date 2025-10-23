using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Controllers;

namespace KinsenOfficial.Controllers
{
    [ApiController]
    [Route("umbraco/api/carstock")]
    [Produces("application/json")]
    public class CarStockWriteController : UmbracoApiController
    {
        private readonly IConfiguration _cfg;
        private readonly IContentService _contentService;
        private readonly IContentTypeService _contentTypeService;

        // appsettings.json (χρησιμοποίησε ΜΟΝΟ GUID)
        private Guid UsedCarSalesPageKey =>
            Guid.TryParse(_cfg["CarStock:UsedCarSalesPageId"], out var key) ? key : Guid.Empty;

        private string BlockPropertyAlias => _cfg["CarStock:BlockPropertyAlias"] ?? "carCardBlock";
        private string CardElementAlias   => _cfg["CarStock:CardElementAlias"] ?? "carCard";
        private string WebhookSecret      => _cfg["CarStock:WebhookSecret"] ?? "";

        public CarStockWriteController(
            IConfiguration cfg,
            IContentService contentService,
            IContentTypeService contentTypeService)
        {
            _cfg = cfg;
            _contentService = contentService;
            _contentTypeService = contentTypeService;
        }

        [HttpPost("cars-updated")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Consumes("application/json")]
        public IActionResult CarsUpdated([FromBody] CarStockEnvelope payload)
        {
            // απλή προστασία
            var key = Request.Headers["X-API-KEY"].ToString();
            if (!string.IsNullOrEmpty(WebhookSecret) && key != WebhookSecret)
                return Unauthorized("Invalid API key");

            if (UsedCarSalesPageKey == Guid.Empty)
                return BadRequest("CarStock:UsedCarSalesPageId missing.");

            var page = _contentService.GetById(UsedCarSalesPageKey);
            if (page == null)
                return NotFound("usedCarSalesPage not found.");

            if (payload?.Cars == null || payload.Cars.Count == 0)
                return BadRequest("No cars in payload.");

            // map στο DTO που ταιριάζει στο element
            var cars = payload.Cars
                .Where(c => c?.Id > 0)
                .Select(s => new CarDto
                {
                    Id               = s!.Id!.Value,
                    Maker            = s.Make ?? "",
                    Model            = s.Model ?? "",
                    YearRelease      = s.YearRelease?.ToString() ?? "", // αν είναι Text στο element
                    Price            = s.Price?.ToString() ?? "",       // αν είναι Text στο element
                    Km               = s.Km?.ToString() ?? "",          // αν είναι Text στο element
                    Cc               = s.Cc ?? 0,
                    Hp               = s.Hp ?? 0,
                    Fuel             = s.Fuel ?? "",
                    TransmissionType = s.TransmissionType ?? "",
                    Color            = s.Color ?? "",
                    TypeOfDiscount   = s.TypeOfDiscount ?? "",
                    TypeOfCar        = s.TypeOfCar ?? "",
                    CarPicUrl        = s.ImageUrl ?? ""
                })
                .ToList();

            try
            {
                ReplaceBlockListWithCars(page, cars); // ⬅ περνάμε ΤΗ σελίδα
                return Ok(new { ok = true, saved = cars.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { ok = false, error = ex.Message });
            }
        }

        private void ReplaceBlockListWithCars(IContent page, List<CarDto> cars)
        {
            var cardType = _contentTypeService.Get(CardElementAlias)
                           ?? throw new InvalidOperationException($"Element type '{CardElementAlias}' not found.");

            var layoutItems  = new List<object>();
            var contentData  = new List<object>();
            var settingsData = new List<object>();

            foreach (var car in cars)
            {
                var key = Guid.NewGuid();
                var udi = $"umb://element/{key:D}";

                layoutItems.Add(new Dictionary<string, object?>
                {
                    ["contentUdi"]  = udi,
                    ["settingsUdi"] = null
                });

                // ⚠️ ΤΑΙΡΙΑΞΕ ΑΥΤΑ ΤΑ ALIASES με το στοιχείο "carCard" σου
                contentData.Add(new Dictionary<string, object?>
                {
                    ["udi"]              = udi,
                    ["contentTypeKey"]   = cardType.Key,
                    ["carID"]            = car.Id,
                    ["maker"]            = car.Maker,
                    ["model"]            = car.Model,

                    // Αν ΤΑ ΕΧΕΙΣ ως Textstring στο element:
                    ["price"]            = car.Price,
                    ["yearRelease"]      = car.YearRelease,
                    ["km"]               = car.Km,

                    // Αν Αντίθετα είναι Number, βάλε αριθμούς:
                    // ["price"]            = decimal.TryParse(car.Price, out var p) ? p : (decimal?)null,
                    // ["yearRelease"]      = int.TryParse(car.YearRelease, out var y) ? y : (int?)null,
                    // ["km"]               = int.TryParse(car.Km, out var km) ? km : (int?)null,

                    ["fuel"]             = car.Fuel,
                    ["color"]            = car.Color,
                    ["cc"]               = car.Cc,
                    ["hp"]               = car.Hp,
                    ["transmissionType"] = car.TransmissionType,
                    ["typeOfDiscount"]   = car.TypeOfDiscount,
                    ["typeOfCar"]        = car.TypeOfCar,
                    // ["carPic"]         = car.CarPicUrl // μόνο αν το prop είναι Text URL (ΟΧΙ media picker)
                });
            }

            var blockValue = new Dictionary<string, object?>
            {
                ["layout"] = new Dictionary<string, object?>
                {
                    ["Umbraco.BlockList"] = layoutItems
                },
                ["contentData"]  = contentData,
                ["settingsData"] = settingsData
            };

            var json = JsonSerializer.Serialize(blockValue);
            page.SetValue(BlockPropertyAlias, json);

            var save = _contentService.Save(page);
            if (!save.Success)
                throw new Exception("Save failed.");

            // Publish (invariant)
            var pub = _contentService.Publish(page, Array.Empty<string>());
            if (!pub.Success)
                throw new Exception("Publish failed.");
        }
    }

    // ====== Payload models ======
    public class CarStockEnvelope
    {
        [JsonPropertyName("cars")] public List<CarStockCar>? Cars { get; set; }
    }

    public class CarStockCar
    {
        [JsonPropertyName("id")]               public int? Id { get; set; }
        [JsonPropertyName("make")]             public string? Make { get; set; }
        [JsonPropertyName("model")]            public string? Model { get; set; }
        [JsonPropertyName("yearRelease")]      public int? YearRelease { get; set; }
        [JsonPropertyName("price")]            public decimal? Price { get; set; }
        [JsonPropertyName("km")]               public int? Km { get; set; }
        [JsonPropertyName("cc")]               public float? Cc { get; set; }
        [JsonPropertyName("hp")]               public float? Hp { get; set; }
        [JsonPropertyName("typeOfDiscount")]   public string? TypeOfDiscount { get; set; }
        [JsonPropertyName("fuel")]             public string? Fuel { get; set; }
        [JsonPropertyName("transmissionType")] public string? TransmissionType { get; set; }
        [JsonPropertyName("color")]            public string? Color { get; set; }
        [JsonPropertyName("typeOfCar")]        public string? TypeOfCar { get; set; }
        [JsonPropertyName("image_url")]        public string? ImageUrl { get; set; }
    }

    public class CarDto
    {
        public int    Id { get; set; }
        public string Maker { get; set; } = "";
        public string Model { get; set; } = "";
        public string YearRelease { get; set; } = "";
        public string Price { get; set; } = "";
        public string Km { get; set; } = "";
        public float  Cc { get; set; }
        public float  Hp { get; set; }
        public string Fuel { get; set; } = "";
        public string TransmissionType { get; set; } = "";
        public string Color { get; set; } = "";
        public string TypeOfDiscount { get; set; } = "";
        public string TypeOfCar { get; set; } = "";
        public string CarPicUrl { get; set; } = "";
    }
}
