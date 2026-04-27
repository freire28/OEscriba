using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCOEWeb.Data;
using MCOEWeb.Data.Entities;
using MCOEWeb.Services;

namespace MCOEWeb.Pages;

public class SincronizacaoModel : PageModel
{
    private static readonly JsonSerializerOptions JsonSse = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly PedidosSincronizacaoService _sincPedidos;
    private readonly ProdutosSincronizacaoService _sincProdutos;
    private readonly IConfiguration _configuration;
    private readonly OescribaDbContext _db;

    public SincronizacaoModel(
        PedidosSincronizacaoService sincPedidos,
        ProdutosSincronizacaoService sincProdutos,
        IConfiguration configuration,
        OescribaDbContext db)
    {
        _sincPedidos = sincPedidos;
        _sincProdutos = sincProdutos;
        _configuration = configuration;
        _db = db;
    }

    [BindProperty]
    public DateTime? DataSincronizacao { get; set; }

    [BindProperty]
    public string Token { get; set; } = "";

    [BindProperty]
    public bool SincronizarPedidos { get; set; } = true;

    [BindProperty]
    public bool SincronizarProdutos { get; set; }

    public string? Erro { get; set; }
    public string MensagemStatus { get; set; } = "Aguardando.";
    public SincronizacaoPedidosResultado? UltimoResultadoPedidos { get; set; }
    public ProdutosSincronizacaoResultado? ResultadoProdutos { get; set; }
    public DateTime? UltimaDataSincPedidos { get; set; }
    public DateTime? UltimaDataSincProdutos { get; set; }

    public async Task OnGetAsync()
    {
        Token = FirstNonEmpty(
            _configuration["TinyApi:Token"],
            Environment.GetEnvironmentVariable("TINY_API_TOKEN")) ?? "";

        DataSincronizacao ??= DateTime.Today;
        await CarregarUltimasDatasAsync();
    }

    public async Task OnPostAsync(CancellationToken cancellationToken)
    {
        Erro = null;
        UltimoResultadoPedidos = null;
        ResultadoProdutos = null;
        MensagemStatus = "Processando…";

        var pre = ValidarAntesDeExecutar();
        if (pre is { } errPre)
        {
            Erro = errPre;
            MensagemStatus = "Aguardando.";
            await CarregarUltimasDatasAsync();
            return;
        }

        var marcouPedidos = SincronizarPedidos;
        var marcouProdutos = SincronizarProdutos;

        var progressoUi = new Progress<SincronizacaoProgresso>(p => { MensagemStatus = p.Mensagem; });

        var (pedidos, produtos, erroSinc) = await ExecutarSincronizacaoAsync(
            Token.Trim(),
            DataSincronizacao!.Value,
            marcouPedidos,
            marcouProdutos,
            progressoUi,
            cancellationToken);

        UltimoResultadoPedidos = pedidos;
        ResultadoProdutos = produtos;
        Erro = erroSinc;
        MensagemStatus = erroSinc is not null ? "Erro." : "Concluído.";

        await RegistrarSincronizacaoEhRecarregarDatasAsync(marcouPedidos, marcouProdutos, cancellationToken);
    }

    /// <summary>POST com <c>handler=Sse</c>: envia progresso em tempo real (text/event-stream).</summary>
    public async Task OnPostSseAsync(CancellationToken cancellationToken)
    {
        var pre = ValidarAntesDeExecutar();
        if (pre is { } err)
        {
            Response.ContentType = "application/json; charset=utf-8";
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync(JsonSerializer.Serialize(new { erro = err }, JsonSse), cancellationToken);
            return;
        }

        var marcouPedidos = SincronizarPedidos;
        var marcouProdutos = SincronizarProdutos;

        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers.Append("X-Accel-Buffering", "no");
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var channel = Channel.CreateUnbounded<SincronizacaoProgresso>();
        var progressoUi = new Progress<SincronizacaoProgresso>(p => { channel.Writer.TryWrite(p); });

        async Task BombeiarProgressoAsync()
        {
            await foreach (var p in channel.Reader.ReadAllAsync(cancellationToken))
            {
                var line = JsonSerializer.Serialize(new
                {
                    percentual = p.Percentual,
                    mensagem = p.Mensagem,
                    reiniciarBarra = p.ReiniciarBarra
                }, JsonSse);
                await Response.WriteAsync($"data: {line}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }

        var bomba = BombeiarProgressoAsync();

        SincronizacaoPedidosResultado? pedidos = null;
        ProdutosSincronizacaoResultado? produtos = null;
        string? erroSinc = null;

        try
        {
            (pedidos, produtos, erroSinc) = await ExecutarSincronizacaoAsync(
                Token.Trim(),
                DataSincronizacao!.Value,
                marcouPedidos,
                marcouProdutos,
                progressoUi,
                cancellationToken);
        }
        finally
        {
            channel.Writer.Complete();
            await bomba;
        }

        try
        {
            _db.Sincronizacoes.Add(new Sincronizacao
            {
                DataSincronizacao = DateTime.Now,
                Pedidos = marcouPedidos,
                Produtos = marcouProdutos
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exReg)
        {
            var msg = $"Falha ao gravar registro em SINCRONIZACOES: {exReg.Message}";
            erroSinc = string.IsNullOrEmpty(erroSinc) ? msg : $"{erroSinc} {msg}";
        }

        await CarregarUltimasDatasAsync();

        var final = new
        {
            done = true,
            erro = erroSinc,
            pedidos,
            produtos,
            ultimaDataSincPedidos = UltimaDataSincPedidos,
            ultimaDataSincProdutos = UltimaDataSincProdutos
        };
        var finalJson = JsonSerializer.Serialize(final, JsonSse);
        await Response.WriteAsync($"data: {finalJson}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private string? ValidarAntesDeExecutar()
    {
        if (DataSincronizacao is null)
            return "Informe a data a ser sincronizada.";
        if (string.IsNullOrWhiteSpace(Token))
            return "Informe o token da API Tiny.";
        if (!SincronizarPedidos && !SincronizarProdutos)
            return "Marque ao menos uma opção: Sincronizar pedidos ou Sincronizar produtos.";
        return null;
    }

    private async Task<(SincronizacaoPedidosResultado? Pedidos, ProdutosSincronizacaoResultado? Produtos, string? ErroSinc)> ExecutarSincronizacaoAsync(
        string token,
        DateTime dataSinc,
        bool marcouPedidos,
        bool marcouProdutos,
        IProgress<SincronizacaoProgresso> progressoUi,
        CancellationToken cancellationToken)
    {
        try
        {
            var pedidosEProdutos = marcouPedidos && marcouProdutos;

            IProgress<SincronizacaoProgresso> progressoPedidosEtapa = pedidosEProdutos
                ? new Progress<SincronizacaoProgresso>(p =>
                    progressoUi.Report(new SincronizacaoProgresso((int)(p.Percentual * 0.5m), p.Mensagem, p.ReiniciarBarra)))
                : progressoUi;

            SincronizacaoPedidosResultado? ultimoPedidos = null;
            if (marcouPedidos)
            {
                ultimoPedidos = await _sincPedidos.SincronizarPorDataAsync(
                    token,
                    dataSinc,
                    progressoPedidosEtapa,
                    cancellationToken);
            }

            ProdutosSincronizacaoResultado? resultadoProdutos = null;
            if (marcouProdutos)
            {
                resultadoProdutos = await _sincProdutos.SincronizarDoTinyAsync(
                    token,
                    progressoUi,
                    pedidosEProdutos ? 50 : 0,
                    pedidosEProdutos ? 50 : 100,
                    cancellationToken);
            }

            progressoUi.Report(new SincronizacaoProgresso(100, "Concluído."));
            return (ultimoPedidos, resultadoProdutos, null);
        }
        catch (Exception ex)
        {
            progressoUi.Report(new SincronizacaoProgresso(100, "Erro."));
            return (null, null, ex.Message);
        }
    }

    private async Task RegistrarSincronizacaoEhRecarregarDatasAsync(
        bool marcouPedidos,
        bool marcouProdutos,
        CancellationToken cancellationToken)
    {
        try
        {
            _db.Sincronizacoes.Add(new Sincronizacao
            {
                DataSincronizacao = DateTime.Now,
                Pedidos = marcouPedidos,
                Produtos = marcouProdutos
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exReg)
        {
            var msg = $"Falha ao gravar registro em SINCRONIZACOES: {exReg.Message}";
            Erro = string.IsNullOrEmpty(Erro) ? msg : $"{Erro} {msg}";
        }

        await CarregarUltimasDatasAsync();
    }

    private async Task CarregarUltimasDatasAsync()
    {
        UltimaDataSincPedidos = await _db.Sincronizacoes.AsNoTracking()
            .Where(s => s.Pedidos)
            .OrderByDescending(s => s.DataSincronizacao)
            .Select(s => (DateTime?)s.DataSincronizacao)
            .FirstOrDefaultAsync();

        UltimaDataSincProdutos = await _db.Sincronizacoes.AsNoTracking()
            .Where(s => s.Produtos)
            .OrderByDescending(s => s.DataSincronizacao)
            .Select(s => (DateTime?)s.DataSincronizacao)
            .FirstOrDefaultAsync();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return null;
    }

    public static string FormatarDataUltimaSinc(DateTime? data) =>
        data is { } d ? d.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR")) : "—";
}
