using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();


builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromHours(2);
    o.Cookie.Name = ".Kinsen.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

builder.Services.Configure<CookieAuthenticationOptions>(
    "UmbracoMembers", 
    options =>
    {
        options.Cookie.Name = "KinsenMemberAuth";
        options.Cookie.HttpOnly = true;

        // Για localhost + fetch
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
    });

// Umbraco
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseStaticFiles();
await app.BootUmbracoAsync();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
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
        u.EndpointRouteBuilder.MapControllers();
    });

await app.RunAsync();
