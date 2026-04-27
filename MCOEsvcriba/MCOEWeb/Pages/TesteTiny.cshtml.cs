using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCOEWeb.Services;

namespace MCOEWeb.Pages;

public class TesteTinyModel : PageModel
{
    private readonly TinyApiClient _tiny;

    public TesteTinyModel(TinyApiClient tiny) => _tiny = tiny;

    [BindProperty]
    public string Token { get; set; } = "";

    public string? Resultado { get; private set; }

    public Task OnGetAsync() => Task.CompletedTask;

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            Resultado = "Informe o token da API Tiny.";
            return Page();
        }

        try
        {
            var filtros = new Dictionary<string, string>();
            var resposta = await _tiny.PesquisarNotasFiscaisAsync(Token, filtros, cancellationToken);
            Resultado = resposta;
        }
        catch (Exception ex)
        {
            Resultado = $"Erro ao chamar API: {ex.Message}";
        }

        return Page();
    }
}
