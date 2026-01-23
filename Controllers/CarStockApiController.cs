using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Controllers;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using System.Text.Json.Nodes;

namespace KinsenOfficial.Controllers
{
    public class CarStockState
    {
        public bool Initialized { get; set; }
    }

    public static class CarStockStateStore
    {
        private static readonly string FilePath =
            Path.Combine(AppContext.BaseDirectory, "App_Data", "carstock_state.json");

        public static CarStockState Load()
        {
            try
            {
                if (!System.IO.File.Exists(FilePath))
                    return new CarStockState { Initialized = false };

                var json = System.IO.File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<CarStockState>(json) ?? new CarStockState();
            }
            catch
            {
                return new CarStockState { Initialized = false };
            }
        }

        public static void Save(CarStockState state)
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            System.IO.File.WriteAllText(FilePath, json);
        }
    }  


    [ApiController]
    [Route("umbraco/api/carstock")]
    [Produces("application/json")]
    public class CarStockWriteController : UmbracoApiController
    {
        private readonly IConfiguration _cfg;
        private readonly IContentService _contentService;
        private readonly IContentTypeService _contentTypeService;
        private readonly Umbraco.Cms.Core.Web.IUmbracoContextAccessor _accessor;
        private readonly IDataTypeService _dataTypeService;
        private readonly ILogger<CarStockWriteController> _logger;
        private readonly IPublishedContentQuery _publishedContentQuery;
        private readonly IUmbracoContextFactory _umbracoContextFactory;


        // appsettings.json (χρησιμοποίησε ΜΟΝΟ GUID)
        private Guid UsedCarSalesPageKey =>
            Guid.TryParse(_cfg["CarStock:UsedCarSalesPageId"], out var key) ? key : Guid.Empty;

        private Guid HomePageKey =>
            Guid.TryParse(_cfg["CarStock:HomePageId"], out var key) ? key : Guid.Empty;

        private string BlockPropertyAlias => _cfg["CarStock:BlockPropertyAlias"] ?? "carCardBlock";
        private string CardElementAlias   => _cfg["CarStock:CardElementAlias"] ?? "carCard";
        private string CarouselBlockPropertyAlias => _cfg["CarStock:CarouselBlockPropertyAlias"] ?? "carouselCars";
        private string WebhookSecret      => _cfg["CarStock:WebhookSecret"] ?? "";

        public CarStockWriteController(
            IConfiguration cfg,
            IContentService contentService,
            IContentTypeService contentTypeService,
            IDataTypeService dataTypeService,
            ILogger<CarStockWriteController> logger,
            IUmbracoContextFactory umbracoContextFactory,
            IPublishedContentQuery publishedContentQuery)
        {
            _cfg = cfg;
            _contentService = contentService;
            _contentTypeService = contentTypeService;
            _dataTypeService = dataTypeService;
            _logger = logger;
            _umbracoContextFactory = umbracoContextFactory;
            _publishedContentQuery = publishedContentQuery;
        }
        
        [HttpPost("cars-updated")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Consumes("application/json")]
        public IActionResult CarsUpdated([FromBody] List<CarStockCar>? carsPayload)
        {
            if (carsPayload == null || carsPayload.Count == 0)
                return BadRequest("No cars in payload.");

            _logger.LogInformation("Incoming carsPayload count = {Count}", carsPayload.Count);

            foreach (var car in carsPayload)
            {
                _logger.LogInformation("Payload Car → ID:{Id}, Maker:{Maker}, Model:{Model}",
                    car.CarId, car.Maker, car.Model);
            }

            static string NormalizeName(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return "";
                value = value.Trim().ToLowerInvariant();
                return char.ToUpper(value[0]) + value.Substring(1);
            }

            // ✔ Μετατροπή payload -> CarDto
            var newCars = carsPayload
            .Where(c => c?.CarId > 0)
            .Select(s => new CarDto
            {
                CarId = s.CarId ?? 0,
                Maker = NormalizeName(s.Maker),
                Model = NormalizeName(s.Model),

                YearRelease = s.YearRelease ?? 0,
                Price = s.Price.HasValue ? Math.Round(s.Price.Value, 0, MidpointRounding.AwayFromZero) : 0m,
                Km = s.Km ?? 0,
                Cc = s.Cc ?? 0,
                Hp = s.Hp ?? 0,

                Fuel = s.Fuel ?? "",
                TransmissionType = s.TransmissionType ?? "",
                Color = NormalizeName(s.Color),
                Offer = s.Offer ?? false,
                TypeOfCar = s.TypeOfCar ?? "",
                CarPic = ""
            })
            .ToList();

            // ✔ Φόρτωση σελίδας
            var page = _contentService.GetById(UsedCarSalesPageKey);
            if (page == null)
                return NotFound("usedCarSalesPage not found.");

            var existingCars = LoadExistingCars(page);
            _logger.LogInformation("EXISTING CARS IN CONTENT = {Count}", existingCars.Count);

            // ✔ Index υπάρχοντα ανά CarId
            var existingMap = existingCars.ToDictionary(c => c.CarId);

            // ✔ Για να μείνει το return ίδιο, κρατάμε carsToAdd μόνο για τα πραγματικά νέα
            var carsToAdd = new List<CarDto>();

            foreach (var incoming in newCars)
            {
                if (existingMap.ContainsKey(incoming.CarId))
                {
                    var existing = existingMap[incoming.CarId];

                    // ⛔ ΔΕΝ ΠΕΙΡΑΖΟΥΜΕ ΤΗ ΦΩΤΟΓΡΑΦΙΑ
                    incoming.CarPic = existing.CarPic;
                    incoming.TenPhotosForUsedCarSales = existing.TenPhotosForUsedCarSales;

                    // ✅ OVERWRITE ΟΛΑ ΤΑ ΥΠΟΛΟΙΠΑ
                    existingMap[incoming.CarId] = incoming;
                }
                else
                {
                    // ✅ NEW
                    existingMap.Add(incoming.CarId, incoming);
                    carsToAdd.Add(incoming);
                }
            }

            _logger.LogInformation("CARS TO ADD (new only) = {Count}", carsToAdd.Count);

            // ✔ Τελικό merged (με updates + adds)
            var merged = existingMap.Values.ToList();
            _logger.LogInformation("FINAL MERGED CAR COUNT = {Total}", merged.Count);

            // ✔ Αντικατάσταση block list
            ReplaceBlockListWithCars(page, merged);

            try
            {
                SyncHomeCarouselOffers(newCars); // ή carsPayload mapped list (newCars είναι mapped/normalized)
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncHomeCarouselOffers failed.");
                // δεν σπάμε το βασικό endpoint — ο controller σου πρέπει να μείνει “bulletproof”
            }

            // ✅ ΑΚΡΙΒΩΣ ΟΠΩΣ ΤΟ ΘΕΣ
            return Ok(new { ok = true, added = carsToAdd.Count });
        }

        private List<CarDto> LoadExistingCars(IContent page)
        {
            var result = new List<CarDto>();
            const string blockAlias = "carCardBlock";

            // 1) Πήγαινε στον PUBLISHED κόσμο
            using var cref = _umbracoContextFactory.EnsureUmbracoContext();
            var published = cref.UmbracoContext.Content?.GetById(page.Id);

            if (published == null)
            {
                _logger.LogWarning("LoadExistingCars: ΔΕΝ βρέθηκε published node για Id={Id}", page.Id);
                return result;
            }

            // 2) Πάρε το BlockListModel από published
            var blocks = published.Value<BlockListModel>(blockAlias);

            if (blocks == null || !blocks.Any())
            {
                _logger.LogWarning("LoadExistingCars: το BlockList '{Alias}' είναι NULL ή άδειο στο node {Id}", blockAlias, page.Id);
                return result;
            }

            _logger.LogInformation("LoadExistingCars: βρέθηκαν {Count} block items στο '{Alias}'", blocks.Count(), blockAlias);

            int index = 0;

            foreach (var block in blocks)
            {
                index++;

                var content = block.Content;

                JsonNode? tenPhotosNode = null;

                var tenProp = content.GetProperty("tenPhotosForUsedCarSales");
                var tenSource = tenProp?.GetSourceValue()?.ToString();

                if (!string.IsNullOrWhiteSpace(tenSource))
                {
                    // κρατάς το ακριβές JSON του nested blocklist
                    tenPhotosNode = JsonNode.Parse(tenSource);
                }

                if (content == null)
                {
                    _logger.LogWarning("LoadExistingCars: block #{Index} έχει null Content", index);
                    continue;
                }

                // 🔁 Τα aliases ΠΡΕΠΕΙ να είναι αυτά που έχεις στο στοιχείο Car block
                var carId  = content.Value<int?>("carId") ?? 0;
                var maker  = content.Value<string>("maker") ?? "";
                var model  = content.Value<string>("model") ?? "";
                var yearRelease = content.Value<int?>("yearRelease") ?? 0;
                var price = content.Value<decimal?>("price") ?? 0;
                var km = content.Value<int?>("km") ?? 0;
                var cc = content.Value<double?>("cc") ?? 0;
                var hp = content.Value<double?>("hp") ?? 0;
                var fuel = content.Value<string>("fuel") ?? "";
                var color = content.Value<string>("color") ?? "";
                var transmissionType = content.Value<string>("transmissionType") ?? "";
                var typeOfCar = content.Value<string>("typeOfCar") ?? "";
                string carPicUdi = "";

                var media = content.Value<IPublishedContent>("carPic");
                if (media != null)
                {
                    carPicUdi = Udi.Create(Constants.UdiEntityType.Media, media.Key).ToString();
                }
                else
                {
                    // 2) Αν είναι αποθηκευμένο ως string (π.χ. ήδη UDI ή url)
                    carPicUdi = content.Value<string>("carPic") ?? "";
                }

                _logger.LogInformation(
                    "LoadExistingCars: block #{Index} → ID:{Id}, Maker:{Maker}, Model:{Model}, Price:{Price}",
                    index, carId, maker, model, price
                );

                if (carId == 0)
                {
                    _logger.LogWarning("LoadExistingCars: block #{Index} έχει carId=0, παραλείπεται", index);
                    continue;
                }

                result.Add(new CarDto
                {
                    CarId = carId,
                    Maker = maker,
                    Model = model,
                    YearRelease = yearRelease,
                    Price = price,
                    Km = km,
                    Cc = cc,
                    Hp = hp,
                    Fuel = fuel,
                    Color = color,
                    TransmissionType = transmissionType,
                    TypeOfCar = typeOfCar,
                    CarPic = carPicUdi,
                    TenPhotosForUsedCarSales = tenPhotosNode
                });
            }

            _logger.LogInformation("LoadExistingCars: ΤΕΛΙΚΑ φορτώθηκαν {Count} cars από content", result.Count);

            return result;
        }
        

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
            // ενιαία κενά
            flat = System.Text.RegularExpressions.Regex.Replace(flat, @"\s+", " ");
            return flat.Trim();
        }

        // Επιστρέφει ΚΑΝΟΝΙΚΟ label ανά alias, από αυστηρό λεξικό συνωνύμων (Ελληνικά/Αγγλικά)
        private static string Canonicalize(string propertyAlias, string? incoming)
        {
            var f = Fold(incoming);

            // maker – βάλε μόνο τις μάρκες που έχεις στο dropdown
            if (propertyAlias == "maker")
            {
                return f switch
                {
                    "volvo" or "Volvo" => "Volvo",
                    "mitsubishi" or "Mitsubishi" => "Mitsubishi",
                    "bmw" or "BMW" or "Bmw" => "BMW",
                    "honda" or "Honda" => "Honda",
                    "maserati" or "Maserati" => "Maserati",
                    _ => incoming ?? ""
                };
            }

            // fuel
            if (propertyAlias == "fuel")
            {
                return f switch
                {
                    // English → Greek
                    "petrol-hybrid" or "hybrid" or "υβριδικο" or "Petrol-hybrid" => "Υβριδικό",
                    "diesel" or "Diesel" or "petrelaio" => "Πετρέλαιο",
                    "petrol" or "gasoline" or "Petrol" or "Gasoline" or "βενζινη" => "Βενζίνη",
                    "electric" or "Electric" => "Ηλεκτρικό",
                    "lpg" or "cng" or "αεριο" => "Αέριο",
                    _ => incoming ?? ""
                };
            }

            // transmissionType
            if (propertyAlias == "transmissionType")
            {
                return f switch
                {
                    "αυτοματο" or "automatic" => "Αυτόματο",
                    "χειροκινητο" or "manual" => "Χειροκίνητο",
                    _ => incoming ?? ""
                };
            }

            // typeOfCar 
            if (propertyAlias == "typeOfCar")
            {
                switch (f)
                {
                    case "sedan":
                    case "σεντάν":
                    case "σενταν":
                    case "Σεντάν":
                    case "Σενταν":
                        return "Sedan";

                    case "πολης":
                    case "πολη":
                    case "πόλη":
                    case "Πόλης":
                    case "city":
                    case "City":
                        return "Πόλης";

                    case "suv":
                    case "Suv":
                    case "SUV":
                        return "SUV";

                    default:
                        return incoming ?? "";
                }
            }

            return incoming ?? "";
        }

       [HttpGet("available-colors")]
        public IActionResult GetAvailableColors()
        {
            if (UsedCarSalesPageKey == Guid.Empty)
                return BadRequest("CarStock:UsedCarSalesPageId missing or invalid.");

            // 1) Φόρτωση published node
            using var cref = _umbracoContextFactory.EnsureUmbracoContext();
            var publishedPage = cref.UmbracoContext.Content?.GetById(UsedCarSalesPageKey);

            if (publishedPage == null)
                return NotFound("usedCarSalesPage not found.");

            // 2) Πάρε σωστά το blocklist
            var blocks = publishedPage.Value<BlockListModel>(BlockPropertyAlias);
            if (blocks == null || !blocks.Any())
                return Ok(Array.Empty<string>());

            // 3) Συλλογή ΟΛΩΝ των χρωμάτων
            var colorsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var block in blocks)
            {
                var content = block.Content;
                if (content == null) continue;

                var raw = content.Value<string>("color");
                if (string.IsNullOrWhiteSpace(raw)) continue;

                // Normalization
                string normalized = raw
                    .Normalize(NormalizationForm.FormD)
                    .Replace("ς", "σ")
                    .Replace("–", "-")
                    .Replace("—", "-")
                    .ToLowerInvariant()
                    .Trim();

                normalized = Regex.Replace(normalized, @"\s+", " ");
                normalized = Regex.Replace(normalized, @"\s*-\s*", "-");

                if (!string.IsNullOrEmpty(normalized))
                {
                    var culture = new System.Globalization.CultureInfo("el-GR");
                    normalized = char.ToUpper(normalized[0], culture) + normalized.Substring(1);
                }

                colorsSet.Add(normalized);
            }

            // 4) Ταξινόμηση με ελληνική κουλτούρα
            var finalColors = colorsSet
                .OrderBy(x => x, StringComparer.Create(new System.Globalization.CultureInfo("el-GR"), true))
                .ToList();

            return Ok(finalColors);
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
                var elementKey = Guid.NewGuid();
                var elementUdi = $"umb://element/{elementKey:D}";

                // layout
                layoutItems.Add(new Dictionary<string, object?>
                {
                    ["contentUdi"]  = elementUdi,
                    ["settingsUdi"] = null
                });

                var makerNormalized           = Canonicalize("maker", car.Maker);
                var fuelNormalized            = Canonicalize("fuel", car.Fuel);
                var colorNormalized           = Canonicalize("color", car.Color);
                var transmissionTypeNormalized= Canonicalize("transmissionType", car.TransmissionType);
                var typeOfCarNormalized       = Canonicalize("typeOfCar", car.TypeOfCar);

                Dictionary<string, object?> BuildProps()
                {
                    return new Dictionary<string, object?>
                    {
                        ["carId"]            = car.CarId,
                        ["maker"]            = makerNormalized,
                        ["model"]            = car.Model,

                        // 🔥 ΤΩΡΑ ΕΙΝΑΙ ΟΙ ΣΩΣΤΟΙ ΤΥΠΟΙ — ΟΧΙ PARSE
                        ["price"]            = car.Price,
                        ["yearRelease"]      = car.YearRelease,
                        ["km"]               = car.Km,
                        ["cc"]               = car.Cc,
                        ["hp"]               = car.Hp,

                        ["fuel"]             = fuelNormalized,
                        ["color"]            = colorNormalized,
                        ["transmissionType"] = transmissionTypeNormalized,
                        ["typeOfCar"]        = typeOfCarNormalized,
                        ["offer"]            = car.Offer,
                        ["carPic"]           = car.CarPic,
                        ["tenPhotosForUsedCarSales"] = car.TenPhotosForUsedCarSales
                    };
                }

                var elementVaries = cardType.VariesByCulture();
                var content = new Dictionary<string, object?>
                {
                    ["key"]              = elementKey,
                    ["udi"]              = elementUdi,
                    ["contentTypeKey"]   = cardType.Key,
                    ["contentTypeAlias"] = cardType.Alias
                };

                if (elementVaries)
                {
                    var cultures = page.AvailableCultures?.ToArray() ?? Array.Empty<string>();
                    if (cultures.Length == 0)
                    {
                        content["variants"] = new[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["culture"]    = null,
                                ["segment"]    = null,
                                ["name"]       = null,
                                ["properties"] = BuildProps()
                            }
                        };
                    }
                    else
                    {
                        var vars  = new List<object>();
                        var props = BuildProps();
                        foreach (var iso in cultures)
                        {
                            vars.Add(new Dictionary<string, object?>
                            {
                                ["culture"]    = iso,
                                ["segment"]    = null,
                                ["name"]       = null,
                                ["properties"] = props
                            });
                        }
                        content["variants"] = vars;
                    }
                }
                else
                {
                    foreach (var kv in BuildProps())
                        content[kv.Key] = kv.Value;
                }

                contentData.Add(content);
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

            var json = System.Text.Json.JsonSerializer.Serialize(blockValue);

            var prop           = page.Properties[BlockPropertyAlias];
            var propType       = prop?.PropertyType;
            var culturesForNode= page.AvailableCultures?.ToArray() ?? Array.Empty<string>();

            if (propType?.VariesByCulture() == true && culturesForNode.Length > 0)
            {
                foreach (var iso in culturesForNode)
                    page.SetValue(BlockPropertyAlias, json, iso);

                _contentService.Save(page);
                _contentService.Publish(page, culturesForNode);
            }
            else
            {
                page.SetValue(BlockPropertyAlias, json);
                _contentService.Save(page);
                _contentService.Publish(page, Array.Empty<string>());
            }
        }
        
        // Απεικόνιση των αυτοκινήτων στο front
        [HttpPost("displayCars")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Consumes("application/json")]
        public IActionResult DisplayCars([FromBody] object? _ = null)
        {
            if (UsedCarSalesPageKey == Guid.Empty)
                return BadRequest("CarStock:UsedCarSalesPageId missing or invalid.");

            var page = _contentService.GetById(UsedCarSalesPageKey);
            if (page == null)
                return NotFound("usedCarSalesPage not found.");

            var json = page.GetValue<string>(BlockPropertyAlias)?.ToString();
            if (string.IsNullOrWhiteSpace(json))
                return Ok(Array.Empty<CarDto>());

            var cars = new List<CarDto>();
            try
            {
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("contentData", out var contentData) ||
                    contentData.ValueKind != JsonValueKind.Array)
                    return Ok(Array.Empty<CarDto>());

                foreach (var e in contentData.EnumerateArray())
                {
                    var dto = new CarDto
                    {
                        CarId = e.TryGetProperty("carId", out var p) && p.TryGetInt32(out var id) ? id : 0,

                        Maker = e.TryGetProperty("maker", out p) ? p.GetString() ?? "" : "",
                        Model = e.TryGetProperty("model", out p) ? p.GetString() ?? "" : "",

                        YearRelease = e.TryGetProperty("yearRelease", out p) && p.TryGetInt32(out var year) ? year : 0,

                        Price = e.TryGetProperty("price", out p) && p.TryGetDecimal(out var price) ? price : 0m,

                        Km = e.TryGetProperty("km", out p) && p.TryGetInt32(out var km) ? km : 0,

                        Fuel = e.TryGetProperty("fuel", out p) ? p.GetString() ?? "" : "",
                        Color = e.TryGetProperty("color", out p) ? p.GetString() ?? "" : "",
                        TransmissionType = e.TryGetProperty("transmissionType", out p) ? p.GetString() ?? "" : "",
                        Offer = e.TryGetProperty("offer", out p) && p.ValueKind == JsonValueKind.False ? false : p.ValueKind == JsonValueKind.True,
                        TypeOfCar = e.TryGetProperty("typeOfCar", out p) ? p.GetString() ?? "" : "",

                        Cc = e.TryGetProperty("cc", out p) && p.TryGetDouble(out var cc) ? cc : 0,
                        Hp = e.TryGetProperty("hp", out p) && p.TryGetDouble(out var hp) ? hp : 0,

                        CarPic = e.TryGetProperty("carPic", out p) ? p.GetString() ?? "" : ""
                    };

                    if (dto.CarId > 0) cars.Add(dto);
                }

                return Ok(cars);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"BlockList JSON parse error: {ex.Message}");
            }
        }    
    
        private static bool IsOfferTrue(string? offer)
        {
            if (string.IsNullOrWhiteSpace(offer)) return false;
            var v = offer.Trim();

            return v.Equals("true", StringComparison.OrdinalIgnoreCase)
                || v.Equals("1")
                || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
    
        private void SyncHomeCarouselOffers(List<CarDto> incomingCars)
        {
            if (HomePageKey == Guid.Empty)
            {
                _logger.LogWarning("HomePageKey is empty. Skipping Home carousel sync.");
                return;
            }

            var home = _contentService.GetById(HomePageKey);
            if (home == null)
            {
                _logger.LogWarning("Home page not found for key {Key}.", HomePageKey);
                return;
            }

            // 🔹 Όλα τα incoming cars indexed by CarId
            var incomingMap = incomingCars
                .Where(c => c.CarId > 0)
                .ToDictionary(c => c.CarId);

            // 🔹 Φόρτωση υπαρχόντων από carouselCars
            var existingHomeCars = LoadExistingCarsFromBlock(home, CarouselBlockPropertyAlias, includeOffer: true);

            // 🔹 Κρατάμε ΜΟΝΟ όσα:
            //   - έχουν carId
            //   - ΚΑΙ το incoming λέει offer == true
            var finalMap = new Dictionary<int, CarDto>();

            int added = 0;
            int updated = 0;
            int removed = 0;

            foreach (var existing in existingHomeCars)
            {
                if (!incomingMap.TryGetValue(existing.CarId, out var incoming))
                {
                    // ❌ δεν υπάρχει πια στο payload → φεύγει
                    removed++;
                    continue;
                }

                if (!incoming.Offer)
                {
                    removed++;
                    continue;
                }

                // ✅ υπάρχει & είναι offer → UPDATE (κρατάμε media)
                incoming.CarPic = existing.CarPic;
                incoming.TenPhotosForUsedCarSales = existing.TenPhotosForUsedCarSales;

                finalMap[incoming.CarId] = incoming;
                updated++;
            }

            // 🔹 Πρόσθεσε νέα offer cars που ΔΕΝ υπήρχαν
            foreach (var incoming in incomingMap.Values)
            {
                if (!incoming.Offer)
                    continue;

                if (finalMap.ContainsKey(incoming.CarId))
                    continue;

                finalMap.Add(incoming.CarId, incoming);
                added++;
            }

            // 🔹 Αν δεν υπάρχει τίποτα να γράψουμε, καθάρισε το carousel
            var finalList = finalMap.Values.ToList();

            ReplaceBlockListWithCarsToAlias(home, CarouselBlockPropertyAlias, finalList);

            _logger.LogInformation(
                "Home carousel synced. Added={Added}, Updated={Updated}, Removed={Removed}, Final={Total}",
                added, updated, removed, finalList.Count
            );
        }

        private List<CarDto> LoadExistingCarsFromBlock(IContent page, string blockAlias, bool includeOffer)
        {
            var result = new List<CarDto>();

            using var cref = _umbracoContextFactory.EnsureUmbracoContext();
            var published = cref.UmbracoContext.Content?.GetById(page.Id);

            if (published == null)
            {
                _logger.LogWarning("LoadExistingCarsFromBlock: no published node for Id={Id}", page.Id);
                return result;
            }

            var blocks = published.Value<BlockListModel>(blockAlias);
            if (blocks == null || !blocks.Any())
                return result;

            foreach (var block in blocks)
            {
                var content = block.Content;
                if (content == null) continue;

                JsonNode? tenPhotosNode = null;
                var tenProp = content.GetProperty("tenPhotosForUsedCarSales");
                var tenSource = tenProp?.GetSourceValue()?.ToString();
                if (!string.IsNullOrWhiteSpace(tenSource))
                    tenPhotosNode = JsonNode.Parse(tenSource);

                var carId = content.Value<int?>("carId") ?? 0;
                if (carId == 0) continue;

                var media = content.Value<IPublishedContent>("carPic");
                var carPicUdi = media != null
                    ? Udi.Create(Constants.UdiEntityType.Media, media.Key).ToString()
                    : (content.Value<string>("carPic") ?? "");

                result.Add(new CarDto
                {
                    CarId = carId,
                    Maker = content.Value<string>("maker") ?? "",
                    Model = content.Value<string>("model") ?? "",
                    YearRelease = content.Value<int?>("yearRelease") ?? 0,
                    Price = content.Value<decimal?>("price") ?? 0,
                    Km = content.Value<int?>("km") ?? 0,
                    Cc = content.Value<double?>("cc") ?? 0,
                    Hp = content.Value<double?>("hp") ?? 0,
                    Fuel = content.Value<string>("fuel") ?? "",
                    Color = content.Value<string>("color") ?? "",
                    TransmissionType = content.Value<string>("transmissionType") ?? "",
                    TypeOfCar = content.Value<string>("typeOfCar") ?? "",
                    Offer = includeOffer ? (content.Value<bool?>("offer") ?? false) : false,
                    CarPic = carPicUdi,
                    TenPhotosForUsedCarSales = tenPhotosNode
                });
            }

            return result;
        }

        private void ReplaceBlockListWithCarsToAlias(IContent page, string blockAlias, List<CarDto> cars)
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

                layoutItems.Add(new Dictionary<string, object?>
                {
                    ["contentUdi"] = elementUdi,
                    ["settingsUdi"] = null
                });

                var makerNormalized = Canonicalize("maker", car.Maker);
                var fuelNormalized = Canonicalize("fuel", car.Fuel);
                var colorNormalized = Canonicalize("color", car.Color);
                var transmissionTypeNormalized = Canonicalize("transmissionType", car.TransmissionType);
                var typeOfCarNormalized = Canonicalize("typeOfCar", car.TypeOfCar);

                Dictionary<string, object?> BuildProps() => new Dictionary<string, object?>
                {
                    ["carId"] = car.CarId,
                    ["maker"] = makerNormalized,
                    ["model"] = car.Model,
                    ["price"] = car.Price,
                    ["yearRelease"] = car.YearRelease,
                    ["km"] = car.Km,
                    ["cc"] = car.Cc,
                    ["hp"] = car.Hp,
                    ["fuel"] = fuelNormalized,
                    ["color"] = colorNormalized,
                    ["transmissionType"] = transmissionTypeNormalized,
                    ["typeOfCar"] = typeOfCarNormalized,
                    ["offer"] = car.Offer,
                    ["carPic"] = car.CarPic,
                    ["tenPhotosForUsedCarSales"] = car.TenPhotosForUsedCarSales
                };

                var elementVaries = cardType.VariesByCulture();

                var content = new Dictionary<string, object?>
                {
                    ["key"] = elementKey,
                    ["udi"] = elementUdi,
                    ["contentTypeKey"] = cardType.Key,
                    ["contentTypeAlias"] = cardType.Alias
                };

                if (elementVaries)
                {
                    var cultures = page.AvailableCultures?.ToArray() ?? Array.Empty<string>();
                    if (cultures.Length == 0)
                    {
                        content["variants"] = new[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["culture"] = null,
                                ["segment"] = null,
                                ["name"] = null,
                                ["properties"] = BuildProps()
                            }
                        };
                    }
                    else
                    {
                        var vars = new List<object>();
                        var props = BuildProps();
                        foreach (var iso in cultures)
                        {
                            vars.Add(new Dictionary<string, object?>
                            {
                                ["culture"] = iso,
                                ["segment"] = null,
                                ["name"] = null,
                                ["properties"] = props
                            });
                        }
                        content["variants"] = vars;
                    }
                }
                else
                {
                    foreach (var kv in BuildProps())
                        content[kv.Key] = kv.Value;
                }

                contentData.Add(content);
            }

            var blockValue = new Dictionary<string, object?>
            {
                ["layout"] = new Dictionary<string, object?>
                {
                    ["Umbraco.BlockList"] = layoutItems
                },
                ["contentData"] = contentData,
                ["settingsData"] = settingsData
            };

            var json = JsonSerializer.Serialize(blockValue);

            var prop = page.Properties[blockAlias];
            var propType = prop?.PropertyType;
            var culturesForNode = page.AvailableCultures?.ToArray() ?? Array.Empty<string>();

            if (propType?.VariesByCulture() == true && culturesForNode.Length > 0)
            {
                foreach (var iso in culturesForNode)
                    page.SetValue(blockAlias, json, iso);

                _contentService.Save(page);
                _contentService.Publish(page, culturesForNode);
            }
            else
            {
                page.SetValue(blockAlias, json);
                _contentService.Save(page);
                _contentService.Publish(page, Array.Empty<string>());
            }
        }
    }

    
    public class CarStockCar
    {
        [JsonPropertyName("carId")]
        public int? CarId { get; set; }

        [JsonPropertyName("maker")]
        public string? Maker { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("yearRelease")]
        public int? YearRelease { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("km")]
        public int? Km { get; set; }

        [JsonPropertyName("cc")]
        public double? Cc { get; set; }
         
        [JsonPropertyName("hp")]
        public double? Hp { get; set; }

        [JsonPropertyName("offer")]
        public bool? Offer { get; set; }

        [JsonPropertyName("fuel")]
        public string? Fuel { get; set; }

        [JsonPropertyName("transmissionType")]
        public string? TransmissionType { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; }

        [JsonPropertyName("typeOfCar")]
        public string? TypeOfCar { get; set; }

        [JsonPropertyName("image_url")]
        public string? ImageUrl { get; set; }
    }

    public class CarDto
    {
        public int CarId { get; set; }
        public string Maker { get; set; } = "";
        public string Model { get; set; } = "";
        public int YearRelease { get; set; }            // FIXED
        public decimal Price { get; set; }              // FIXED
        public int Km { get; set; }                     // FIXED
        public double Cc { get; set; }
        public double Hp { get; set; }
        public string Fuel { get; set; } = "";
        public string TransmissionType { get; set; } = "";
        public string Color { get; set; } = "";
        public bool Offer { get; set; } 
        public string TypeOfCar { get; set; } = "";
        public string CarPic { get; set; } = "";
        public JsonNode? TenPhotosForUsedCarSales { get; set; }

    }
}