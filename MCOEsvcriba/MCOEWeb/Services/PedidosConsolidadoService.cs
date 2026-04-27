using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MCOEWeb.Data;
using MCOEWeb.Data.Entities;

namespace MCOEWeb.Services;

public record PedidosListagemFiltro(DateTime? DataInicio, DateTime? DataFim, string Uf, string Cliente);

/// <summary>
/// Linha de <see cref="ConsolidadoVendas"/> com e-commerce, custo e MC calculados (ex-Pedidos.razor).
/// </summary>
public sealed class LinhaConsolidadoMc
{
    public required ConsolidadoVendas Dados { get; init; }
    public decimal? Mc { get; init; }
    public string? NumeroEcommerce { get; init; }
    public decimal? PrecoCustoTotal { get; init; }
    /// <summary>Data do pedido em <c>PEDIDOS</c>, quando há vínculo nota → pedido.</summary>
    public DateTime? DataPedido { get; init; }
}

public class PedidosConsolidadoService
{
    private readonly OescribaDbContext _db;

    public PedidosConsolidadoService(OescribaDbContext db) => _db = db;

    public const int TamanhoPagina = 25;

    public async Task<(List<LinhaConsolidadoMc> Linhas, int TotalRegistros, int PaginaEfetiva)> CarregarPaginaAsync(
        PedidosListagemFiltro filtro,
        int paginaSolicitada,
        CancellationToken cancellationToken = default)
    {
        var q = BuildConsolidadosQueryAplicado(filtro);

        var totalRegistros = await q
            .Select(c => c.IdConsolidacao)
            .Distinct()
            .CountAsync(cancellationToken);

        if (totalRegistros == 0)
            return (new List<LinhaConsolidadoMc>(), 0, 1);

        var totalPaginas = (totalRegistros + TamanhoPagina - 1) / TamanhoPagina;
        var paginaAtual = Math.Clamp(paginaSolicitada, 1, totalPaginas);

        var skip = (paginaAtual - 1) * TamanhoPagina;

        var ids = await q
            .Select(c => c.IdConsolidacao)
            .Distinct()
            .OrderByDescending(id => id)
            .Skip(skip)
            .Take(TamanhoPagina)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return (new List<LinhaConsolidadoMc>(), totalRegistros, paginaAtual);

        var consolidados = await _db.ConsolidadoVendas
            .AsNoTracking()
            .Where(c => ids.Contains(c.IdConsolidacao))
            .OrderByDescending(c => c.IdConsolidacao)
            .ToListAsync(cancellationToken);

        var linhas = await MontarLinhasComMargemAsync(consolidados, cancellationToken);
        return (linhas, totalRegistros, paginaAtual);
    }

    public async Task<List<LinhaConsolidadoMc>> CarregarTodosParaExportAsync(
        PedidosListagemFiltro filtro,
        CancellationToken cancellationToken = default)
    {
        var q = BuildConsolidadosQueryAplicado(filtro);
        var ids = await q
            .Select(c => c.IdConsolidacao)
            .Distinct()
            .OrderByDescending(id => id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return new List<LinhaConsolidadoMc>();

        var consolidados = await _db.ConsolidadoVendas
            .AsNoTracking()
            .Where(c => ids.Contains(c.IdConsolidacao))
            .OrderByDescending(c => c.IdConsolidacao)
            .ToListAsync(cancellationToken);

        return await MontarLinhasComMargemAsync(consolidados, cancellationToken);
    }

    public async Task<LinhaConsolidadoMc?> ObterLinhaPorIdConsolidacaoAsync(
        int idConsolidacao,
        CancellationToken cancellationToken = default)
    {
        var c = await _db.ConsolidadoVendas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdConsolidacao == idConsolidacao, cancellationToken);
        if (c is null)
            return null;
        var linhas = await MontarLinhasComMargemAsync(new List<ConsolidadoVendas> { c }, cancellationToken);
        return linhas.FirstOrDefault();
    }

    private IQueryable<ConsolidadoVendas> BuildConsolidadosQueryAplicado(PedidosListagemFiltro filtro)
    {
        IQueryable<ConsolidadoVendas> q;

        if (TemFiltroDataPedido(filtro))
        {
            var (inicioDia, fimExclusivo) = ObterIntervaloDataPedido(filtro);
            q =
                from c in _db.ConsolidadoVendas.AsNoTracking()
                join nf in _db.NotasFiscais.AsNoTracking() on c.IdNota equals nf.IdNotaFiscal
                join ped in _db.Pedidos.AsNoTracking() on nf.IdPedido!.Trim() equals ped.IdTiny
                where ped.DataPedido >= inicioDia && ped.DataPedido < fimExclusivo
                select c;
        }
        else
        {
            q = _db.ConsolidadoVendas.AsNoTracking();
        }

        if (!string.IsNullOrWhiteSpace(filtro.Cliente))
        {
            var termo = filtro.Cliente.Trim();
            q = q.Where(c => c.Cliente.Contains(termo));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Uf))
        {
            var uf = NormalizarUf(filtro.Uf);
            if (uf.Length > 0)
                q = q.Where(c => c.UfVenda == uf);
        }

        return q;
    }

    private static bool TemFiltroDataPedido(PedidosListagemFiltro filtro) =>
        filtro.DataInicio.HasValue || filtro.DataFim.HasValue;

    private static (DateTime InicioDia, DateTime FimExclusivo) ObterIntervaloDataPedido(PedidosListagemFiltro filtro)
    {
        var i = filtro.DataInicio?.Date;
        var f = filtro.DataFim?.Date;
        DateTime inicio;
        DateTime fimInclusivo;
        if (i.HasValue && f.HasValue)
        {
            inicio = i.Value;
            fimInclusivo = f.Value;
            if (inicio > fimInclusivo)
                (inicio, fimInclusivo) = (fimInclusivo, inicio);
        }
        else if (i.HasValue)
        {
            inicio = i.Value;
            fimInclusivo = i.Value;
        }
        else
        {
            fimInclusivo = f!.Value;
            inicio = fimInclusivo;
        }

        return (inicio, fimInclusivo.AddDays(1));
    }

    private static string NormalizarUf(string uf)
    {
        var t = uf.Trim().ToUpperInvariant();
        return t.Length <= 2 ? t : t[..2];
    }

    private async Task<List<LinhaConsolidadoMc>> MontarLinhasComMargemAsync(
        List<ConsolidadoVendas> consolidados,
        CancellationToken cancellationToken)
    {
        if (consolidados.Count == 0)
            return new List<LinhaConsolidadoMc>();

        var idNotas = consolidados.Select(c => c.IdNota).Distinct().ToList();

        var notasList = await _db.NotasFiscais
            .AsNoTracking()
            .Where(n => idNotas.Contains(n.IdNotaFiscal))
            .ToListAsync(cancellationToken);

        var notaPorIdNota = notasList
            .GroupBy(n => n.IdNotaFiscal)
            .ToDictionary(g => g.Key, g => g.First());

        var idTinysPedido = notaPorIdNota.Values
            .Select(n => (n.IdPedido ?? "").Trim())
            .Where(s => s.Length > 0)
            .Distinct()
            .ToList();

        var pedidosList = idTinysPedido.Count == 0
            ? new List<Pedido>()
            : await _db.Pedidos
                .AsNoTracking()
                .Where(p => idTinysPedido.Contains(p.IdTiny))
                .ToListAsync(cancellationToken);

        var pedidoPorIdTiny = pedidosList
            .GroupBy(p => p.IdTiny)
            .ToDictionary(g => g.Key, g => g.First());

        var idsPedidoInterno = pedidosList.Select(p => (long)p.IdPedido).Distinct().ToList();

        var itensList = idsPedidoInterno.Count == 0
            ? new List<ItensPedido>()
            : await _db.ItensPedidos
                .AsNoTracking()
                .Where(i => i.IdPedido.HasValue && idsPedidoInterno.Contains(i.IdPedido.Value))
                .ToListAsync(cancellationToken);

        var itensPorPedido = itensList
            .GroupBy(i => i.IdPedido!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var chavesProdutoTiny = itensList
            .Where(i => i.IdProduto.HasValue)
            .Select(i => IdProdutoParaChaveTiny(i.IdProduto!.Value))
            .Where(k => k.Length > 0)
            .Distinct()
            .ToList();

        var produtosList = chavesProdutoTiny.Count == 0
            ? new List<Produto>()
            : await _db.Produtos
                .AsNoTracking()
                .Where(p => chavesProdutoTiny.Contains(p.IdProdutoTiny))
                .ToListAsync(cancellationToken);

        var custoPorIdProdutoTiny = produtosList.ToDictionary(p => p.IdProdutoTiny, p => p.PrecoCusto);

        var resultado = new List<LinhaConsolidadoMc>(consolidados.Count);

        foreach (var c in consolidados)
        {
            decimal? mc = null;
            decimal? precoCustoTotal = null;
            string? numeroEcommerce = null;
            DateTime? dataPedido = null;

            if (notaPorIdNota.TryGetValue(c.IdNota, out var nf))
            {
                var idTiny = (nf.IdPedido ?? "").Trim();
                if (idTiny.Length > 0 && pedidoPorIdTiny.TryGetValue(idTiny, out var ped))
                {
                    numeroEcommerce = string.IsNullOrWhiteSpace(ped.NumeroEcommerce) ? null : ped.NumeroEcommerce.Trim();
                    dataPedido = ped.DataPedido;
                    var somaCusto = SomarCustoItensPedido((long)ped.IdPedido, itensPorPedido, custoPorIdProdutoTiny);
                    precoCustoTotal = somaCusto;
                    mc = CalcularMargemContribuicao(c, somaCusto);
                }
            }

            resultado.Add(new LinhaConsolidadoMc
            {
                Dados = c,
                Mc = mc,
                NumeroEcommerce = numeroEcommerce,
                PrecoCustoTotal = precoCustoTotal,
                DataPedido = dataPedido
            });
        }

        return resultado;
    }

    private static string IdProdutoParaChaveTiny(long idProdutoTiny) =>
        idProdutoTiny.ToString(CultureInfo.InvariantCulture);

    private static decimal SomarCustoItensPedido(
        long idPedidoInterno,
        IReadOnlyDictionary<long, List<ItensPedido>> itensPorPedido,
        IReadOnlyDictionary<string, decimal> custoPorIdProdutoTiny)
    {
        if (!itensPorPedido.TryGetValue(idPedidoInterno, out var itens))
            return 0m;

        decimal soma = 0;
        foreach (var it in itens)
        {
            if (!it.IdProduto.HasValue)
                continue;

            var chave = IdProdutoParaChaveTiny(it.IdProduto.Value);
            if (!custoPorIdProdutoTiny.TryGetValue(chave, out var precoCusto))
                continue;

            var q = it.Quantidade ?? 1m;
            soma += precoCusto * q;
        }

        return soma;
    }

    private static decimal CalcularMargemContribuicao(ConsolidadoVendas a, decimal somaPrecoCustoProdutos)
    {
        var vv = a.ValorVenda ?? 0m;
        var taxa = a.TaxaMarketplace ?? 0m;
        var frete = a.ValorFrete ?? 0m;
        var icms = a.Icms ?? 0m;
        var pis = a.Pis ?? 0m;
        var cofins = a.Cofins ?? 0m;
        var difal = a.Difal ?? 0m;

        return vv - (taxa + icms + pis + cofins + difal + somaPrecoCustoProdutos);
    }
}

public static class PedidosViewFormat
{
    public static string Currency(decimal? valor) =>
        valor.HasValue ? valor.Value.ToString("C2", CultureInfo.GetCultureInfo("pt-BR")) : "-";

    public static string Mc(decimal? valor) =>
        valor.HasValue ? valor.Value.ToString("C2", CultureInfo.GetCultureInfo("pt-BR")) : "-";

    public static string Texto(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? "-" : valor;

    public static string DataPedido(DateTime? valor) =>
        valor.HasValue ? valor.Value.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR")) : "-";

    public static string? RowClassMc(decimal? mc) =>
        mc is < 0m ? "table-danger" : null;

    public static string? CardClassMc(decimal? mc) =>
        mc is < 0m ? "border-danger" : "border-light";
}
