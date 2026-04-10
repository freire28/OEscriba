namespace MCOEWeb.Data.Entities;

/// <summary>
/// Mapeia <c>dbo.NOTAS_FISCAIS</c>.
/// Chave primária composta: <see cref="IdNotaFiscal"/> (IDENTITY no SQL Server) + <see cref="IdTiny"/>.
/// </summary>
public class NotaFiscal
{
    /// <summary>Gerado pelo banco (IDENTITY); não atribuir em inserts.</summary>
    public int IdNotaFiscal { get; set; }

    public string IdTiny { get; set; } = string.Empty;

    /// <summary>
    /// Conteúdo XML da NF-e (coluna <c>xml</c> no SQL Server).
    /// </summary>
    public string XmlNota { get; set; } = string.Empty;

    public string IdPedido { get; set; } = string.Empty;

    /// <summary>
    /// Coluna <c>CONSOLIDADA</c> (<c>bit</c> no SQL Server); novos registros entram com <c>0</c>.
    /// </summary>
    public bool Consolidada { get; set; }
}
