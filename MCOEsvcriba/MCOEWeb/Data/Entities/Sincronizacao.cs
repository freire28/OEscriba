namespace MCOEWeb.Data.Entities;

/// <summary>
/// Mapeia <c>dbo.SINCRONIZACOES</c> — registro de uma execução de sincronização.
/// </summary>
public class Sincronizacao
{
    /// <summary>Gerado pelo banco (IDENTITY); não atribuir em inserts.</summary>
    public int IdSincronizacao { get; set; }

    public DateTime DataSincronizacao { get; set; }

    public bool Produtos { get; set; }

    public bool Pedidos { get; set; }
}
