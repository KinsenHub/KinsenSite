using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Core.Web;
using System.Linq;

namespace KinsenOfficial.Controllers
{
    [Route("umbraco/api/[controller]/[action]")]
    [Authorize]
    public class CarExportController : UmbracoApiController
    {
        private readonly IUmbracoContextAccessor _ctx;

        public CarExportController(IUmbracoContextAccessor ctx)
        {
            _ctx = ctx;
        }

        [HttpGet]
        public IActionResult ExportCars()
        {
            if (!_ctx.TryGetUmbracoContext(out var umbCtx))
                return NotFound();

            var root = umbCtx.Content?.GetAtRoot().FirstOrDefault();
            var carsPage = root?.Children.FirstOrDefault(x => x.ContentType.Alias == "carsPage");

            if (carsPage == null)
                return NotFound();

            var cars = carsPage.ChildrenOfType("carPage");

            var sb = new StringBuilder();
            sb.AppendLine("Title,Brand,Price");

            foreach (var car in cars)
            {
                var title = car.Name;
                var brand = car.Value<string>("brand");
                var price = car.Value<decimal>("price");

                sb.AppendLine($"{title},{brand},{price}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "cars_export.csv");
        }
    }
}
