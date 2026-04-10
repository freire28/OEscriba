namespace MCOEWeb.Data.Entities;

/// <summary>
/// Mapeia <c>dbo.CONSOLIDADO_VENDAS</c>.
/// </summary>
public class ConsolidadoVendas
{
    /// <summary>Gerado pelo banco (IDENTITY); não atribuir em inserts.</summary>
    public int IdConsolidacao { get; set; }

    public int IdNota { get; set; }

    public string Cliente { get; set; } = string.Empty;

    public string UfVenda { get; set; } = string.Empty;

    public decimal? ValorVenda { get; set; }

    public decimal? TaxaMarketplace { get; set; }

    public decimal? ValorFrete { get; set; }

    public decimal? Icms { get; set; }

    public decimal? Pis { get; set; }

    public decimal? Cofins { get; set; }

    public decimal? Difal { get; set; }
}
