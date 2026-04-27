using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCOEWeb.Services;

namespace MCOEWeb.Pages;

public class NotasFiscaisModel : PageModel
{
    private readonly TinyApiClient _tiny;

    public NotasFiscaisModel(TinyApiClient tiny) => _tiny = tiny;

    [BindProperty]
    public string Token { get; set; } = "";

    [BindProperty]
    public int Pagina { get; set; } = 1;

    public string? Erro { get; set; }
    public List<NotaFiscalResumoVm> Notas { get; private set; } = new();

    public Task OnGetAsync() => Task.CompletedTask;

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Erro = null;
        Notas.Clear();

        if (string.IsNullOrWhiteSpace(Token))
        {
            Erro = "Informe o token da API Tiny.";
            return Page();
        }

        if (Pagina < 1)
            Pagina = 1;

        try
        {
            var filtros = new Dictionary<string, string>
            {
                ["pagina"] = Pagina.ToString(CultureInfo.InvariantCulture)
            };

            var root = await _tiny.ListarNotasFiscaisAsync(Token.Trim(), filtros, cancellationToken);
            var ret = root.Retorno;

            if (ret is null)
            {
                Erro = "Resposta inválida da API Tiny.";
                return Page();
            }

            if (!string.Equals(ret.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                Erro = ret.Erros?.FirstOrDefault()?.Erro ?? ret.Status ?? "Erro na pesquisa.";
                return Page();
            }

            var listaNotas = ret.NotasFiscais;
            if (listaNotas is null || listaNotas.Count == 0)
            {
                Erro = "Nenhuma nota fiscal encontrada.";
                return Page();
            }

            foreach (var wrapper in listaNotas)
            {
                var nf = wrapper.NotaFiscal;
                if (nf is null)
                    continue;

                Notas.Add(new NotaFiscalResumoVm
                {
                    Id = nf.Id ?? "",
                    Numero = nf.Numero ?? "",
                    Serie = nf.Serie ?? "",
                    DataEmissao = nf.DataEmissao ?? "",
                    NomeCliente = nf.Nome ?? "",
                    ValorProdutos = nf.ValorProdutos,
                    ValorFrete = nf.ValorFrete,
                    DescricaoSituacao = nf.DescricaoSituacao ?? nf.Situacao ?? ""
                });
            }
        }
        catch (Exception ex)
        {
            Erro = $"Erro ao carregar notas fiscais: {ex.Message}";
        }

        return Page();
    }

    public static string FormatCurrency(decimal? value) =>
        value.HasValue ? value.Value.ToString("C2", new CultureInfo("pt-BR")) : "-";
}

public class NotaFiscalResumoVm
{
    public string Id { get; set; } = "";
    public string Numero { get; set; } = "";
    public string Serie { get; set; } = "";
    public string DataEmissao { get; set; } = "";
    public string NomeCliente { get; set; } = "";
    public decimal? ValorProdutos { get; set; }
    public decimal? ValorFrete { get; set; }
    public string DescricaoSituacao { get; set; } = "";
}
