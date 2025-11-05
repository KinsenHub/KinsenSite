using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Controllers;
using System.Globalization;
using System.Text;

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
        private readonly IDataTypeService _dataTypeService;
        private readonly ILogger<CarStockWriteController> _logger;


        // === CONFIG VALUES ===
        private Guid UsedCarSalesPageKey =>
            Guid.TryParse(_cfg["CarStock:UsedCarSalesPageId"], out var key) ? key : Guid.Empty;

        private string BlockPropertyAlias => _cfg["CarStock:BlockPropertyAlias"] ?? "carCardBlock";
        private string CardElementAlias => _cfg["CarStock:CardElementAlias"] ?? "carCard";
        private string WebhookSecret => _cfg["CarStock:WebhookSecret"] ?? "";

        public CarStockWriteController(
            IConfiguration cfg,
            IContentService contentService,
            IContentTypeService contentTypeService,
            IDataTypeService dataTypeService,
            ILogger<CarStockWriteController> logger)
        {
            _cfg = cfg;
            _contentService = contentService;
            _contentTypeService = contentTypeService;
            _dataTypeService = dataTypeService;
            _logger = logger;

        }

        // === Helper για εκτύπωση arrays ===
        private static string Arr(object? o)
        {
            if (o is string[] sa) return "[" + string.Join(",", sa) + "]";
            if (o is IEnumerable<string> ie) return "[" + string.Join(",", ie) + "]";
            if (o is string s) return "[" + s + "]";
            return o?.ToString() ?? "[]";
        }

        // === String normalization ===
        private static string Fold(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var formD = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (var ch in formD)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            var flat = sb.ToString().Normalize(NormalizationForm.FormC);
            flat = System.Text.RegularExpressions.Regex.Replace(flat, @"\s+", " ");
            return flat.Trim();
        }

        // === Canonicalize strings (normalize user input) ===
        private static string Canonicalize(string propertyAlias, string? incoming)
        {
            var f = Fold(incoming);
            if (propertyAlias == "maker")
            {
                return f switch
                {
                    "volvo" => "Volvo",
                    "mitsubishi" => "Mitsubishi",
                    "bmw" => "BMW",
                    "honda" => "Honda",
                    "maserati" => "Maserati",
                    _ => incoming ?? ""
                };
            }
            if (propertyAlias == "fuel")
            {
                return f switch
                {
                    "diesel" or "πετρελαιο" => "Πετρέλαιο",
                    "petrol" or "gasoline" or "βενζινη" => "Βενζίνη",
                    "hybrid" or "υβριδικο" => "Υβριδικό",
                    "electric" or "ηλεκτρικο" => "Ηλεκτρικό",
                    _ => incoming ?? ""
                };
            }
            if (propertyAlias == "transmissionType")
            {
                return f switch
                {
                    "automatic" or "αυτοματο" => "Αυτόματο",
                    "manual" or "χειροκινητο" => "Χειροκίνητο",
                    _ => incoming ?? ""
                };
            }
            if (propertyAlias == "color")
            {
                return f switch
                {
                    "white" or "λευκο" or "ασπρο" => "Άσπρο",
                    "black" or "μαυρο" => "Μαύρο",
                    "blue" or "μπλε" => "Μπλε",
                    "silver" or "ασημι" => "Ασημί",
                    "gray" or "γκρι" => "Γκρι",
                    "red" or "κοκκινο" => "Κόκκινο",
                    _ => incoming ?? ""
                };
            }
            return incoming ?? "";
        }

        // === Map to dropdown value index ===
        // === Map to dropdown value index ===
    private string? MapToStoredDropdownValue(IContentType elementType, string propertyAlias, string? incoming)
    {
        if (string.IsNullOrWhiteSpace(incoming)) return null;

        var propType = elementType.CompositionPropertyTypes.FirstOrDefault(p => p.Alias == propertyAlias);
        if (propType == null) return null;

        var dt = _dataTypeService.GetDataType(propType.DataTypeId);
        if (dt == null) return null;

        // --- ConfigurationData (Umbraco 10+)
        IDictionary<string, object>? cfg = null;
        var confDataProp = dt.GetType().GetProperty("ConfigurationData");
        if (confDataProp?.GetValue(dt) is IDictionary<string, object> confDict)
            cfg = confDict;

        if (cfg == null || !cfg.ContainsKey("items"))
            return null;

        // --- Πάρε τα items (λίστα dropdown)
        var arr = cfg["items"] as IEnumerable<object>;
        if (arr == null) return null;

        var items = arr.Select(x => x?.ToString()?.Trim() ?? "").ToList();

        // --- Βρες το index
        var idx = items.FindIndex(i => string.Equals(Fold(i), Fold(incoming), StringComparison.OrdinalIgnoreCase));
        if (idx == -1)
        {
            Console.WriteLine($"[WARN] '{incoming}' not found in dropdown items: {string.Join(",", items)}");
            return null;
        }

        var label = items[idx];
        _logger.LogInformation($"[MATCH] '{incoming}' -> index {idx} -> label '{label}'");

        // Επιστρέφουμε το *label* (π.χ. "Volvo"), όχι τον index
        return label;
    }



        // === Main endpoint ===
        [HttpPost("cars-updated")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Consumes("application/json")]
        public IActionResult CarsUpdated([FromBody] List<CarStockCar>? carsPayload)
        {
            if (carsPayload == null || carsPayload.Count == 0)
                return BadRequest("No cars in payload.");

            var cars = carsPayload
                .Where(c => c?.CarId != null && c.CarId > 0)
                .Select(s => new CarDto
                {
                    CarId = s.CarId ?? 0,
                    Maker = s.Maker ?? "",
                    Model = s.Model ?? "",
                    YearRelease = s.YearRelease?.ToString() ?? "",
                    Price = s.Price?.ToString() ?? "",
                    Km = s.Km?.ToString() ?? "",
                    Cc = s.Cc ?? 0,
                    Hp = s.Hp ?? 0,
                    Fuel = s.Fuel ?? "",
                    TransmissionType = s.TransmissionType ?? "",
                    Color = s.Color ?? "",
                    TypeOfCar = s.TypeOfCar ?? "",
                    CarPicUrl = s.ImageUrl ?? ""
                })
                .ToList();

            var page = _contentService.GetById(UsedCarSalesPageKey);
            if (page == null) return NotFound("UsedCarSalesPage not found.");

            ReplaceBlockListWithCars(page, cars);
            return Ok(new { ok = true, saved = cars.Count });
        }

        // === Core logic ===
        private void ReplaceBlockListWithCars(IContent page, List<CarDto> cars)
        {
            var cardType = _contentTypeService.Get(CardElementAlias)
                ?? throw new InvalidOperationException($"Element type '{CardElementAlias}' not found.");

            var layoutItems = new List<object>();
            var contentData = new List<object>();
            var settingsData = new List<object>();

            foreach (var car in cars)
            {
                var elementKey = Guid.NewGuid();
                var elementUdi = $"umb://element/{elementKey:D}";

                // === Normalize + Map μόνο maker ===
                var makerVal = MapToStoredDropdownValue(cardType, "maker", Canonicalize("maker", car.Maker));

                // === LOG 1: πριν φτιαχτούν τα props ===
                Console.WriteLine($"[BEFORE PROPS] CarId={car.CarId} | MakerFromJson='{car.Maker}' | Canonical='{Canonicalize("maker", car.Maker)}' | Mapped='{makerVal}'");

                // === Props ===
                Dictionary<string, object?> BuildProps()
                {
                    var d = new Dictionary<string, object?>
                    {
                        ["carId"] = car.CarId,
                        ["model"] = car.Model,
                        ["price"] = decimal.TryParse(car.Price, out var p) ? p : 0m
                    };

                    if (!string.IsNullOrEmpty(makerVal))
                        d["maker"] = new[] { makerVal };

                    // === LOG 2: τελικό maker value ===
                    Console.WriteLine($"[PROPS READY] CarId={car.CarId} | maker={(d.ContainsKey("maker") ? string.Join(",", (string[])d["maker"]) : "EMPTY")}");

                    return d;
                }

                // Layout
                layoutItems.Add(new Dictionary<string, object?>
                {
                    ["contentUdi"] = elementUdi,
                    ["settingsUdi"] = null
                });

                // Content
                var content = new Dictionary<string, object?>
                {
                    ["key"] = elementKey,
                    ["udi"] = elementUdi,
                    ["contentTypeKey"] = cardType.Key,
                    ["contentTypeAlias"] = cardType.Alias
                };

                foreach (var kv in BuildProps())
                    content[kv.Key] = kv.Value;

                contentData.Add(content);
            }

            // === Δημιουργία JSON BlockList ===
            var blockValue = new Dictionary<string, object?>
            {
                ["layout"] = new Dictionary<string, object?>
                {
                    ["Umbraco.BlockList"] = layoutItems
                },
                ["contentData"] = contentData,
                ["settingsData"] = settingsData
            };

            var json = JsonSerializer.Serialize(blockValue, new JsonSerializerOptions { WriteIndented = true });

            // === LOG 3: JSON πριν το save ===
            Console.WriteLine("----- BLOCK JSON BEFORE SAVE -----");
            Console.WriteLine(json);

            // === Save & Publish ===
            var prop = page.Properties[BlockPropertyAlias];
            var propType = prop?.PropertyType;
            var cultures = page.AvailableCultures?.ToArray() ?? Array.Empty<string>();

            if (propType?.VariesByCulture() == true && cultures.Length > 0)
            {
                foreach (var iso in cultures)
                    page.SetValue(BlockPropertyAlias, json, iso);

                _contentService.Save(page);
                _contentService.Publish(page, cultures);
            }
            else
            {
                page.SetValue(BlockPropertyAlias, json);
                _contentService.Save(page);
                _contentService.Publish(page, Array.Empty<string>());
            }

            // === LOG 4: JSON μετά το publish ===
            Console.WriteLine("----- AFTER PUBLISH -----");
            var after = page.GetValue(BlockPropertyAlias);
            Console.WriteLine(after is string s ? s : JsonSerializer.Serialize(after));
        }

    }

    // === Data models ===
    public class CarStockCar
    {
        [JsonPropertyName("carId")] public int? CarId { get; set; }
        [JsonPropertyName("maker")] public string? Maker { get; set; }
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("yearRelease")] public int? YearRelease { get; set; }
        [JsonPropertyName("price")] public decimal? Price { get; set; }
        [JsonPropertyName("km")] public int? Km { get; set; }
        [JsonPropertyName("cc")] public double? Cc { get; set; }
        [JsonPropertyName("hp")] public double? Hp { get; set; }
        [JsonPropertyName("fuel")] public string? Fuel { get; set; }
        [JsonPropertyName("color")] public string? Color { get; set; }
        [JsonPropertyName("typeOfCar")] public string? TypeOfCar { get; set; }
        [JsonPropertyName("transmissionType")] public string? TransmissionType { get; set; }
        [JsonPropertyName("image_url")] public string? ImageUrl { get; set; }
    }

    public class CarDto
    {
        public int CarId { get; set; }
        public string Maker { get; set; } = "";
        public string Model { get; set; } = "";
        public string YearRelease { get; set; } = "";
        public string Price { get; set; } = "";
        public string Km { get; set; } = "";
        public double Cc { get; set; }
        public double Hp { get; set; }
        public string Fuel { get; set; } = "";
        public string TransmissionType { get; set; } = "";
        public string Color { get; set; } = "";
        public string TypeOfCar { get; set; } = "";
        public string CarPicUrl { get; set; } = "";
    }
}
