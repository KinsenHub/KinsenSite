using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Extensions;

namespace Kinsen.Web.Api
{
    [Route("umbraco/api/favorites")]
    public class FavoritesApiController : UmbracoApiController
    {
        private const string SESSION_KEY = "FAVORITE_CARS";
        private const string BLOCK_ALIAS = "carCardBlock";

        private readonly IUmbracoContextAccessor _contextAccessor;

        public FavoritesApiController(IUmbracoContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
        }

        // =========================
        // POST: toggle favorite
        // =========================
        [HttpPost("toggle")]
        public IActionResult Toggle([FromBody] FavoriteRequest req)
        {
            if (req == null || req.CarId <= 0)
                return BadRequest();

            var favorites = GetFavorites();

            if (favorites.Contains(req.CarId))
                favorites.Remove(req.CarId);
            else
                favorites.Add(req.CarId);

            SaveFavorites(favorites);

            return Ok(new
            {
                isFavorite = favorites.Contains(req.CarId)
            });
        }

        // =========================
        // GET: all favorites
        // =========================
        [HttpGet("get")]
        public IActionResult Get()
        {
            var ids = GetFavorites();
            if (!ids.Any())
                return Ok(Array.Empty<object>());

            if (!_contextAccessor.TryGetUmbracoContext(out var ctx))
                return StatusCode(500, "Umbraco context not available");

            // 🔹 Βρες τη σελίδα που έχει το blocklist
            var salesPage = ctx.Content
                .GetAtRoot()
                .SelectMany(x => x.DescendantsOrSelf())
                .FirstOrDefault(x => x.ContentType.Alias == "usedCarSalesPage");

            if (salesPage == null)
                return Ok(Array.Empty<object>());

            var blocks = salesPage.Value<BlockListModel>(BLOCK_ALIAS);
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


        [HttpGet("ids")]
        public IActionResult GetIds()
        {
            var ids = GetFavorites();
            return Ok(ids);
        }
        
        // =========================
        // helpers
        // =========================
        private List<int> GetFavorites()
        {
            return HttpContext.Session.GetObject<List<int>>(SESSION_KEY)
                   ?? new List<int>();
        }

        private void SaveFavorites(List<int> list)
        {
            HttpContext.Session.SetObject(SESSION_KEY, list);
        }
    }

    // =========================
    // DTO
    // =========================
    public class FavoriteRequest
    {
        public int CarId { get; set; }
    }

    // =========================
    // Session Extensions
    // =========================
    public static class SessionExtensions
    {
        public static void SetObject<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        public static T? GetObject<T>(this ISession session, string key)
        {
            var json = session.GetString(key);
            return json == null
                ? default
                : JsonSerializer.Deserialize<T>(json);
        }
    }
}
