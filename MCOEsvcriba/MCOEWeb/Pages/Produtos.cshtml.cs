using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCOEWeb.Services;

namespace MCOEWeb.Pages;

public class ProdutosModel : PageModel
{
    private readonly TinyApiClient _tiny;

    public ProdutosModel(TinyApiClient tiny) => _tiny = tiny;

    [BindProperty]
    public string Token { get; set; } = "";

    [BindProperty]
    public string Pesquisa { get; set; } = "";

    public string? Erro { get; set; }
    public List<ProdutoViewModel> Produtos { get; private set; } = new();

    public Task OnGetAsync() => Task.CompletedTask;

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Erro = null;
        Produtos.Clear();

        if (string.IsNullOrWhiteSpace(Token))
        {
            Erro = "Informe o token da API Tiny.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Pesquisa))
        {
            Erro = "Informe ao menos parte do nome ou código do produto para pesquisar.";
            return Page();
        }

        try
        {
            var filtros = new Dictionary<string, string> { ["pesquisa"] = Pesquisa };

            var retornoRoot = await _tiny.ListarProdutosAsync(Token, filtros, cancellationToken);
            var ret = retornoRoot.Retorno;
            if (ret is null)
            {
                Erro = "Resposta inválida da API Tiny (produtos).";
                return Page();
            }

            if (!string.Equals(ret.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                Erro = ret.Erros?.FirstOrDefault()?.Erro ?? ret.Status ?? "Erro na pesquisa de produtos.";
                return Page();
            }

            if (ret.Produtos is null || ret.Produtos.Count == 0)
            {
                Erro = "Nenhum produto encontrado.";
                return Page();
            }

            foreach (var wrapper in ret.Produtos)
            {
                var prod = wrapper.Produto;
                if (prod is null)
                    continue;

                Produtos.Add(new ProdutoViewModel
                {
                    Id = prod.Id ?? string.Empty,
                    Nome = prod.Nome ?? string.Empty,
                    PrecoVenda = prod.Preco ?? 0m,
                    PrecoCusto = prod.PrecoCusto,
                    ValorFrete = null,
                    Icms = null,
                    Pis = null,
                    Cofins = null,
                    Difal = null
                });
            }
        }
        catch (Exception ex)
        {
            Erro = $"Erro ao carregar produtos: {ex.Message}";
        }

        return Page();
    }

    public static string FormatCurrency(decimal? value) =>
        value.HasValue ? value.Value.ToString("C2", CultureInfo.GetCultureInfo("pt-BR")) : "-";
}

public class ProdutoViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public decimal? PrecoCusto { get; set; }
    public decimal PrecoVenda { get; set; }
    public decimal? ValorFrete { get; set; }
    public decimal? Icms { get; set; }
    public decimal? Pis { get; set; }
    public decimal? Cofins { get; set; }
    public decimal? Difal { get; set; }
}
