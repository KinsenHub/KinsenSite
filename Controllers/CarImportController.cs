// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.Extensions.Logging;
// using System.Text;
// using Umbraco.Cms.Core;
// using Umbraco.Cms.Core.Models;
// using Umbraco.Cms.Core.Models.Blocks;
// using Umbraco.Cms.Core.PropertyEditors;
// using Umbraco.Cms.Core.Services;
// using Umbraco.Cms.Core.Web;
// using Umbraco.Cms.Web.Common.Controllers;
// using Umbraco.Extensions;
// using Umbraco.Cms.Core.PropertyEditors.ValueConverters;

// namespace KinsenOfficial.Controllers
// {
//     [Route("umbraco/api/carimport")]
//     public class CarImportApiController : UmbracoApiController
//     {
//         private readonly ILogger<CarImportApiController> _logger;
//         private readonly IContentService _contentService;
//         private readonly IContentTypeService _contentTypeService;
//         private readonly IUmbracoContextFactory _contextFactory;
//         private readonly BlockEditorConverter _blockEditorConverter;

//         public CarImportApiController(
//             ILogger<CarImportApiController> logger,
//             IContentService contentService,
//             IContentTypeService contentTypeService,
//             IUmbracoContextFactory contextFactory,
//             BlockEditorConverter blockEditorConverter)
//         {
//             _logger = logger;
//             _contentService = contentService;
//             _contentTypeService = contentTypeService;
//             _contextFactory = contextFactory;
//             _blockEditorConverter = blockEditorConverter;
//         }

//         [HttpPost("upload")]
//         public IActionResult ImportCsv(IFormFile csvFile)
//         {
//             if (csvFile == null || csvFile.Length == 0)
//                 return BadRequest("❌ Δεν επιλέχθηκε αρχείο CSV.");

//             var csvRows = new List<string[]>();

//             using var reader = new StreamReader(csvFile.OpenReadStream(), Encoding.UTF8);
//             var header = reader.ReadLine();
//             if (header == null)
//                 return BadRequest("❌ Το αρχείο δεν έχει επικεφαλίδα.");

//             var columns = header.Split(';');
//             if (columns.Length < 13)
//                 return BadRequest("❌ Το CSV πρέπει να έχει τουλάχιστον 13 στήλες.");

//             while (!reader.EndOfStream)
//             {
//                 var line = reader.ReadLine();
//                 if (string.IsNullOrWhiteSpace(line)) continue;

//                 var values = line.Split(';');
//                 if (values.Length < 13) continue;

//                 csvRows.Add(values);
//             }

//             if (csvRows.Count == 0)
//                 return BadRequest("⚠️ Το CSV δεν περιέχει έγκυρες γραμμές.");

//             var elementType = _contentTypeService.Get("carCard");
//             if (elementType == null)
//                 return BadRequest("❌ Δεν βρέθηκε το element type 'carCard'.");

//             var blockItemDataList = new List<BlockItemData>();

//             foreach (var values in csvRows)
//             {
//                 var props = new Dictionary<string, object?>
//                 {
//                     ["carID"] = values[0].Trim(),
//                     ["maker"] = values[1].Trim(),
//                     ["model"] = values[2].Trim(),
//                     ["yearRelease"] = values[3].Trim(),
//                     ["price"] = values[4].Trim(),
//                     ["km"] = values[5].Trim(),
//                     ["cc"] = values[6].Trim(),
//                     ["hp"] = values[7].Trim(),
//                     ["fuel"] = values[8].Trim(),
//                     ["transmissionType"] = values[9].Trim(),
//                     ["color"] = values[10].Trim(),
//                     ["typeOfCar"] = values[11].Trim(),
//                     ["typeOfDiscount"] = values[12].Trim()
//                 };

//                 var blockItem = new BlockItemData
//                 {
//                     ContentTypeKey = elementType.Key,
//                     Udi = Udi.Create(Constants.UdiEntityType.Element, Guid.NewGuid()),
//                     RawPropertyValues = props
//                 };

//                 blockItemDataList.Add(blockItem);
//             }

//             var blockListItems = blockItemDataList
//                 .Select(BlockListItem.CreateElement)
//                 .ToList();

//             var blockListModel = new BlockListModel(blockListItems);
            
//             // ✏️ Βρες τη usedCarSalesPage
//             using var contextRef = _contextFactory.EnsureUmbracoContext();
//             var root = contextRef.UmbracoContext.Content.GetAtRoot().FirstOrDefault();
//             var page = root?.DescendantsOrSelf().FirstOrDefault(x => x.ContentType.Alias == "usedCarSalesPage");

//             if (page == null)
//                 return NotFound("❌ Δεν βρέθηκε η σελίδα usedCarSalesPage.");

//             var pageContent = _contentService.GetById(page.Id);
//             if (pageContent == null)
//                 return NotFound("❌ Δεν φορτώθηκε το IContent της σελίδας.");

//             // ✅ Αντικατάσταση του block list
//             pageContent.SetValue("carCardBlock", blockListModel);
//             _contentService.Save(pageContent);
//             _contentService.Publish(pageContent, pageContent.AvailableCultures.ToArray());

//             return Ok($"✅ Εισήχθησαν {blockItemDataList.Count} νέα αυτοκίνητα στο carCardBlock.");
//         }
//     }
// }
