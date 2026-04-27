using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using MCOEWeb.Services;

namespace MCOEWeb.Pages;

public class MercadoLivreCallbackModel : PageModel
{
    private readonly MercadoLivreApiClient _mercadoLivre;

    public MercadoLivreCallbackModel(MercadoLivreApiClient mercadoLivre) => _mercadoLivre = mercadoLivre;

    public bool Carregando { get; private set; }
    public string? Erro { get; private set; }
    public string? UserJson { get; private set; }
    public string? ResumoToken { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Carregando = true;
        Erro = null;
        UserJson = null;
        ResumoToken = null;

        var query = QueryHelpers.ParseQuery(Request.QueryString.Value ?? "");

        if (query.TryGetValue("error", out var err))
        {
            var desc = query.TryGetValue("error_description", out var ed) ? ed.ToString() : "";
            Erro = $"Autorização recusada: {err} {desc}";
            Carregando = false;
            return;
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            Erro = "Nenhum código (code) na URL. Cadastre no Mercado Livre a Redirect URI exatamente igual à desta página (incluindo o caminho /callback-mercadolivre).";
            Carregando = false;
            return;
        }

        try
        {
            var tokenResponse = await _mercadoLivre.GetTokenAsync(code.ToString(), cancellationToken);
            ResumoToken = $"Expira em {tokenResponse.ExpiresIn} segundos. Guarde o refresh_token em banco/sessão para renovar. User ID: {tokenResponse.UserId}";
            UserJson = await _mercadoLivre.GetAsync("users/me", cancellationToken);
        }
        catch (Exception ex)
        {
            Erro = ex.Message;
        }
        finally
        {
            Carregando = false;
        }
    }
}
