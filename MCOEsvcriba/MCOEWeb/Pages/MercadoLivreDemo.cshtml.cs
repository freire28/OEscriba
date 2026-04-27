using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCOEWeb.Services;

namespace MCOEWeb.Pages;

public class MercadoLivreDemoModel : PageModel
{
    private readonly MercadoLivreApiClient _ml;
    private static readonly JsonSerializerOptions JsonPretty = new() { WriteIndented = true };

    public MercadoLivreDemoModel(MercadoLivreApiClient ml) => _ml = ml;

    [BindProperty]
    public string TokenManual { get; set; } = "";

    [BindProperty]
    public string RefreshTokenManual { get; set; } = "";

    [BindProperty]
    public string SellerIdStr { get; set; } = "";

    [BindProperty]
    public int PedidosOffset { get; set; }

    [BindProperty]
    public int PedidosLimit { get; set; } = 50;

    [BindProperty]
    public string PedidoIdStr { get; set; } = "";

    public string? ErroManual { get; private set; }
    public string? JsonManual { get; private set; }
    public string? ErroRefresh { get; private set; }
    public string? JsonRefresh { get; private set; }
    public string? ErroPedidos { get; private set; }
    public string? JsonPedidos { get; private set; }
    public string? ErroPedidoId { get; private set; }
    public string? JsonPedidoId { get; private set; }

    public Task OnGetAsync() => Task.CompletedTask;

    public async Task<IActionResult> OnPostRefreshTokenAsync(CancellationToken cancellationToken)
    {
        ErroRefresh = null;
        JsonRefresh = null;
        if (string.IsNullOrWhiteSpace(RefreshTokenManual))
        {
            ErroRefresh = "Cole o refresh_token.";
            return Page();
        }

        try
        {
            var resp = await _ml.AtualizarTokenComRefreshTokenAsync(RefreshTokenManual.Trim(), cancellationToken);
            RefreshTokenManual = resp.RefreshToken;
            TokenManual = resp.AccessToken;
            JsonRefresh = JsonSerializer.Serialize(resp, JsonPretty);
        }
        catch (Exception ex)
        {
            ErroRefresh = ex.Message;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostUsersMeAsync(CancellationToken cancellationToken)
    {
        ErroManual = null;
        JsonManual = null;
        if (string.IsNullOrWhiteSpace(TokenManual))
        {
            ErroManual = "Cole o access token.";
            return Page();
        }

        try
        {
            _ml.SetAccessToken(TokenManual.Trim());
            JsonManual = await _ml.GetAsync("users/me", cancellationToken);
        }
        catch (Exception ex)
        {
            ErroManual = ex.Message;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostPedidosVendedorAsync(CancellationToken cancellationToken)
    {
        ErroPedidos = null;
        JsonPedidos = null;
        if (string.IsNullOrWhiteSpace(TokenManual))
        {
            ErroPedidos = "Cole o access token na seção 3 (ou renove na seção 2).";
            return Page();
        }

        try
        {
            _ml.SetAccessToken(TokenManual.Trim());

            long sellerId;
            if (string.IsNullOrWhiteSpace(SellerIdStr))
            {
                var me = await _ml.ObterUsuarioAutenticadoAsync(cancellationToken);
                if (me is null)
                {
                    ErroPedidos = "Não foi possível obter /users/me.";
                    return Page();
                }
                sellerId = me.Id;
            }
            else if (!long.TryParse(SellerIdStr.Trim(), out sellerId))
            {
                ErroPedidos = "ID do vendedor inválido.";
                return Page();
            }

            JsonPedidos = await _ml.BuscarPedidosPorVendedorAsync(sellerId, PedidosOffset, PedidosLimit, null, cancellationToken);
        }
        catch (Exception ex)
        {
            ErroPedidos = ex.Message;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostPedidoPorIdAsync(CancellationToken cancellationToken)
    {
        ErroPedidoId = null;
        JsonPedidoId = null;
        if (string.IsNullOrWhiteSpace(TokenManual))
        {
            ErroPedidoId = "Cole o access token na seção 3 (ou renove na seção 2).";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(PedidoIdStr))
        {
            ErroPedidoId = "Informe o ID do pedido.";
            return Page();
        }

        try
        {
            _ml.SetAccessToken(TokenManual.Trim());
            JsonPedidoId = await _ml.ObterPedidoPorIdAsync(PedidoIdStr.Trim(), cancellationToken);
        }
        catch (Exception ex)
        {
            ErroPedidoId = ex.Message;
        }

        return Page();
    }
}
