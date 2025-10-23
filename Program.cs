// using System;
// using Microsoft.AspNetCore.StaticFiles; // 🔹 ΠΡΟΣΟΧΗ: Βάλε αυτό στο top αν δεν υπάρχει
// using KinsenOfficial;

// WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// builder.Services.AddControllers(); // ✅ API controllers

// // 🔹 1) Προσθήκη Session & Cache ΠΡΙΝ το Build()
// builder.Services.AddDistributedMemoryCache(); // in-memory session store
// builder.Services.AddSession(o =>
// {
//     o.IdleTimeout = TimeSpan.FromHours(2);   // π.χ. 2 ώρες
//     o.Cookie.Name = ".Kinsen.Session";
//     o.Cookie.HttpOnly = true;
//     o.Cookie.IsEssential = true;
// });

// builder.CreateUmbracoBuilder()
//     .AddBackOffice()
//     .AddWebsite()
//     .AddComposers()
//     .Build();

// builder.Services.AddControllers().AddJsonOptions(options =>
// {
//     options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
// });

// builder.Services.AddHttpContextAccessor();

// builder.Services.AddHttpClient(); // αφορά το import των αυτοκινήτων στο σύστημα

// // builder.Services.AddUnique<IContentAppFactory, ExportContentAppFactory>();

// WebApplication app = builder.Build();

// var provider = new FileExtensionContentTypeProvider();
// provider.Mappings[".mp4"] = "video/mp4"; // 👈 προσθέτει .mp4

// app.UseStaticFiles(new StaticFileOptions
// {
//     ContentTypeProvider = provider
// });

// await app.BootUmbracoAsync();

// // 🔹 2) Session middleware ΠΡΙΝ το UseUmbraco
// app.UseSession();

// app.UseStaticFiles();

// app.UseUmbraco()
//     .WithMiddleware(u =>
//     {
//         u.UseBackOffice();
//         u.UseWebsite();
//     })
//     .WithEndpoints(u =>
//     {
//         u.UseBackOfficeEndpoints();
//         u.UseWebsiteEndpoints();

//         // κρατάς αυτό για τα [ApiController]
//         u.EndpointRouteBuilder.MapControllers();
//     });

// await app.RunAsync();

using System;
using Microsoft.AspNetCore.StaticFiles;
using KinsenOfficial;

var builder = WebApplication.CreateBuilder(args);

// 1) Controllers (μία φορά) + JSON options
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// 2) Session & Cache (για καλάθι κ.λπ.)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromHours(1);   // διάστημα αδράνειας session
    o.Cookie.Name = ".Kinsen.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    // o.Cookie.SameSite = SameSiteMode.Lax; // προαιρετικό (default Lax)
});

// 3) Umbraco
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient(); // για imports/autos κ.λπ.

var app = builder.Build();

// 4) Static files (με custom content types αν χρειάζεται)
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".mp4"] = "video/mp4";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

// 5) Boot Umbraco
await app.BootUmbracoAsync();

// 6) Pipeline σειρά: Routing → (Auth) → (AuthZ) → Session → Umbraco
app.UseRouting();

// (προαιρετικά — συνήθως τα προσθέτει και το Umbraco, αλλά δεν βλάπτει)
app.UseAuthentication();
app.UseAuthorization();

// ΠΡΟΣΟΧΗ: Session ΠΡΙΝ τα Umbraco endpoints
app.UseSession();

app.UseUmbraco()
   .WithMiddleware(u =>
   {
       u.UseBackOffice();
       u.UseWebsite();
   })
   .WithEndpoints(u =>
   {
       u.UseBackOfficeEndpoints();
       u.UseWebsiteEndpoints();

       // για τα [ApiController]
       u.EndpointRouteBuilder.MapControllers();
   });

await app.RunAsync();