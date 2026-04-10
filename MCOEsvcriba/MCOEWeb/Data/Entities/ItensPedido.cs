namespace MCOEWeb.Data.Entities;

/// <summary>
/// Mapeia <c>dbo.ItensPedido</c>.
/// </summary>
public class ItensPedido
{
    /// <summary>Gerado pelo banco (IDENTITY); não atribuir em inserts.</summary>
    public int Id { get; set; }

    public long? IdPedido { get; set; }

    public long? IdProduto { get; set; }

    public string? Codigo { get; set; }

    public string? Descricao { get; set; }

    public string? Unidade { get; set; }

    public decimal? Quantidade { get; set; }

    public decimal? ValorUnitario { get; set; }
}
