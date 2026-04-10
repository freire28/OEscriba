namespace MCOEWeb.Services;

/// <summary>
/// Carrega tokens de <c>MercadoLivre:*</c> / variáveis de ambiente e atualiza após OAuth.
/// Expiração padrão do ML: <c>expires_in</c> = 21600 s; usa margem antes de expirar para renovar.
/// </summary>
public sealed class MercadoLivreTokenStore : IMercadoLivreTokenStore
{
    /// <summary>Valor típico retornado pelo ML em <c>expires_in</c> (6 horas).</summary>
    public const int DefaultExpiresInSeconds = 21600;

    /// <summary>Renovar alguns minutos antes do fim da validade.</summary>
    public const int RefreshBufferSeconds = 300;

    private readonly object _sync = new();
    private string? _accessToken;
    private string? _refreshToken;
    private DateTimeOffset? _accessTokenExpiresAtUtc;

    public MercadoLivreTokenStore(IConfiguration configuration)
    {
        var section = configuration.GetSection("MercadoLivre");

        _accessToken = FirstNonEmpty(
            section["AccessToken"],
            Environment.GetEnvironmentVariable("MERCADOLIVRE_ACCESS_TOKEN"));

        _refreshToken = FirstNonEmpty(
            section["RefreshToken"],
            Environment.GetEnvironmentVariable("MERCADOLIVRE_REFRESH_TOKEN"));

        var expiresRaw = FirstNonEmpty(
            section["AccessTokenExpiresAtUtc"],
            Environment.GetEnvironmentVariable("MERCADOLIVRE_ACCESS_TOKEN_EXPIRES_AT"));

        _accessTokenExpiresAtUtc = ParseExpiresAt(expiresRaw);

        // Sem expiração informada, mas com access + refresh: primeira chamada à API renova e grava expires_in (ex.: 21600 s).
        if (!_accessTokenExpiresAtUtc.HasValue
            && !string.IsNullOrWhiteSpace(_accessToken)
            && !string.IsNullOrWhiteSpace(_refreshToken))
        {
            _accessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        }
    }

    public string? AccessToken
    {
        get
        {
            lock (_sync) return _accessToken;
        }
    }

    public string? RefreshToken
    {
        get
        {
            lock (_sync) return _refreshToken;
        }
    }

    public bool IsAccessTokenValid()
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(_accessToken))
                return false;
            if (!_accessTokenExpiresAtUtc.HasValue)
                return true;
            return DateTimeOffset.UtcNow < _accessTokenExpiresAtUtc.Value.AddSeconds(-RefreshBufferSeconds);
        }
    }

    public bool CanRefresh()
    {
        lock (_sync)
        {
            return !string.IsNullOrWhiteSpace(_refreshToken);
        }
    }

    public void UpdateFromTokenResponse(MercadoLivreTokenResponse response)
    {
        lock (_sync)
        {
            _accessToken = response.AccessToken;
            if (!string.IsNullOrWhiteSpace(response.RefreshToken))
                _refreshToken = response.RefreshToken;

            var seconds = response.ExpiresIn > 0 ? response.ExpiresIn : DefaultExpiresInSeconds;
            _accessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(seconds);
        }
    }

    public void SetAccessToken(string accessToken, TimeSpan? validFor = null)
    {
        lock (_sync)
        {
            _accessToken = accessToken;
            var ttl = validFor ?? TimeSpan.FromSeconds(DefaultExpiresInSeconds);
            _accessTokenExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl);
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return null;
    }

    /// <summary>
    /// Aceita Unix (segundos UTC), número em string, ou data ISO 8601.
    /// </summary>
    private static DateTimeOffset? ParseExpiresAt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        raw = raw.Trim();
        if (long.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var unix))
            return DateTimeOffset.FromUnixTimeSeconds(unix);

        if (DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var dto))
            return dto.ToUniversalTime();

        return null;
    }
}
