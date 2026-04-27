namespace MCOEWeb.Data.Entities;

/// <summary>
/// Mapeia <c>dbo.PEDIDOS</c>.
/// Chave primária composta: <see cref="IdPedido"/> + <see cref="IdTiny"/>.
/// </summary>
public class Pedido
{
    public int IdPedido { get; set; }

    public string IdTiny { get; set; } = string.Empty;

    /// <summary>Identificador de e-commerce na Tiny (ex.: id do pedido no Mercado Livre em <c>numero_ecommerce</c>).</summary>
    public string NumeroEcommerce { get; set; } = string.Empty;

    public DateTime DataPedido { get; set; }

    public DateTime DataSincronizacao { get; set; }

    /// <summary>Id da nota fiscal na Olist/Tiny (<c>pedido.obter</c> → <c>id_nota_fiscal</c>). Texto no banco (varchar/nvarchar).</summary>
    public string? IdNotaFiscal { get; set; }
}
