namespace MCOEWeb.Data.Entities;

/// <summary>
/// Mapeia <c>dbo.PEDIDOS</c>.
/// Chave primária composta: <see cref="IdPedido"/> + <see cref="IdTiny"/>.
/// </summary>
public class Pedido
{
    public int IdPedido { get; set; }

    public string IdTiny { get; set; } = string.Empty;

    public string NumeroEcommerce { get; set; } = string.Empty;

    public DateTime DataPedido { get; set; }

    public DateTime DataSincronizacao { get; set; }

    /// <summary>Id da nota fiscal na Olist/Tiny (<c>pedido.obter</c> → <c>id_nota_fiscal</c>). Texto no banco (varchar/nvarchar).</summary>
    public string? IdNotaFiscal { get; set; }
}
