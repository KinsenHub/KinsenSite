using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Web.Common.Controllers;

namespace KinsenOfficial.Controllers
{
    [ApiController]
    [Route("umbraco/api/carstock")]
    [Produces("application/json")]
    public class CarStockApiController : UmbracoApiController
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<CarStockApiController> _logger;

        // In-memory store
        private static readonly ConcurrentDictionary<int, CarDto> _cars = new();
        private static readonly ConcurrentDictionary<string, byte> _eventIds = new();

        public CarStockApiController(IConfiguration cfg, ILogger<CarStockApiController> logger)
        {
            _cfg = cfg;
            _logger = logger;
        }

        // === 1) Receive cars (Postman / carStock server)
        [HttpPost("cars-updated")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Consumes("application/json")]
        public IActionResult CarsUpdated([FromBody] CarStockEnvelope payload)
        {
            var expectedKey = _cfg["CarStock:WebhookSecret"];
            var incomingKey = Request.Headers["X-API-KEY"].ToString();

            if (!string.IsNullOrWhiteSpace(expectedKey) && incomingKey != expectedKey)
                return Unauthorized("Invalid API key");

            if (payload == null)
                return BadRequest("Payload is null");

            if (!string.IsNullOrWhiteSpace(payload.EventId))
            {
                if (!_eventIds.TryAdd(payload.EventId, 1))
                    return Ok(new { ok = true, duplicated = true });
            }

            var incoming = payload.Cars ?? new List<CarStockCar>();
            int added = 0, skipped = 0;

            foreach (var s in incoming)
            {
                if (s?.Id is null || s.Id <= 0)
                {
                    skipped++;
                    continue;
                }

                // Αν υπάρχει ήδη, skip
                if (_cars.ContainsKey(s.Id.Value))
                {
                    skipped++;
                    continue;
                }

                var dto = new CarDto
                {
                    Id = s.Id.Value,
                    Maker = s.Make ?? "",
                    Model = s.Model ?? "",
                    YearRelease = s.YearRelease?.ToString() ?? "",
                    Price = s.Price?.ToString() ?? "",
                    Km = s.Km?.ToString() ?? "",
                    Cc = s.Cc.Value,
                    Hp = s.Hp.Value,
                    TypeOfDiscount = s.TypeOfDiscount ?? "",
                    Fuel = s.Fuel ?? "",
                    TransmissionType = s.TransmissionType ?? "",
                    Color = s.Color ?? "",
                    TypeOfCar = s.TypeOfCar ?? "",
                    CarPicUrl = s.ImageUrl ?? ""
                };

                if (_cars.TryAdd(dto.Id, dto))
                    added++;
            }

            _logger.LogInformation(
                "cars-updated: received {Received}, added {Added}, skipped {Skipped}, total now {Total}",
                incoming.Count, added, skipped, _cars.Count
            );

            return Ok(new
            {
                ok = true,
                received = incoming.Count,
                added,
                skipped,
                total = _cars.Count
            });
        }

        // === 2) List cars ===
        [HttpPost("getcarsStock")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Consumes("application/json")]
        public IActionResult GetCarsStock([FromBody] object? _ = null)
        {
            var list = _cars.Values.OrderBy(c => c.Id).ToList();
            if (list.Count == 0)
                return NoContent();
            return Ok(list);
        }

        // === 3) Get single car ===
        [HttpPost("getcarbyid")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Consumes("application/json")]
        public IActionResult GetCarById([FromBody] IdQuery q)
        {
            if (q == null || q.Id <= 0)
                return BadRequest("Missing or invalid id");

            if (_cars.TryGetValue(q.Id, out var dto))
                return Ok(dto);

            return NotFound(new { message = $"Car with ID {q.Id} not found." });
        }

        // === 4) Health check ===
        [HttpGet("ping")]
        [AllowAnonymous]
        public IActionResult Ping() => Ok(new { ok = true, t = DateTimeOffset.UtcNow });
    }

    // === Models ===

    public class IdQuery
    {
        public int Id { get; set; }
    }

    public class CarStockEnvelope
    {
        [JsonPropertyName("eventId")] public string? EventId { get; set; }
        [JsonPropertyName("timestamp")] public long? Timestamp { get; set; }
        [JsonPropertyName("cars")] public List<CarStockCar>? Cars { get; set; }
    }

    public class CarStockCar
    {
        [JsonPropertyName("id")] public int? Id { get; set; }
        [JsonPropertyName("make")] public string? Make { get; set; }
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("yearRelease")] public int? YearRelease { get; set; }
        [JsonPropertyName("price")] public decimal? Price { get; set; }
        [JsonPropertyName("km")] public int? Km { get; set; }
        [JsonPropertyName("cc")] public float? Cc { get; set; }
        [JsonPropertyName("hp")] public float? Hp { get; set; }
        [JsonPropertyName("typeOfDiscount")] public string? TypeOfDiscount { get; set; }
        [JsonPropertyName("fuel")] public string? Fuel { get; set; }
        [JsonPropertyName("transmissionType")] public string? TransmissionType { get; set; }
        [JsonPropertyName("color")] public string? Color { get; set; }
        [JsonPropertyName("typeOfCar")] public string? TypeOfCar { get; set; }
        [JsonPropertyName("image_url")] public string? ImageUrl { get; set; }
    }

    public class CarDto
    {
        public int Id { get; set; }
        public string Maker { get; set; } = "";
        public string Model { get; set; } = "";
        public string YearRelease { get; set; } = "";
        public string Price { get; set; } = "";
        public string Km { get; set; } = "";
        public float Cc { get; set; }
        public float Hp { get; set; }
        public string TypeOfDiscount { get; set; } = "";
        public string Fuel { get; set; } = "";
        public string TransmissionType { get; set; } = "";
        public string Color { get; set; } = "";
        public string TypeOfCar { get; set; } = "";
        public string CarPicUrl { get; set; } = "";
    }
}
