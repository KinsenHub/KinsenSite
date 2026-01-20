using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;
//-----------------------------------------------
//Φέρνει δεδομένα για ένα συγκεκριμένο αυτοκίνητο
//Είναι το API για να φορτώνει η σελίδα carDetailsMember τις πληροφορίες του αυτοκινήτου.
//-----------------------------------------------

[Route("umbraco/api/[controller]")]
public class CarApiMemberController : UmbracoApiController
{
    private readonly IUmbracoContextFactory _contextFactory;

    public CarApiMemberController(IUmbracoContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public class CarRequest
    {
        public int Id { get; set; }
    }

    [HttpPost("getcarbyid")]
    public IActionResult GetCarById([FromBody] CarRequest request)
    {
        if (request == null || request.Id <= 0)
            return BadRequest("Invalid request.");

        using var cref = _contextFactory.EnsureUmbracoContext();
        var umb = cref.UmbracoContext;

        var salesPage = umb.Content.GetAtRoot()
            .SelectMany(x => x.DescendantsOrSelf())
            .FirstOrDefault(x => x.ContentType.Alias == "usedCarSalesPage");

        if (salesPage == null) return NotFound("Sales page not found.");

        var carBlocks = salesPage.Value<IEnumerable<BlockListItem>>("carCardBlock");
        var car = carBlocks?.Select(x => x.Content)
            .FirstOrDefault(x => x.Value<int>("carID") == request.Id);

        var gallery = new List<string>();

        var tenPhotosBlocks = car.Value<IEnumerable<BlockListItem>>("TenPhotosForUsedCarSales");

        if (tenPhotosBlocks != null)
        {
            foreach (var block in tenPhotosBlocks)
            {
                var content = block.Content;
                if (content == null) continue;

                for (int i = 1; i <= 10; i++)
                {
                    var img = content.Value<IPublishedContent>($"img{i}");
                    if (img != null)
                    {
                        gallery.Add(img.Url());
                    }
                }
            }
        }

        if (car == null) return NotFound($"Car with ID {request.Id} not found.");

        return Ok(new
        {
            id = request.Id,
            maker = car.Value<string>("maker"),
            model = car.Value<string>("model"),
            price = car.Value<string>("price"),
            year = car.Value<string>("yearRelease"),
            km = car.Value<string>("km"),
            fuel = car.Value<string>("fuel"),
            color = car.Value<string>("color"),
            cc = car.Value<string>("cc"),
            hp = car.Value<string>("hp"),
            transmission = car.Value<string>("transmissionType"),
            typeOfCar = car.Value<string>("typeOfCar"),
            imageUrl = car.Value<IPublishedContent>("carPic")?.Url(),
            gallery = gallery
        });
    }
}
