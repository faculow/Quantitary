using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Quantitary.Models;
using Quantitary.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(BD.ObtenerCadenaConexion(builder.Configuration)));

builder.Services.AddScoped<ProductoRepository>();

var gemini = builder.Configuration.GetSection("Gemini").Get<GeminiOptions>() ?? new GeminiOptions();
gemini.ApiKey = builder.Configuration["GEMINI_API_KEY"]
    ?? builder.Configuration["Gemini:ApiKey"]
    ?? string.Empty;

builder.Services.AddSingleton(gemini);
builder.Services.AddScoped<ComercioDatosSql>();
builder.Services.AddScoped<IAHistorialSql>();
builder.Services.AddScoped<IAStockSql>();
builder.Services.AddScoped<IAProductoSql>();
builder.Services.AddHttpClient<IAiChatService, GeminiChatService>(client =>
    client.Timeout = TimeSpan.FromSeconds(60));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("quantitary-ia", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Session.GetInt32("UsuarioID")?.ToString()
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

var app = builder.Build();

BD.GarantizarEsquemaActual(builder.Configuration);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Landing}/{id?}");

app.Run();


