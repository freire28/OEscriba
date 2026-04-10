using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using MCOEWeb.Data;
using MCOEWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Nginx / proxy reverso no Linux (X-Forwarded-For / X-Forwarded-Proto)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();

// SQL Server — banco OESCRIBA (connection string: ConnectionStrings:OESCRIBA)
builder.Services.AddDbContext<OescribaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OESCRIBA")));

// Cliente HTTP da API Tiny
builder.Services.AddHttpClient<TinyApiClient>();
builder.Services.AddScoped<PedidosSincronizacaoService>();
builder.Services.AddScoped<ProdutosSincronizacaoService>();

// Mercado Livre: tokens (env / appsettings) e renovação automática
builder.Services.AddSingleton<IMercadoLivreTokenStore, MercadoLivreTokenStore>();

// Cliente HTTP da API Mercado Livre (OAuth 2.0)
builder.Services.AddHttpClient<MercadoLivreApiClient>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
