using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using WebUI.Configuration;
using WebUI.Extensions;
using WebUI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12);
    options.Cookie.Name = ".GoBike.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddGoBikeApiClient(builder.Configuration, builder.Environment);
builder.Services.Configure<MapTilerSettings>(
    builder.Configuration.GetSection(MapTilerSettings.SectionName));

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Motorcycle", "AdminOrStaff");
    options.Conventions.AuthorizeFolder("/Customers", "AdminOrStaff");
    options.Conventions.AuthorizeFolder("/Rentals", "AdminOrStaff");
    options.Conventions.AuthorizeFolder("/Routes", "AdminOrStaff");
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("StaffOnly", policy => policy.RequireRole("Staff", "Admin"));
    options.AddPolicy("AdminOrStaff", policy => policy.RequireRole("Admin", "Staff"));
});

builder.Services.AddHttpClient("GobikeApi", client =>
{
    var apiSettings = builder.Configuration.GetSection(WebUI.Configuration.ApiSettings.SectionName);
    var baseUrl = apiSettings["BaseUrl"] ?? "https://localhost:7144";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddHttpMessageHandler<ApiAuthCookieHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/Index"));

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
