namespace MCOEWeb.Data.Entities;

/// <summary>
/// Mapeia <c>dbo.PRODUTOS</c>.
/// </summary>
public class Produto
{
    /// <summary>Gerado pelo banco (IDENTITY); não atribuir em inserts.</summary>
    public int IdProduto { get; set; }

    public string IdProdutoTiny { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public decimal Preco { get; set; }

    public decimal PrecoCusto { get; set; }

    public bool Ativo { get; set; }
}
