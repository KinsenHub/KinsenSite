using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

[ApiController]
[Route("umbraco/api/[controller]")]
public class FavoritesCustomerController : ControllerBase
{
    private readonly IMemberService _memberService;
    private readonly IMemberManager _memberManager;
    private readonly ILogger<FavoritesCustomerController> _logger;
    private readonly IUmbracoContextAccessor _contextAccessor;

    public FavoritesCustomerController(
        IMemberService memberService,
        IMemberManager memberManager,
        ILogger<FavoritesCustomerController> logger,
        IUmbracoContextAccessor contextAccessor)
    {
        _memberService = memberService;
        _memberManager = memberManager;
        _logger = logger;
        _contextAccessor = contextAccessor;
    }

    // =========================
    // POST: toggle favorite
    // =========================
    [HttpPost("Toggle")]
    public async Task<IActionResult> Toggle([FromBody] FavoriteRequest req)
    {
        _logger.LogInformation("❤️ Toggle favorite called | CarId={CarId}", req?.CarId);

        if (req == null || req.CarId <= 0)
            return BadRequest("Invalid carId");

        // ✅ ΙΔΙΟ AUTH CHECK ΜΕ SaveProfile
        var loggedIn = await _memberManager.GetCurrentMemberAsync();
        if (loggedIn == null)
        {
            _logger.LogWarning("❌ No member logged in");
            return Unauthorized("No member logged in");
        }

        _logger.LogInformation(
            "✅ Logged-in member | Id={Id} | Key={Key} | Username={Username}",
            loggedIn.Id,
            loggedIn.Key,
            loggedIn.UserName
        );

        var member = _memberService.GetByKey(loggedIn.Key);
        if (member == null)
        {
            _logger.LogError("❌ MemberService.GetByKey failed | Key={Key}", loggedIn.Key);
            return Unauthorized("Member not found");
        }

        // 🔹 ΔΙΑΒΑΣΕ favoriteCars
        var raw = member.GetValue<string>("favoriteCars");
        var favorites = string.IsNullOrWhiteSpace(raw)
            ? new List<int>()
            : JsonSerializer.Deserialize<List<int>>(raw) ?? new List<int>();

        _logger.LogInformation(
            "📖 Current favorites | Count={Count} | Cars={Cars}",
            favorites.Count,
            string.Join(",", favorites)
        );

        // 🔹 TOGGLE
        if (favorites.Contains(req.CarId))
        {
            favorites.Remove(req.CarId);
            _logger.LogInformation("➖ Removed CarId={CarId}", req.CarId);
        }
        else
        {
            favorites.Add(req.CarId);
            _logger.LogInformation("➕ Added CarId={CarId}", req.CarId);
        }

        // 🔹 SAVE BACK TO MEMBER
        member.SetValue("favoriteCars", JsonSerializer.Serialize(favorites));
        _memberService.Save(member);

        _logger.LogInformation(
            "💾 Favorites saved | MemberId={MemberId} | FavoritesCount={Count}",
            member.Id,
            favorites.Count
        );

        return Ok(new
        {
            success = true,
            isFavorite = favorites.Contains(req.CarId),
            favorites
        });
    }

    // =========================
    // GET: favorite car IDs
    // =========================
    [HttpGet("GetIds")]
    public async Task<IActionResult> GetFavoriteCarIds()
    {
        _logger.LogInformation("📥 GetFavoriteCarIds called");

        var loggedIn = await _memberManager.GetCurrentMemberAsync();
        if (loggedIn == null)
            return Ok(Array.Empty<object>());

        var member = _memberService.GetByKey(loggedIn.Key);
        if (member == null)
            return Ok(Array.Empty<object>());

        // 🔹 Πάρε τα favorite IDs από το member
        var raw = member.GetValue<string>("favoriteCars");
        var ids = string.IsNullOrWhiteSpace(raw)
            ? new List<int>()
            : JsonSerializer.Deserialize<List<int>>(raw) ?? new List<int>();

        if (!ids.Any())
            return Ok(Array.Empty<int>());

        // 🔹 Umbraco content
        if (!_contextAccessor.TryGetUmbracoContext(out var ctx))
                return StatusCode(500, "Umbraco context not available");

            // 🔹 Βρες τη σελίδα που έχει το blocklist
            var salesPage = ctx.Content
                .GetAtRoot()
                .SelectMany(x => x.DescendantsOrSelf())
                .FirstOrDefault(x => x.ContentType.Alias == "usedCarSalesPage");

            if (salesPage == null)
                return Ok(Array.Empty<object>());

            var blocks = salesPage.Value<BlockListModel>("carCardBlock");
            if (blocks == null || !blocks.Any())
                return Ok(Array.Empty<object>());

            var cars = blocks
                .Select(b => b.Content)
                .Where(c =>
                    c != null &&
                    c.Value<int?>("carID") != null &&
                    ids.Contains(c.Value<int>("carID"))
                )
                .Select(c => new
                {
                    id    = c.Value<int>("carID"),
                    maker = c.Value<string>("maker"),
                    model = c.Value<string>("model"),
                    year  = c.Value<string>("yearRelease"),
                    price = c.Value<string>("price"),
                    img   = c.Value<IPublishedContent>("carPic")?.Url(),
                    url   = $"/carDetailsMember?id={c.Value<int>("carID")}"
                })
                .ToList();

            return Ok(cars);
    }
}

public class FavoriteRequest
{
    public int CarId { get; set; }
}