using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCOEWeb.Services;

/// <summary>
/// Cliente para a API do Mercado Livre (Mercado Libre).
/// Obtém e renova o access token via OAuth 2.0 e permite chamadas autenticadas.
/// </summary>
public class MercadoLivreApiClient
{
    private const string BaseUrl = "https://api.mercadolibre.com";
    private const string AuthBaseUrl = "https://auth.mercadolivre.com.br";

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly SemaphoreSlim TokenRefreshLock = new(1, 1);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IMercadoLivreTokenStore _tokenStore;

    public MercadoLivreApiClient(HttpClient httpClient, IConfiguration configuration, IMercadoLivreTokenStore tokenStore)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _tokenStore = tokenStore;
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Client ID da aplicação (App ID) no Mercado Livre.
    /// </summary>
    private string ClientId => _configuration["MercadoLivre:ClientId"] ?? "";

    /// <summary>
    /// Client Secret da aplicação no Mercado Livre.
    /// </summary>
    private string ClientSecret => _configuration["MercadoLivre:ClientSecret"] ?? "";

    /// <summary>
    /// URL de redirecionamento registrada na aplicação (ex: https://seusite.com/callback).
    /// </summary>
    public string RedirectUri => _configuration["MercadoLivre:RedirectUri"] ?? "";

    /// <summary>
    /// Gera a URL para o usuário autorizar a aplicação e obter o código.
    /// Redirecione o usuário para esta URL; após autorizar, o ML redireciona para RedirectUri com ?code=XXX.
    /// </summary>
    public string GetAuthorizationUrl(string? state = null)
    {
        var qs = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri
        };
        if (!string.IsNullOrWhiteSpace(state))
            qs["state"] = state;

        var query = string.Join("&", qs.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"{AuthBaseUrl}/authorization?{query}";
    }

    /// <summary>
    /// Dados básicos do usuário autenticado (<c>GET /users/me</c>).
    /// </summary>
    public Task<MercadoLivreUserProfile?> ObterUsuarioAutenticadoAsync(CancellationToken cancellationToken = default)
        => GetAsync<MercadoLivreUserProfile>("users/me", cancellationToken);

    /// <summary>
    /// Lista pedidos em que o usuário é <b>vendedor</b> (<c>GET /orders/search?seller=...</c>).
    /// </summary>
    /// <param name="sellerId">ID do vendedor (normalmente o mesmo de <see cref="MercadoLivreUserProfile.Id"/> do token).</param>
    /// <param name="offset">Paginação (padrão ML: até 50 por página).</param>
    /// <param name="limit">Entre 1 e 50.</param>
    /// <param name="idPedidoFiltro">Opcional: filtra por ID do pedido (<c>q</c> na API).</param>
    public Task<string> BuscarPedidosPorVendedorAsync(
        long sellerId,
        int offset = 0,
        int limit = 50,
        string? idPedidoFiltro = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 50);
        var parts = new List<string>
        {
            $"seller={sellerId}",
            $"offset={offset}",
            $"limit={limit}"
        };
        if (!string.IsNullOrWhiteSpace(idPedidoFiltro))
            parts.Add($"q={Uri.EscapeDataString(idPedidoFiltro.Trim())}");

        return GetAsync($"orders/search?{string.Join("&", parts)}", cancellationToken);
    }

    /// <summary>
    /// Indica se há access token válido ou <c>refresh_token</c> para renovar antes das chamadas à API.
    /// </summary>
    public bool TemTokenParaChamadasApi() =>
        _tokenStore.IsAccessTokenValid() || _tokenStore.CanRefresh();

    /// <summary>
    /// Obtém um pedido pelo ID (<c>GET /orders/{id}</c>), ex.: <c>2000015371733006</c>.
    /// </summary>
    public Task<string> ObterPedidoPorIdAsync(string idPedido, CancellationToken cancellationToken = default)
    {
        var id = (idPedido ?? "").Trim();
        if (id.Length == 0 || !id.All(char.IsAsciiDigit))
            throw new ArgumentException("Informe o ID numérico do pedido.", nameof(idPedido));

        return GetAsync($"orders/{id}", cancellationToken);
    }

    /// <summary>
    /// <c>GET /orders/{id}</c> sem lançar em 404; usado após consolidar NF-e para enriquecer taxas/frete.
    /// Usa o mesmo fluxo de token que <see cref="GetAsync"/> (renova com <c>refresh_token</c> quando expira, ex.: 21600 s).
    /// </summary>
    public async Task<string?> TryObterPedidoPorIdAsync(string idPedido, CancellationToken cancellationToken = default)
    {
        var id = (idPedido ?? "").Trim();
        if (id.Length == 0 || !id.All(char.IsAsciiDigit))
            return null;

        await EnsureValidAccessTokenAsync(cancellationToken);
        var bearer = _tokenStore.AccessToken ?? throw new InvalidOperationException("Access token ausente após validação.");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"orders/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// <c>GET /packs/{packId}</c> sem lançar em 404; usado após <c>/orders</c> com o mesmo <c>numero_ecommerce</c> quando o valor é id de pack.
    /// </summary>
    public async Task<string?> TryObterPackPorIdAsync(string packId, CancellationToken cancellationToken = default)
    {
        var id = (packId ?? "").Trim();
        if (id.Length == 0 || !id.All(char.IsAsciiDigit))
            return null;

        await EnsureValidAccessTokenAsync(cancellationToken);
        var bearer = _tokenStore.AccessToken ?? throw new InvalidOperationException("Access token ausente após validação.");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"packs/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Extrai o primeiro <c>orders[].id</c> do JSON de <c>GET /packs/{id}</c>.
    /// </summary>
    private static string? ExtrairPrimeiroOrderIdDoPackJson(string? packJson)
    {
        if (string.IsNullOrWhiteSpace(packJson))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(packJson);
            if (!doc.RootElement.TryGetProperty("orders", out var orders) || orders.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var o in orders.EnumerateArray())
            {
                if (!o.TryGetProperty("id", out var idEl))
                    continue;
                if (idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt64(out var n))
                    return n.ToString(CultureInfo.InvariantCulture);
                var s = idEl.GetString()?.Trim();
                if (!string.IsNullOrEmpty(s) && s.All(char.IsAsciiDigit))
                    return s;
            }
        }
        catch
        {
            return null;
        }
        return null;
    }

    /// <summary>
    /// Obtém valores para <c>TAXA_MARKETPLACE = order_cost - list_cost - sale_fee</c> (shipment + pedido ML).
    /// Fluxo: (1) <c>GET /orders/{numero_ecommerce}</c>; (2) se não achar, <c>GET /packs/{numero_ecommerce}</c>;
    /// (3) lê <c>orders[].id</c>; (4) <c>GET /orders/{id}</c>; (5) <c>GET /shipments/{id}</c> e opcionalmente <c>/costs</c>.
    /// </summary>
    public async Task<(decimal? SaleFee, decimal? OrderCost, decimal? ListCost, decimal? ShippingCost)> ObterCustosDoPedidoAsync(
        string numeroEcommerceMercadoLivre,
        CancellationToken cancellationToken = default)
    {
        var id = (numeroEcommerceMercadoLivre ?? "").Trim();
        if (id.Length == 0 || !id.All(char.IsAsciiDigit))
            return (null, null, null, null);

        var json = await TryObterPedidoPorIdAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            var packJson = await TryObterPackPorIdAsync(id, cancellationToken);
            var orderFromPack = ExtrairPrimeiroOrderIdDoPackJson(packJson);
            if (!string.IsNullOrEmpty(orderFromPack))
                json = await TryObterPedidoPorIdAsync(orderFromPack, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(json))
            return (null, null, null, null);

        return await CalcularCustosDeJsonPedidoAsync(json, cancellationToken);
    }

    private async Task<(decimal? SaleFee, decimal? OrderCost, decimal? ListCost, decimal? ShippingCost)> CalcularCustosDeJsonPedidoAsync(
        string json,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var saleFee = ExtrairSaleFeeDoPedido(root);

        decimal? orderCost = null;
        decimal? listCost = null;
        decimal? shippingCostField = null;

        var shipId = root.TryGetProperty("shipping", out var shipping)
            ? TryGetInt64(shipping, "id")
            : null;

        if (shipId is { } sid && sid > 0)
        {
            var shipmentJson = await TryGetShipmentJsonAsync(sid, formatoNovo: true, cancellationToken)
                ?? await TryGetShipmentJsonAsync(sid, formatoNovo: false, cancellationToken);
            if (!string.IsNullOrWhiteSpace(shipmentJson))
            {
                ExtrairCustosDoShipmentJson(shipmentJson, ref orderCost, ref listCost, ref shippingCostField);
            }

            if (!listCost.HasValue)
                listCost ??= await ObterListCostDeShipmentCostsAsync(sid, cancellationToken);
        }

        return (saleFee, orderCost, listCost, shippingCostField);
    }

    private static decimal? ExtrairSaleFeeDoPedido(JsonElement root)
    {
        if (root.TryGetProperty("sale_fee", out var saleFeeEl))
        {
            if (saleFeeEl.ValueKind == JsonValueKind.Number || saleFeeEl.ValueKind == JsonValueKind.String)
            {
                var direct = JsonElementToDecimal(saleFeeEl);
                if (direct is not null)
                    return direct;
            }

            if (saleFeeEl.ValueKind == JsonValueKind.Object)
            {
                var fromObj = JsonTryDecimalNames(saleFeeEl, "amount", "value", "total", "fee", "raw_amount");
                if (fromObj is not null)
                    return fromObj;
            }
        }

        if (!root.TryGetProperty("order_items", out var orderItems) || orderItems.ValueKind != JsonValueKind.Array)
            return ExtrairMarketplaceFeeDosPagamentos(root);

        decimal sum = 0;
        var any = false;
        foreach (var item in orderItems.EnumerateArray())
        {
            decimal? part = null;
            if (item.TryGetProperty("sale_fee", out var itemFee))
            {
                if (itemFee.ValueKind is JsonValueKind.Number or JsonValueKind.String)
                    part = JsonElementToDecimal(itemFee);
                else if (itemFee.ValueKind == JsonValueKind.Object)
                    part = JsonTryDecimalNames(itemFee, "amount", "value", "total", "fee", "raw_amount");
            }

            part ??= JsonTryDecimalNames(item, "marketplace_fee", "marketplaceFee");

            if (part is not null)
            {
                sum += part.Value;
                any = true;
            }
        }

        if (any)
            return sum;

        return ExtrairMarketplaceFeeDosPagamentos(root);
    }

    private static decimal? ExtrairMarketplaceFeeDosPagamentos(JsonElement root)
    {
        var raiz = JsonTryDecimalNames(root, "marketplace_fee", "marketplaceFee");
        if (raiz is not null)
            return raiz;

        if (!root.TryGetProperty("payments", out var payments) || payments.ValueKind != JsonValueKind.Array)
            return null;

        decimal sum = 0;
        var any = false;
        foreach (var p in payments.EnumerateArray())
        {
            if (JsonTryDecimalNames(p, "marketplace_fee", "marketplaceFee") is { } mf)
            {
                sum += mf;
                any = true;
            }
        }

        return any ? sum : null;
    }

    private async Task<string?> TryGetAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await GetAsync(path, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// GET <c>/shipments/{id}</c>. Com <paramref name="formatoNovo"/> envia <c>x-format-new: true</c> (recomendado pelo ML).
    /// </summary>
    private async Task<string?> TryGetShipmentJsonAsync(long shipmentId, bool formatoNovo, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureValidAccessTokenAsync(cancellationToken);
            var bearer = _tokenStore.AccessToken ?? throw new InvalidOperationException("Access token ausente após validação.");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"shipments/{shipmentId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
         /*   if (formatoNovo)
                request.Headers.TryAddWithoutValidation("x-format-new", "true");
         */
            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// ML expõe custo de tabela do envio em <c>GET /shipments/{id}/costs</c> (<c>gross_amount</c>) quando o corpo principal não traz <c>list_cost</c>.
    /// </summary>
    private async Task<decimal?> ObterListCostDeShipmentCostsAsync(long shipmentId, CancellationToken cancellationToken)
    {
        var json = await TryGetAsync($"shipments/{shipmentId}/costs", cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            return JsonTryDecimalNames(r, "gross_amount", "list_cost", "listCost");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extrai custos do JSON bruto de /shipments/{id}.
    /// Mantém fallback textual para casos em que o payload venha em formato fora do esperado.
    /// </summary>
    private static void ExtrairCustosDoShipmentJson(
        string shipmentJson,
        ref decimal? orderCost,
        ref decimal? listCost,
        ref decimal? shippingCostField)
    {      
        orderCost ??= TryReadDecimalFromJsonText(shipmentJson, "order_cost");
        listCost ??= TryReadDecimalFromJsonText(shipmentJson, "list_cost");
        shippingCostField ??= TryReadDecimalFromJsonText(shipmentJson, "cost");
    }

    private static decimal? TryReadDecimalFromJsonText(string json, string fieldName)
    {
        var pattern = $"\\\"{Regex.Escape(fieldName)}\\\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)";
        var match = Regex.Match(json, pattern, RegexOptions.CultureInvariant);
        if (!match.Success)
            return null;

        return decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    private static decimal? JsonTryDecimalNames(JsonElement el, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            var v = JsonGetDecimal(el, name);
            if (v.HasValue)
                return v;
        }

        foreach (var prop in el.EnumerateObject())
        {
            foreach (var name in propertyNames)
            {
                if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return JsonElementToDecimal(prop.Value);
            }
        }

        return null;
    }

    private static decimal? JsonGetDecimal(JsonElement el, string propertyName)
    {
        if (!el.TryGetProperty(propertyName, out var p))
            return null;
        return JsonElementToDecimal(p);
    }

    private static decimal? JsonElementToDecimal(JsonElement p) =>
        p.ValueKind switch
        {
            JsonValueKind.Number => p.TryGetDecimal(out var d) ? d : null,
            JsonValueKind.String => decimal.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var x)
                ? x
                : null,
            _ => null
        };

    private static long? TryGetInt64(JsonElement el, string propertyName)
    {
        if (!el.TryGetProperty(propertyName, out var p))
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var n))
            return n;
        if (p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n2))
            return n2;
        return null;
    }

    /// <summary>
    /// Obtém um pedido pelo ID e deserializa o JSON.
    /// </summary>
    public Task<T?> ObterPedidoPorIdAsync<T>(string idPedido, CancellationToken cancellationToken = default) where T : class
    {
        var id = (idPedido ?? "").Trim();
        if (id.Length == 0 || !id.All(char.IsAsciiDigit))
            throw new ArgumentException("Informe o ID numérico do pedido.", nameof(idPedido));

        return GetAsync<T>($"orders/{id}", cancellationToken);
    }

    /// <summary>
    /// Troca o código de autorização por access_token e refresh_token.
    /// Chame após o usuário autorizar e o ML redirecionar com ?code=XXX.
    /// </summary>
    public Task<MercadoLivreTokenResponse> GetTokenAsync(string code, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["scope"] = "offline_access"
        };

        return EnviarOAuthTokenAsync(body, cancellationToken);
    }

    /// <summary>
    /// Renova o <c>access_token</c> com <c>refresh_token</c>.
    /// Equivale ao POST <c>https://api.mercadolibre.com/oauth/token</c> com
    /// <c>application/x-www-form-urlencoded</c>: <c>grant_type=refresh_token</c>, <c>client_id</c>, <c>client_secret</c>, <c>refresh_token</c>.
    /// O ML devolve novo <c>access_token</c> e novo <c>refresh_token</c>; guarde os dois.
    /// </summary>
    public Task<MercadoLivreTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["refresh_token"] = refreshToken
        };

        return EnviarOAuthTokenAsync(body, cancellationToken);
    }

    /// <summary>
    /// Alias de <see cref="RefreshTokenAsync"/> para atualizar o token a partir do refresh.
    /// </summary>
    public Task<MercadoLivreTokenResponse> AtualizarTokenComRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        => RefreshTokenAsync(refreshToken, cancellationToken);

    private async Task<MercadoLivreTokenResponse> EnviarOAuthTokenAsync(
        Dictionary<string, string> formFields,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(formFields);
        using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/token") { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<MercadoLivreTokenResponse>(json, JsonReadOptions)
            ?? throw new InvalidOperationException("Resposta do /oauth/token inválida.");

        _tokenStore.UpdateFromTokenResponse(tokenResponse);
        return tokenResponse;
    }

    /// <summary>
    /// Define o access token a ser usado nas requisições (ex.: token colado na página de demo).
    /// Assume TTL padrão do ML (21600 s) para controle de expiração.
    /// </summary>
    public void SetAccessToken(string accessToken)
    {
        _tokenStore.SetAccessToken(accessToken, TimeSpan.FromSeconds(MercadoLivreTokenStore.DefaultExpiresInSeconds));
    }

    /// <summary>
    /// Retorna o access token atualmente em uso (se definido).
    /// </summary>
    public string? GetAccessToken() => _tokenStore.AccessToken;

    /// <summary>
    /// Requisição GET autenticada na API do Mercado Livre.
    /// path: ex. "users/me" ou "sites/MLB" (sem barra inicial).
    /// </summary>
    public async Task<string> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        await EnsureValidAccessTokenAsync(cancellationToken);
        var bearer = _tokenStore.AccessToken ?? throw new InvalidOperationException("Access token ausente após validação.");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Requisição GET autenticada com deserialização JSON.
    /// </summary>
    public async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default) where T : class
    {
        var json = await GetAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<T>(json, JsonReadOptions);
    }

    /// <summary>
    /// Requisição POST autenticada (body JSON).
    /// </summary>
    public async Task<string> PostAsync(string path, object? body = null, CancellationToken cancellationToken = default)
    {
        await EnsureValidAccessTokenAsync(cancellationToken);
        var bearer = _tokenStore.AccessToken ?? throw new InvalidOperationException("Access token ausente após validação.");
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private async Task EnsureValidAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_tokenStore.IsAccessTokenValid())
            return;

        if (!_tokenStore.CanRefresh())
        {
            throw new InvalidOperationException(
                "Nenhum access token válido e não há refresh_token. Configure MercadoLivre:AccessToken / MERCADOLIVRE_ACCESS_TOKEN " +
                "e MercadoLivre:RefreshToken / MERCADOLIVRE_REFRESH_TOKEN (e opcionalmente AccessTokenExpiresAtUtc / MERCADOLIVRE_ACCESS_TOKEN_EXPIRES_AT), " +
                "ou chame GetTokenAsync(code), RefreshTokenAsync(refreshToken) ou SetAccessToken(token).");
        }

        await TokenRefreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_tokenStore.IsAccessTokenValid())
                return;

            await RefreshTokenAsync(_tokenStore.RefreshToken!, cancellationToken);
        }
        finally
        {
            TokenRefreshLock.Release();
        }
    }
}

/// <summary>
/// Perfil mínimo retornado por <c>GET /users/me</c>.
/// </summary>
public class MercadoLivreUserProfile
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

/// <summary>
/// Resposta do endpoint /oauth/token (obtenção e refresh).
/// </summary>
public class MercadoLivreTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }
}
