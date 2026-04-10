namespace MCOEWeb.Services;

/// <summary>
/// Mantém access/refresh token do Mercado Livre (config, variáveis de ambiente e respostas OAuth).
/// </summary>
public interface IMercadoLivreTokenStore
{
    string? AccessToken { get; }
    string? RefreshToken { get; }

    /// <summary>
    /// Indica se o access token pode ser usado (não expirou em relação ao buffer configurado).
    /// Sem data de expiração conhecida, considera válido se houver access token.
    /// </summary>
    bool IsAccessTokenValid();

    bool CanRefresh();

    void UpdateFromTokenResponse(MercadoLivreTokenResponse response);

    /// <summary>
    /// Define o access token manualmente (ex.: página de demo). Define expiração relativa ao TTL padrão do ML se não informado.
    /// </summary>
    void SetAccessToken(string accessToken, TimeSpan? validFor = null);
}
