using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MCOEWeb.Data;
using MCOEWeb.Data.Entities;

namespace MCOEWeb.Services;

/// <summary>
/// Sincroniza produtos da API Tiny (<c>produtos.pesquisa.php</c>) para <c>dbo.PRODUTOS</c>.
/// </summary>
public class ProdutosSincronizacaoService
{
    private readonly TinyApiClient _tiny;
    private readonly OescribaDbContext _db;

    public ProdutosSincronizacaoService(TinyApiClient tiny, OescribaDbContext db)
    {
        _tiny = tiny;
        _db = db;
    }

    /// <param name="progressBase">Percentual inicial no progresso global (ex.: 50 quando pedidos já usaram 0–50).</param>
    /// <param name="progressSpan">Faixa de percentual reservada a esta etapa (ex.: 50 para ocupar 50–100).</param>
    public async Task<ProdutosSincronizacaoResultado> SincronizarDoTinyAsync(
        string token,
        IProgress<SincronizacaoProgresso>? progresso = null,
        int progressBase = 0,
        int progressSpan = 100,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Informe o token da API Tiny.", nameof(token));

        progressSpan = Math.Clamp(progressSpan, 1, 100);
        progressBase = Math.Clamp(progressBase, 0, 100);

        var inseridos = 0;
        var atualizados = 0;
        var ignorados = 0;

        var pagina = 1;
        var totalPaginas = 1;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filtros = new Dictionary<string, string>
            {
                ["pagina"] = pagina.ToString(CultureInfo.InvariantCulture)
            };

            var root = await _tiny.ListarProdutosAsync(token.Trim(), filtros, cancellationToken);
            var ret = root.Retorno;
            if (ret is null)
                throw new InvalidOperationException("Resposta inválida da API Tiny (produtos).");

            if (!string.Equals(ret.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                var msg = ret.Erros?.FirstOrDefault()?.Erro ?? ret.Status ?? "Erro na pesquisa de produtos.";
                throw new InvalidOperationException(msg);
            }

            totalPaginas = Math.Max(1, ret.NumeroPaginas ?? 1);

            ReportarProgresso(progresso, progressBase, progressSpan, pagina, totalPaginas,
                $"Produtos — página {pagina} de {totalPaginas}…");

            var wrappers = ret.Produtos ?? new List<TinyProdutoPesquisaWrapper>();
            if (wrappers.Count > 0)
            {
                var linhas = wrappers
                    .Select(w => w.Produto)
                    .Where(p => p is not null)
                    .Cast<TinyProdutoPesquisa>()
                    .ToList();

                var idsTiny = linhas
                    .Select(p => NormalizarIdTiny(p.Id))
                    .Where(id => id.Length > 0)
                    .Distinct()
                    .ToList();

                var existentes = idsTiny.Count == 0
                    ? new Dictionary<string, Produto>(StringComparer.Ordinal)
                    : await _db.Produtos
                        .Where(x => idsTiny.Contains(x.IdProdutoTiny))
                        .ToDictionaryAsync(x => x.IdProdutoTiny, StringComparer.Ordinal, cancellationToken);

                foreach (var p in linhas)
                {
                    var idTiny = NormalizarIdTiny(p.Id);
                    if (idTiny.Length == 0)
                    {
                        ignorados++;
                        continue;
                    }

                    var nome = Truncate(p.Nome, 255);
                    var preco = p.Preco ?? 0m;
                    var precoCusto = p.PrecoCusto ?? 0m;
                    var ativo = MapearAtivo(p);

                    if (existentes.TryGetValue(idTiny, out var row))
                    {
                        row.Nome = nome;
                        row.Preco = preco;
                        row.PrecoCusto = precoCusto;
                        row.Ativo = ativo;
                        atualizados++;
                    }
                    else
                    {
                        var novo = new Produto
                        {
                            IdProdutoTiny = idTiny,
                            Nome = nome,
                            Preco = preco,
                            PrecoCusto = precoCusto,
                            Ativo = ativo
                        };
                        _db.Produtos.Add(novo);
                        existentes[idTiny] = novo;
                        inseridos++;
                    }
                }

                await _db.SaveChangesAsync(cancellationToken);
            }

            if (pagina >= totalPaginas)
                break;
            pagina++;
        }

        ReportarProgresso(progresso, progressBase, progressSpan, totalPaginas, totalPaginas,
            $"Produtos — concluído ({inseridos} novo(s), {atualizados} atualizado(s)).");

        return new ProdutosSincronizacaoResultado(inseridos, atualizados, ignorados);
    }

    private static void ReportarProgresso(
        IProgress<SincronizacaoProgresso>? progresso,
        int progressBase,
        int progressSpan,
        int paginaAtual,
        int totalPaginas,
        string mensagem)
    {
        if (progresso is null)
            return;
        var pctInterno = totalPaginas <= 0
            ? 100
            : (int)(paginaAtual * 100.0 / totalPaginas);
        var pct = progressBase + (int)(pctInterno * progressSpan / 100.0);
        pct = Math.Clamp(pct, 0, 100);
        progresso.Report(new SincronizacaoProgresso(pct, mensagem));
    }

    private static string NormalizarIdTiny(string? id)
    {
        var t = (id ?? "").Trim();
        if (t.Length == 0)
            return string.Empty;
        return t.Length <= 10 ? t : t[..10];
    }

    private static string Truncate(string? s, int max)
    {
        var t = (s ?? "").Trim();
        return t.Length <= max ? t : t[..max];
    }

    /// <summary>API costuma enviar <c>situacao</c> = A (ativo) ou I (inativo).</summary>
    private static bool MapearAtivo(TinyProdutoPesquisa p)
    {
        var s = (p.Situacao ?? "").Trim();
        if (s.Length == 0)
            return true;
        if (s.Equals("I", StringComparison.OrdinalIgnoreCase)
            || s.Equals("Inativo", StringComparison.OrdinalIgnoreCase)
            || s.Equals("E", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }
}

public record ProdutosSincronizacaoResultado(int Inseridos, int Atualizados, int IgnoradosSemId);
