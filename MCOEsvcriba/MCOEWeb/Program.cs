using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using MCOEWeb.Data;
using MCOEWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRazorPages();

builder.Services.AddDbContext<OescribaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OESCRIBA")));

builder.Services.AddHttpClient<TinyApiClient>();
builder.Services.AddScoped<PedidosSincronizacaoService>();
builder.Services.AddScoped<ProdutosSincronizacaoService>();
builder.Services.AddScoped<PedidosConsolidadoService>();

builder.Services.AddSingleton<IMercadoLivreTokenStore, MercadoLivreTokenStore>();
builder.Services.AddHttpClient<MercadoLivreApiClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Em Development, o perfil "http" só expõe HTTP; UseHttpsRedirection redireciona para HTTPS
// e o site parece "não abrir" (conexão recusada). Em produção mantém o redirecionamento.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
