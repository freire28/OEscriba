using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCOEWeb.Services;

namespace MCOEWeb.Pages;

public class PedidosModel : PageModel
{
    private readonly PedidosConsolidadoService _pedidos;

    public PedidosModel(PedidosConsolidadoService pedidos) => _pedidos = pedidos;

    [BindProperty(SupportsGet = true)]
    public string? Buscar { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? Di { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? Df { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Uf { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Cliente { get; set; }

    [BindProperty(SupportsGet = true)]
    public int P { get; set; } = 1;

    /// <summary>g = grid, c = cards</summary>
    [BindProperty(SupportsGet = true)]
    public string V { get; set; } = "g";

    public List<LinhaConsolidadoMc> Linhas { get; private set; } = new();
    public int TotalRegistros { get; private set; }
    /// <summary>Totais do filtro atual (todos os registros, não só a página).</summary>
    public decimal TotalValorVenda { get; private set; }
    public decimal TotalMc { get; private set; }
    public string? Erro { get; private set; }
    public bool ModoCards => string.Equals(V, "c", StringComparison.OrdinalIgnoreCase);

    public int TotalPaginas => TotalRegistros <= 0
        ? 1
        : (TotalRegistros + PedidosConsolidadoService.TamanhoPagina - 1) / PedidosConsolidadoService.TamanhoPagina;

    public int PaginaEfetiva { get; private set; } = 1;

    public int PrimeiroItemPagina => TotalRegistros == 0 ? 0 : (PaginaEfetiva - 1) * PedidosConsolidadoService.TamanhoPagina + 1;

    public int UltimoItemPagina => TotalRegistros == 0
        ? 0
        : Math.Min(PaginaEfetiva * PedidosConsolidadoService.TamanhoPagina, TotalRegistros);

    /// <summary>Registros da página atual em JSON para preencher o modal de detalhe no cliente.</summary>
    public string LinhasModalJson =>
        Linhas.Count == 0
            ? "[]"
            : JsonSerializer.Serialize(
                Linhas.Select(l => new
                {
                    id = l.Dados.IdConsolidacao,
                    idNota = l.Dados.IdNota,
                    numeroEcommerce = l.NumeroEcommerce,
                    dataPedido = l.DataPedido,
                    cliente = l.Dados.Cliente,
                    ufVenda = l.Dados.UfVenda,
                    valorVenda = l.Dados.ValorVenda,
                    taxaMarketplace = l.Dados.TaxaMarketplace,
                    valorFrete = l.Dados.ValorFrete,
                    icms = l.Dados.Icms,
                    pis = l.Dados.Pis,
                    cofins = l.Dados.Cofins,
                    difal = l.Dados.Difal,
                    precoCustoTotal = l.PrecoCustoTotal,
                    mc = l.Mc
                }).ToList());

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (Buscar != "1")
            return;

        var filtro = new PedidosListagemFiltro(Di, Df, Uf ?? "", Cliente ?? "");
        try
        {
            var todas = await _pedidos.CarregarTodosParaExportAsync(filtro, cancellationToken);
            TotalRegistros = todas.Count;
            TotalValorVenda = todas.Sum(l => l.Dados.ValorVenda ?? 0m);
            TotalMc = todas.Sum(l => l.Mc ?? 0m);

            var totalPaginas = TotalRegistros <= 0
                ? 1
                : (TotalRegistros + PedidosConsolidadoService.TamanhoPagina - 1) / PedidosConsolidadoService.TamanhoPagina;
            PaginaEfetiva = Math.Clamp(P, 1, totalPaginas);
            if (P != PaginaEfetiva)
                P = PaginaEfetiva;

            var skip = (PaginaEfetiva - 1) * PedidosConsolidadoService.TamanhoPagina;
            Linhas = todas.Skip(skip).Take(PedidosConsolidadoService.TamanhoPagina).ToList();
        }
        catch (Exception ex)
        {
            Erro = $"Erro ao carregar dados de CONSOLIDADO_VENDAS: {ex.Message}";
            Linhas = new List<LinhaConsolidadoMc>();
            TotalRegistros = 0;
            TotalValorVenda = 0m;
            TotalMc = 0m;
        }
    }

    public async Task<IActionResult> OnGetExportarAsync(CancellationToken cancellationToken)
    {
        if (Buscar != "1")
            return RedirectToPage(new { });

        var filtro = new PedidosListagemFiltro(Di, Df, Uf ?? "", Cliente ?? "");
        List<LinhaConsolidadoMc> linhasExport;
        try
        {
            linhasExport = await _pedidos.CarregarTodosParaExportAsync(filtro, cancellationToken);
        }
        catch (Exception ex)
        {
            TempData["ErroExport"] = ex.Message;
            return RedirectToPage(new { buscar = "1", di = Di, df = Df, uf = Uf, cliente = Cliente, p = P, v = V });
        }

        using var stream = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Pedidos");
            ws.Cell(1, 1).Value = "Nº e-commerce";
            ws.Cell(1, 2).Value = "Data pedido";
            ws.Cell(1, 3).Value = "Cliente";
            ws.Cell(1, 4).Value = "UF";
            ws.Cell(1, 5).Value = "Valor venda";
            ws.Cell(1, 6).Value = "Taxa marketplace";
            ws.Cell(1, 7).Value = "Frete";
            ws.Cell(1, 8).Value = "ICMS";
            ws.Cell(1, 9).Value = "PIS";
            ws.Cell(1, 10).Value = "COFINS";
            ws.Cell(1, 11).Value = "DIFAL";
            ws.Cell(1, 12).Value = "Preço custo (itens)";
            ws.Cell(1, 13).Value = "MC";
            ws.Row(1).Style.Font.Bold = true;

            const string fmt = "#,##0.00";
            var r = 2;
            foreach (var linha in linhasExport)
            {
                ws.Cell(r, 1).Value = linha.NumeroEcommerce ?? string.Empty;
                if (linha.DataPedido.HasValue)
                {
                    ws.Cell(r, 2).Value = linha.DataPedido.Value;
                    ws.Cell(r, 2).Style.DateFormat.Format = "dd/MM/yyyy";
                }
                ws.Cell(r, 3).Value = linha.Dados.Cliente;
                ws.Cell(r, 4).Value = linha.Dados.UfVenda;
                ws.Cell(r, 5).Value = linha.Dados.ValorVenda;
                ws.Cell(r, 5).Style.NumberFormat.Format = fmt;
                ws.Cell(r, 6).Value = linha.Dados.TaxaMarketplace;
                ws.Cell(r, 6).Style.NumberFormat.Format = fmt;
                ws.Cell(r, 7).Value = linha.Dados.ValorFrete;
                ws.Cell(r, 7).Style.NumberFormat.Format = fmt;
                ws.Cell(r, 8).Value = linha.Dados.Icms;
                ws.Cell(r, 8).Style.NumberFormat.Format = fmt;
                ws.Cell(r, 9).Value = linha.Dados.Pis;
                ws.Cell(r, 9).Style.NumberFormat.Format = fmt;
                ws.Cell(r, 10).Value = linha.Dados.Cofins;
                ws.Cell(r, 10).Style.NumberFormat.Format = fmt;
                ws.Cell(r, 11).Value = linha.Dados.Difal;
                ws.Cell(r, 11).Style.NumberFormat.Format = fmt;
                ws.Cell(r, 12).Value = linha.PrecoCustoTotal;
                ws.Cell(r, 12).Style.NumberFormat.Format = fmt;
                ws.Cell(r, 13).Value = linha.Mc;
                ws.Cell(r, 13).Style.NumberFormat.Format = fmt;
                r++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(stream);
        }

        stream.Position = 0;
        var nome = $"Pedidos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            nome);
    }

    public IEnumerable<int> PaginasParaExibir()
    {
        var total = TotalPaginas;
        if (total <= 0)
            yield break;

        const int maxBotoes = 9;
        if (total <= maxBotoes)
        {
            for (var i = 1; i <= total; i++)
                yield return i;
            yield break;
        }

        var end = Math.Min(total, PaginaEfetiva + maxBotoes / 2);
        var start = Math.Max(1, end - maxBotoes + 1);
        end = Math.Min(total, start + maxBotoes - 1);
        start = Math.Max(1, end - maxBotoes + 1);

        for (var i = start; i <= end; i++)
            yield return i;
    }

    public bool TemAlgumFiltro() =>
        Di.HasValue || Df.HasValue || !string.IsNullOrWhiteSpace(Uf) || !string.IsNullOrWhiteSpace(Cliente);
}
