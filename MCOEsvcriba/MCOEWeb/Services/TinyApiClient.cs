using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MCOEWeb.Services;

public class TinyApiClient
{
    private readonly HttpClient _httpClient;
    private readonly int _delayBetweenRequestsMs;

    public TinyApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _delayBetweenRequestsMs = Math.Max(0, configuration.GetValue("TinyApi:DelayBetweenRequestsMs", 400));

        var baseUrl = configuration["TinyApi:BaseUrl"] ?? "https://api.tiny.com.br/api2/";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Envia uma requisição POST application/x-www-form-urlencoded para um endpoint da API Tiny.
    /// </summary>
    /// <param name="endpoint">Ex.: "notas.fiscais.pesquisa.php"</param>
    /// <param name="parameters">Parâmetros do formulário (incluindo token, formato, filtros, etc).</param>
    public async Task<string> PostFormAsync(string endpoint, IDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        using var content = new FormUrlEncodedContent(parameters);

        try
        {
            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        finally
        {
            if (_delayBetweenRequestsMs > 0)
                await Task.Delay(_delayBetweenRequestsMs, cancellationToken);
        }
    }

    /// <summary>
    /// Exemplo específico para pesquisa de notas fiscais.
    /// </summary>
    public Task<string> PesquisarNotasFiscaisAsync(string token, IDictionary<string, string>? filtros = null, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, string>
        {
            ["token"] = token,
            ["formato"] = "json"
        };

        if (filtros is not null)
        {
            foreach (var kvp in filtros)
            {
                data[kvp.Key] = kvp.Value;
            }
        }

        return PostFormAsync("notas.fiscais.pesquisa.php", data, cancellationToken);
    }

    /// <summary>
    /// Lista notas fiscais (<c>notas.fiscais.pesquisa.php</c>) e deserializa para <see cref="TinyNotasFiscaisPesquisaRoot"/>.
    /// Filtros comuns: <c>pagina</c>, <c>dataInicial</c>/<c>dataFinal</c> (dd/mm/yyyy), etc., conforme documentação Tiny.
    /// </summary>
    public async Task<TinyNotasFiscaisPesquisaRoot> ListarNotasFiscaisAsync(
        string token,
        IDictionary<string, string>? filtros = null,
        CancellationToken cancellationToken = default)
    {
        var json = await PesquisarNotasFiscaisAsync(token, filtros, cancellationToken);
        return JsonSerializer.Deserialize<TinyNotasFiscaisPesquisaRoot>(json, TinyNotasFiscaisPesquisaJsonOptions.Default)
            ?? throw new InvalidOperationException("Resposta de notas fiscais inválida.");
    }

    /// <summary>
    /// Pesquisa notas fiscais por intervalo de datas (<c>dataInicial</c>/<c>dataFinal</c> em dd/mm/yyyy) e página.
    /// </summary>
    public Task<TinyNotasFiscaisPesquisaRoot> ListarNotasFiscaisPorPeriodoAsync(
        string token,
        string dataInicialDdMmYyyy,
        string dataFinalDdMmYyyy,
        int pagina = 1,
        CancellationToken cancellationToken = default)
    {
        var filtros = new Dictionary<string, string>
        {
            ["dataInicial"] = dataInicialDdMmYyyy,
            ["dataFinal"] = dataFinalDdMmYyyy,
            ["pagina"] = pagina.ToString(CultureInfo.InvariantCulture)
        };
        return ListarNotasFiscaisAsync(token, filtros, cancellationToken);
    }

    /// <summary>
    /// Pesquisa notas fiscais pelo número do pedido no e-commerce/sistema.
    /// Parâmetro da API Tiny (form): <c>numeroEcommerce</c> — valor = <c>numero_ecommerce</c> do pedido/JSON.
    /// </summary>
    public Task<TinyNotasFiscaisPesquisaRoot> ListarNotasFiscaisPorNumeroEcommerceAsync(
        string token,
        string numeroEcommerce,
        int pagina = 1,
        CancellationToken cancellationToken = default)
    {
        var filtros = new Dictionary<string, string>
        {
            ["numeroEcommerce"] = numeroEcommerce.Trim(),
            ["pagina"] = pagina.ToString(CultureInfo.InvariantCulture)
        };
        return ListarNotasFiscaisAsync(token, filtros, cancellationToken);
    }

    /// <summary>
    /// Pesquisa de pedidos: pedidos.pesquisa.php
    /// Filtros comuns: dataInicial, dataFinal (dd/mm/yyyy), pagina, sort (ASC|DESC), situacao, etc.
    /// </summary>
    public Task<string> PesquisarPedidosAsync(string token, IDictionary<string, string>? filtros = null, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, string>
        {
            ["token"] = token,
            ["formato"] = "json"
        };

        if (filtros is not null)
        {
            foreach (var kvp in filtros)
            {
                data[kvp.Key] = kvp.Value;
            }
        }

        return PostFormAsync("pedidos.pesquisa.php", data, cancellationToken);
    }

    /// <summary>
    /// Pesquisa pedidos por intervalo de datas de cadastro (<c>dataInicial</c>/<c>dataFinal</c> em dd/mm/yyyy)
    /// e deserializa o JSON para <see cref="TinyPedidosPesquisaRoot"/>.
    /// </summary>
    public async Task<TinyPedidosPesquisaRoot> BuscarPedidosPorPeriodoAsync(
        string token,
        string dataInicialDdMmYyyy,
        string dataFinalDdMmYyyy,
        int pagina = 1,
        string sort = "DESC",
        CancellationToken cancellationToken = default)
    {
        var filtros = new Dictionary<string, string>
        {
            ["dataInicial"] = dataInicialDdMmYyyy,
            ["dataFinal"] = dataFinalDdMmYyyy,
            ["pagina"] = pagina.ToString(CultureInfo.InvariantCulture),
            ["sort"] = string.IsNullOrWhiteSpace(sort) ? "DESC" : sort.Trim().ToUpperInvariant()
        };

        var json = await PesquisarPedidosAsync(token, filtros, cancellationToken);
        return JsonSerializer.Deserialize<TinyPedidosPesquisaRoot>(json, TinyPedidosPesquisaJsonOptions.Default)
            ?? throw new InvalidOperationException("Resposta de pedidos inválida.");
    }

    /// <summary>
    /// Pesquisa de produtos (<c>produtos.pesquisa.php</c>): POST <c>application/x-www-form-urlencoded</c>
    /// com <c>token</c>, <c>formato=json</c> e parâmetros opcionais (<c>pesquisa</c>, <c>pagina</c>, etc.).
    /// </summary>
    public Task<string> PesquisarProdutosAsync(string token, IDictionary<string, string>? filtros = null, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, string>
        {
            ["token"] = token,
            ["formato"] = "json"
        };

        if (filtros is not null)
        {
            foreach (var kvp in filtros)
            {
                data[kvp.Key] = kvp.Value;
            }
        }

        return PostFormAsync("produtos.pesquisa.php", data, cancellationToken);
    }

    /// <summary>
    /// Chama <c>produtos.pesquisa.php</c> e deserializa o JSON para <see cref="TinyProdutosPesquisaRoot"/>.
    /// Sem filtros extras equivale a token + formato json (listagem conforme API).
    /// </summary>
    public async Task<TinyProdutosPesquisaRoot> ListarProdutosAsync(
        string token,
        IDictionary<string, string>? filtros = null,
        CancellationToken cancellationToken = default)
    {
        var json = await PesquisarProdutosAsync(token, filtros, cancellationToken);
        return JsonSerializer.Deserialize<TinyProdutosPesquisaRoot>(json, TinyProdutosPesquisaJsonOptions.Default)
            ?? throw new InvalidOperationException("Resposta de produtos inválida.");
    }

    /// <summary>
    /// Obter produto específico: produto.obter.php
    /// </summary>
    /// <param name="token">Token da API Tiny.</param>
    /// <param name="id">Id do produto (como no seu curl).</param>
    public Task<string> ObterProdutoAsync(string token, string id, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, string>
        {
            ["token"] = token,
            ["formato"] = "json",
            ["id"] = id
        };

        return PostFormAsync("produto.obter.php", data, cancellationToken);
    }

    /// <summary>
    /// Obter pedido específico: pedido.obter.php (corpo JSON bruto).
    /// </summary>
    /// <param name="token">Token da API Tiny.</param>
    /// <param name="id">Id do pedido na Olist/Tiny (parâmetro <c>id</c> do formulário).</param>
    public Task<string> ObterPedidoAsync(string token, string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Informe o id do pedido.", nameof(id));

        var data = new Dictionary<string, string>
        {
            ["token"] = token,
            ["formato"] = "json",
            ["id"] = id.Trim()
        };

        return PostFormAsync("pedido.obter.php", data, cancellationToken);
    }

    /// <summary>
    /// Obtém o detalhe de um pedido (<c>pedido.obter.php</c>) pelo id e deserializa o JSON.
    /// </summary>
    /// <param name="token">Token da API Tiny.</param>
    /// <param name="idPedido">Id do pedido na Olist/Tiny.</param>
    public async Task<TinyPedidoObterRoot> ObterPedidoDetalheAsync(
        string token,
        string idPedido,
        CancellationToken cancellationToken = default)
    {
        var json = await ObterPedidoAsync(token, idPedido, cancellationToken);
        return JsonSerializer.Deserialize<TinyPedidoObterRoot>(json, TinyPedidosPesquisaJsonOptions.Default)
            ?? throw new InvalidOperationException("Resposta de pedido.obter inválida.");
    }

    /// <inheritdoc cref="ObterPedidoDetalheAsync(string, string, CancellationToken)" />
    public Task<TinyPedidoObterRoot> ObterPedidoDetalheAsync(
        string token,
        long idPedido,
        CancellationToken cancellationToken = default)
        => ObterPedidoDetalheAsync(token, idPedido.ToString(CultureInfo.InvariantCulture), cancellationToken);

    /// <summary>
    /// Obter nota fiscal específica: nota.fiscal.obter.php
    /// </summary>
    /// <param name="token">Token da API Tiny.</param>
    /// <param name="id">Id da nota fiscal.</param>
    public Task<string> ObterNotaFiscalAsync(string token, string id, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, string>
        {
            ["token"] = token,
            ["formato"] = "json",
            ["id"] = id
        };

        return PostFormAsync("nota.fiscal.obter.php", data, cancellationToken);
    }

    /// <summary>
    /// Obtém o XML da NF-e (wrapper <c>retorno</c> com <c>xml_nfe</c> ou erros): <c>nota.fiscal.obter.xml.php</c>.
    /// Documentação: <see href="https://tiny.com.br/api-docs/api2-notas-fiscais-obter-xml" />.
    /// </summary>
    /// <param name="token">Token da API Tiny.</param>
    /// <param name="id">Id da nota fiscal no Tiny.</param>
    /// <returns>Corpo da resposta em XML (UTF-8).</returns>
    public Task<string> ObterNotaFiscalXmlAsync(string token, string id, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, string>
        {
            ["token"] = token,
            ["id"] = id
        };

        return PostFormAsync("nota.fiscal.obter.xml.php", data, cancellationToken);
    }
}

