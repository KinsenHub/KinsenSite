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
        private readonly Umbraco.Cms.Core.Web.IUmbracoContextAccessor _accessor;
        private readonly IDataTypeService _dataTypeService;

        // appsettings.json (χρησιμοποίησε ΜΟΝΟ GUID)
        private Guid UsedCarSalesPageKey =>
            Guid.TryParse(_cfg["CarStock:UsedCarSalesPageId"], out var key) ? key : Guid.Empty;

        private string BlockPropertyAlias => _cfg["CarStock:BlockPropertyAlias"] ?? "carCardBlock";
        private string CardElementAlias   => _cfg["CarStock:CardElementAlias"] ?? "carCard";
        private string WebhookSecret      => _cfg["CarStock:WebhookSecret"] ?? "";

        public CarStockWriteController(
            IConfiguration cfg,
            IContentService contentService,
            IContentTypeService contentTypeService,
            IDataTypeService dataTypeService)
        {
             _cfg = cfg;
            _contentService = contentService;
            _contentTypeService = contentTypeService;
            _dataTypeService = dataTypeService;
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
                .Where(c => c?.CarId > 0)
                .Select(s => new CarDto
                {
                    CarId = s.CarId!.Value,
                    Maker = s.Maker ?? "",
                    Model = s.Model ?? "",
                    YearRelease = s.YearRelease?.ToString() ?? "", // αν είναι Text στο element
                    Price = s.Price?.ToString() ?? "",       // αν είναι Text στο element
                    Km = s.Km?.ToString() ?? "",          // αν είναι Text στο element
                    Cc = s.Cc ?? 0,
                    Hp = s.Hp ?? 0,
                    Fuel = s.Fuel ?? "",
                    TransmissionType = s.TransmissionType ?? "",
                    Color = s.Color ?? "",
                    TypeOfDiscount = s.TypeOfDiscount ?? "",
                    TypeOfCar = s.TypeOfCar ?? "",
                    CarPicUrl = s.ImageUrl ?? ""
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
                    "volvo" => "Volvo",
                    "mitsubishi" => "Mitsubishi",
                    "bmw" => "BMW",
                    "honda" => "Honda",
                    "maserati" => "Maserati",
                    _ => incoming ?? ""
                };
            }

            // fuel
            if (propertyAlias == "fuel")
            {
                return f switch
                {
                    "βενζινη" or "benzini" or "petrol" => "Βενζίνη",
                    "πετρελαιο" or "petrelaio" or "diesel" => "Πετρέλαιο",
                    "υβριδικο" or "hybrid" => "Υβριδικό",
                    "ηλεκτρικο" or "ilektriko" or "electric" => "Ηλεκτρικό",
                    "αεριο" or "lpg" or "cng" => "Αέριο",
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

            // color – βάλε τα ακριβή labels του dropdown σου
            if (propertyAlias == "color")
            {
                return f switch
                {
                    "μαυρο" or "black" => "Μαύρο",
                    "γκρι" or "grey" or "gray" => "Γκρι",
                    "ασπρο" or "λευκο" or "white" => "Άσπρο",
                    "κοκκινο" or "red" => "Κόκκινο",
                    "μπλε" or "blue" => "Μπλε",
                    "ασημι" or "silver" => "Ασημί",
                    _ => incoming ?? ""
                };
            }

            // typeOfCar – προσαρμόσου στα labels του DataType σου
            if (propertyAlias == "typeOfCar")
            {
                return f switch
                {
                    "sedan" => "Sedan",
                    "πολης" or "city" => "Πόλης",
                    "suv" => "SUV",
                    "hatchback" => "Hatchback",
                    "coupe" => "Coupe",
                    "station wagon" or "wagon" or "break" => "Station Wagon",
                    _ => incoming ?? ""
                };
            }

            return incoming ?? "";
        }
        
        
        private string MapDropdownValue(IContentType elementType, string propertyAlias, string? incoming)
        {
            if (string.IsNullOrWhiteSpace(incoming))
                return string.Empty;

            var propType = elementType.CompositionPropertyTypes.FirstOrDefault(p => p.Alias == propertyAlias);
            if (propType == null)
                return incoming;

            var dt = _dataTypeService.GetDataType(propType.DataTypeId); // int, όχι Guid
            if (dt == null)
                return incoming;

            bool useKeys = false;
            var candidates = new List<(Guid Id, string? Value)>();

            // ---- 1) ConfigurationEditorJson (Umbraco 14–15) ----
            var confJsonProp = dt.GetType().GetProperty("ConfigurationEditorJson");
            if (confJsonProp?.GetValue(dt) is string json && !string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("items", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var el in arr.EnumerateArray())
                        {
                            Guid id = Guid.Empty;
                            if (el.TryGetProperty("id", out var jId) && jId.ValueKind == System.Text.Json.JsonValueKind.String)
                                Guid.TryParse(jId.GetString(), out id);

                            string? val = el.TryGetProperty("value", out var jVal) ? jVal.GetString() : null;
                            candidates.Add((id, val));
                        }
                    }
                    if (root.TryGetProperty("useKeys", out var uk))
                        useKeys = uk.ValueKind == System.Text.Json.JsonValueKind.True;
                }
                catch { /* ignore */ }
            }

            // ---- 2) ConfigurationData (dictionary) ----
            if (candidates.Count == 0)
            {
                var confDataProp = dt.GetType().GetProperty("ConfigurationData");
                if (confDataProp?.GetValue(dt) is IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue("items", out var itemsObj) && itemsObj is System.Collections.IEnumerable list)
                    {
                        foreach (var o in list)
                        {
                            try
                            {
                                var s = System.Text.Json.JsonSerializer.Serialize(o);
                                using var ed = System.Text.Json.JsonDocument.Parse(s);
                                var el = ed.RootElement;

                                Guid id = Guid.Empty;
                                if (el.TryGetProperty("id", out var jId) && jId.ValueKind == System.Text.Json.JsonValueKind.String)
                                    Guid.TryParse(jId.GetString(), out id);

                                string? val = el.TryGetProperty("value", out var jVal) ? jVal.GetString() : null;
                                candidates.Add((id, val));
                            }
                            catch { }
                        }
                    }
                    if (dict.TryGetValue("useKeys", out var ukObj) && ukObj is bool b) useKeys = b;
                }
            }

            // ---- 3) Παλαιότερο "Configuration" με Items/UseKeys ----
            if (candidates.Count == 0)
            {
                var cfgObj = dt.GetType().GetProperty("Configuration")?.GetValue(dt);
                if (cfgObj != null)
                {
                    var itemsProp = cfgObj.GetType().GetProperty("Items");
                    var ukProp    = cfgObj.GetType().GetProperty("UseKeys");
                    if (ukProp?.GetValue(cfgObj) is bool b) useKeys = b;

                    if (itemsProp?.GetValue(cfgObj) is System.Collections.IEnumerable itemsEnum)
                    {
                        foreach (var it in itemsEnum)
                        {
                            Guid id = Guid.Empty;
                            string? val = null;

                            var idProp  = it.GetType().GetProperty("Id");
                            var valProp = it.GetType().GetProperty("Value");

                            var idObj = idProp?.GetValue(it);
                            if (idObj is Guid g) id = g;
                            else if (idObj is string s && Guid.TryParse(s, out var g2)) id = g2;

                            val = valProp?.GetValue(it)?.ToString();
                            candidates.Add((id, val));
                        }
                    }
                }
            }

            if (candidates.Count == 0)
                return incoming;

            // --- Normalization & ranking ---
            static string Norm(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return "";
                var t = s.Trim().Replace('\u00A0',' ').ToLowerInvariant();
                // βγάλε παρενθέσεις στο τέλος: "Honda (Legacy option)" -> "honda"
                t = System.Text.RegularExpressions.Regex.Replace(t, @"\s*\(.*?\)\s*$", "");
                t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ");
                return t;
            }

            string incomingExact = incoming.Trim();
            string incomingNorm  = Norm(incoming);

            int Score((Guid Id, string? Value) c)
            {
                var label = (c.Value ?? "").Trim();
                var labelNorm = Norm(label);

                // 0: exact (case-insensitive)
                if (string.Equals(label, incomingExact, StringComparison.OrdinalIgnoreCase)) return 0;

                // 1: normalized equal
                if (labelNorm == incomingNorm) return 1;

                // 2: normalized starts-with (π.χ. "honda (europe)")
                if (labelNorm.StartsWith(incomingNorm) || incomingNorm.StartsWith(labelNorm)) return 2;

                return 100; // no match
            }

            int Penalty((Guid Id, string? Value) c)
            {
                var l = (c.Value ?? "").ToLowerInvariant();
                int p = 0;
                if (l.Contains("legacy"))      p += 50;
                if (l.Contains("deprecated"))  p += 50;
                if (l.Contains("unsupported")) p += 50;
                return p;
            }

            var best = candidates
                .Select(c => new { c.Id, c.Value, score = Score(c) + Penalty(c) })
                .OrderBy(x => x.score)
                .ThenBy(x => (x.Value ?? "").Length) // πιο “καθαρά” labels πρώτα
                .FirstOrDefault();

            if (best == null || best.score >= 100)
                return incoming; // δεν βρέθηκε τίποτα σχετικό

            return useKeys
                ? (best.Id != Guid.Empty ? best.Id.ToString() : incoming)
                : (best.Value ?? incoming);
        }


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

                // Layout
                layoutItems.Add(new Dictionary<string, object?>
                {
                    ["contentUdi"]  = elementUdi,
                    ["settingsUdi"] = null
                });

                // --- Canonicalize + mapping (ΚΡΑΤΑΣ ακριβώς ό,τι ήδη κάνεις για makerRaw κ.λπ.) ---
                var makerRaw        = MapDropdownValue(cardType, "maker",            Canonicalize("maker",            car.Maker));
                var fuelRaw         = MapDropdownValue(cardType, "fuel",             Canonicalize("fuel",             car.Fuel));
                var transmissionRaw = MapDropdownValue(cardType, "transmissionType", Canonicalize("transmissionType", car.TransmissionType));
                var colorRaw        = MapDropdownValue(cardType, "color",            Canonicalize("color",            car.Color));
                var typeOfCarRaw    = MapDropdownValue(cardType, "typeOfCar",        Canonicalize("typeOfCar",        car.TypeOfCar));

                // --- αυστηρό guard για useKeys=true ---
                string? GuardUseKeys(string propAlias, string? raw)
                {
                    if (string.IsNullOrWhiteSpace(raw)) return null;
                    var ptype = cardType.CompositionPropertyTypes.FirstOrDefault(p => p.Alias == propAlias);
                    if (ptype == null) return null;
                    var dt = _dataTypeService.GetDataType(ptype.DataTypeId);
                    if (dt == null) return null;

                    var useKeys = false;
                    var confJsonProp = dt.GetType().GetProperty("ConfigurationEditorJson");
                    if (confJsonProp?.GetValue(dt) is string conf && !string.IsNullOrWhiteSpace(conf))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(conf);
                            if (doc.RootElement.TryGetProperty("useKeys", out var uk))
                                useKeys = uk.ValueKind == System.Text.Json.JsonValueKind.True;
                        }
                        catch { }
                    }

                    if (!useKeys) return raw;
                    return Guid.TryParse(raw, out _) ? raw : null; // ΜΟΝΟ GUID αν useKeys=true
                }

                makerRaw        = GuardUseKeys("maker",            makerRaw);
                fuelRaw         = GuardUseKeys("fuel",             fuelRaw);
                transmissionRaw = GuardUseKeys("transmissionType", transmissionRaw);
                colorRaw        = GuardUseKeys("color",            colorRaw);
                typeOfCarRaw    = GuardUseKeys("typeOfCar",        typeOfCarRaw);

                // --- helper για να χτίσω τα properties του element (ένας Dictionary με ΤΙΠΟΥΣ σωστούς) ---
                Dictionary<string, object?> BuildProps()
                {
                    var d = new Dictionary<string, object?>
                    {
                        ["carId"]          = car.CarId,
                        ["model"]          = car.Model,
                        ["price"]          = decimal.TryParse(car.Price, out var dPrice) ? dPrice : 0m,
                        ["yearRelease"]    = int.TryParse(car.YearRelease, out var y) ? y : 0,
                        ["km"]             = int.TryParse(car.Km, out var k) ? k : 0,
                        ["cc"]             = car.Cc,
                        ["hp"]             = car.Hp,
                        ["typeOfDiscount"] = car.TypeOfDiscount,
                        ["carPicUrl"]      = car.CarPicUrl
                    };

                    if (!string.IsNullOrEmpty(makerRaw))        d["maker"]            = makerRaw;
                    if (!string.IsNullOrEmpty(fuelRaw))         d["fuel"]             = fuelRaw;
                    if (!string.IsNullOrEmpty(transmissionRaw)) d["transmissionType"] = transmissionRaw;
                    if (!string.IsNullOrEmpty(colorRaw))        d["color"]            = colorRaw;
                    if (!string.IsNullOrEmpty(typeOfCarRaw))    d["typeOfCar"]        = typeOfCarRaw;

                    return d;
                }

                // --- ΤΟ ΚΡΙΣΙΜΟ: αν το element type είναι Vary by culture, γράφουμε ΜΟΝΟ μέσα σε "variants" ---
                var elementVaries = cardType.VariesByCulture(); // μέθοδος στη v15
                var content = new Dictionary<string, object?>
                {
                    ["key"]               = elementKey,
                    ["udi"]               = elementUdi,
                    ["contentTypeKey"]    = cardType.Key,
                    ["contentTypeAlias"]  = cardType.Alias
                };

                if (elementVaries)
                {
                    // πάρ’ τα cultures από το node
                    var cultures = page.AvailableCultures?.ToArray() ?? Array.Empty<string>();
                    if (cultures.Length == 0)
                    {
                        // fallback: γράψε invariant variants (Umbraco το δέχεται)
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
                        // γράψε τις ίδιες τιμές σε ΟΛΕΣ τις γλώσσες
                        var vars = new List<object>();
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
                    // invariant element → γράφουμε επίπεδα properties στο root (όπως ήδη έκανες)
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

            blockValue = new Dictionary<string, object?>
            {
                ["layout"] = new Dictionary<string, object?>
                {
                    ["Umbraco.BlockList"] = layoutItems
                },
                ["contentData"]  = contentData,
                ["settingsData"] = settingsData
            };

            var json = System.Text.Json.JsonSerializer.Serialize(blockValue);

            // property + cultures του node
            var prop     = page.Properties[BlockPropertyAlias];
            var propType = prop?.PropertyType;
            var culturesForNode = page.AvailableCultures?.ToArray() ?? Array.Empty<string>();

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
                        CarId = e.TryGetProperty("carId", out var vId) && vId.TryGetInt32(out var id) ? id : 0,
                        Maker = e.TryGetProperty("maker", out var v) ? v.GetString() ?? "" : "",
                        Model = e.TryGetProperty("model", out v) ? v.GetString() ?? "" : "",
                        Price = e.TryGetProperty("price", out v) ? v.ToString() : "",
                        YearRelease = e.TryGetProperty("yearRelease", out v) ? v.ToString() : "",
                        Km = e.TryGetProperty("km", out v) ? v.ToString() : "",
                        Fuel = e.TryGetProperty("fuel", out v) ? v.GetString() ?? "" : "",
                        Color = e.TryGetProperty("color", out v) ? v.GetString() ?? "" : "",
                        TransmissionType = e.TryGetProperty("transmissionType", out v) ? v.GetString() ?? "" : "",
                        TypeOfDiscount = e.TryGetProperty("typeOfDiscount", out v) ? v.GetString() ?? "" : "",
                        TypeOfCar = e.TryGetProperty("typeOfCar", out v) ? v.GetString() ?? "" : "",
                        Cc = e.TryGetProperty("cc", out v) && v.TryGetSingle(out var cc) ? cc : 0,
                        Hp = e.TryGetProperty("hp", out v) && v.TryGetSingle(out var hp) ? hp : 0,
                        CarPicUrl = e.TryGetProperty("carPicUrl", out v) ? v.GetString() ?? "" : ""
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
        
    }

    // ====== Payload models ======
    public class CarStockEnvelope
    {
        [JsonPropertyName("cars")] public List<CarStockCar>? Cars { get; set; }
    }

    public class CarStockCar
    {
        [JsonPropertyName("carId")]               public int? CarId { get; set; }
        [JsonPropertyName("maker")]             public string? Maker { get; set; }
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
        public int    CarId { get; set; }
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
