using System;
using Microsoft.AspNetCore.StaticFiles; // 🔹 ΠΡΟΣΟΧΗ: Βάλε αυτό στο top αν δεν υπάρχει
using KinsenOfficial;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(); // ✅ API controllers

// 🔹 1) Προσθήκη Session & Cache ΠΡΙΝ το Build()
builder.Services.AddDistributedMemoryCache(); // in-memory session store
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromHours(2);   // π.χ. 2 ώρες
    o.Cookie.Name = ".Kinsen.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient(); // αφορά το import των αυτοκινήτων στο σύστημα

// builder.Services.AddUnique<IContentAppFactory, ExportContentAppFactory>();

WebApplication app = builder.Build();

var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".mp4"] = "video/mp4"; // 👈 προσθέτει .mp4

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

await app.BootUmbracoAsync();

// 🔹 2) Session middleware ΠΡΙΝ το UseUmbraco
app.UseSession();

app.UseStaticFiles();

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

        // κρατάς αυτό για τα [ApiController]
        u.EndpointRouteBuilder.MapControllers();
    });

await app.RunAsync();

