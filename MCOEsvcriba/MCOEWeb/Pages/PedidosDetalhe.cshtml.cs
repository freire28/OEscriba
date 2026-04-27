using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCOEWeb.Services;

namespace MCOEWeb.Pages;

public class PedidosDetalheModel : PageModel
{
    private readonly PedidosConsolidadoService _pedidos;

    public PedidosDetalheModel(PedidosConsolidadoService pedidos) => _pedidos = pedidos;

    [BindProperty(SupportsGet = true)]
    public int Cid { get; set; }

    public LinhaConsolidadoMc? Linha { get; private set; }
    public string? Erro { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (Cid <= 0)
        {
            Erro = "Id de consolidação inválido.";
            return Page();
        }

        Linha = await _pedidos.ObterLinhaPorIdConsolidacaoAsync(Cid, cancellationToken);
        if (Linha is null)
            Erro = "Registro não encontrado.";
        return Page();
    }
}
