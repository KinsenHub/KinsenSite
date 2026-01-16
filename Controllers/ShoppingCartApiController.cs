using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using Umbraco.Cms.Web.Common.Controllers;

namespace Kinsen.Web.Api 
{
    [ApiController]
    [Route("umbraco/api/[controller]")] // -> /umbraco/api/cart/*
    public class CartController : UmbracoApiController
    {
        private const string CART_SESSION_KEY = "Kinsen.Cart";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private List<CartItem> GetCart()
        {
            var json = HttpContext.Session.GetString(CART_SESSION_KEY);
            return string.IsNullOrEmpty(json)
                ? new List<CartItem>()
                : (JsonSerializer.Deserialize<List<CartItem>>(json, JsonOpts) ?? new List<CartItem>());
        }

        private void SaveCart(List<CartItem> cart)
            => HttpContext.Session.SetString(CART_SESSION_KEY, JsonSerializer.Serialize(cart, JsonOpts));

        // --- quick test ---
        [HttpGet("ping")]
        public IActionResult Ping() => Ok(new { ok = true, t = DateTimeOffset.UtcNow });

        [HttpGet("get")]
        public IActionResult Get() => Ok(GetCart());

        [HttpGet("count")]
        public IActionResult Count() => Ok(new { count = GetCart().Count });

        // [HttpPost("add")]
        // public IActionResult Add([FromBody] CartItem item)
        // {
        //     if (item == null || string.IsNullOrWhiteSpace(item.Id))
        //         return BadRequest("Invalid item");

        //     item.Maker   = (item.Maker ?? item.Make ?? "").Trim();
        //     item.Make    = null; // legacy
        //     item.Model   = (item.Model ?? "").Trim();
        //     item.AddedAt = item.AddedAt == 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : item.AddedAt;

        //     var cart = GetCart();
        //     var idx = cart.FindIndex(x => x.Id == item.Id);
        //     if (idx == -1) cart.Add(item); else cart[idx] = item;

        //     SaveCart(cart);
        //     return Ok(new { count = cart.Count, items = cart });
        // }

        [HttpPost("add")]
        public IActionResult Add([FromBody] CartItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
                return BadRequest("Invalid item");

            item.Maker   = (item.Maker ?? item.Make ?? "").Trim();
            item.Make    = null; // legacy
            item.Model   = (item.Model ?? "").Trim();
            item.AddedAt = item.AddedAt == 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : item.AddedAt;

            var cart = GetCart();

            // ⬇️ ΝΕΟ: έλεγχος διπλοεγγραφής
            if (cart.Any(x => x.Id == item.Id))
            {
                return Conflict(new
                {
                    code = "DUPLICATE",
                    message = "Το προϊόν υπάρχει ήδη στο καλάθι.",
                    count = cart.Count,
                    items = cart
                });
            }

            // αλλιώς πρόσθεσέ το
            cart.Add(item);
            SaveCart(cart);

            return Ok(new { count = cart.Count, items = cart });
        }

        [HttpPost("remove")]
        public IActionResult Remove([FromBody] IdReq req)
        {
            if (string.IsNullOrWhiteSpace(req?.Id)) return BadRequest();
            var cart = GetCart().Where(x => x.Id != req.Id).ToList();
            SaveCart(cart);
            return Ok(new { count = cart.Count, items = cart });
        }

        [HttpPost("clear")]
        public IActionResult Clear()
        {
            SaveCart(new List<CartItem>());
            return Ok(new { count = 0 });
        }

        [HttpGet("contains")]
        public IActionResult Contains([FromQuery] string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Missing id");

            var cart = GetCart();
            var exists = cart.Any(x => x.Id == id);
            return Ok(new { contains = exists, count = cart.Count });
        }
    }

    public class CartItem
    {
        public string Id { get; set; } = "";
        public string? Maker { get; set; }   // σωστό πεδίο
        public string? Make  { get; set; }   // legacy input
        public string? Model { get; set; }
        public string? Title { get; set; }
        public string? PriceText { get; set; }  // π.χ. "15.000"
        public int?    PriceValue { get; set; } // π.χ. 15000
        public string? Img { get; set; }
        public string? Url { get; set; }
        public int?    Year { get; set; }
        public int?    Km { get; set; }
        public string? Fuel { get; set; }
        public int?    Cc { get; set; }
        public int?    Hp { get; set; }
        public string? Color { get; set; }
        public long    AddedAt { get; set; }
    }

    public class IdReq { public string Id { get; set; } = ""; }
}