using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCOEWeb.Services;

/// <summary>
/// Raiz da resposta JSON de <c>produtos.pesquisa.php</c>.
/// </summary>
public class TinyProdutosPesquisaRoot
{
    [JsonPropertyName("retorno")]
    public TinyProdutosPesquisaRetorno? Retorno { get; set; }
}

/// <summary>
/// Elemento <c>retorno</c> da pesquisa de produtos.
/// </summary>
public class TinyProdutosPesquisaRetorno
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("pagina")]
    [JsonConverter(typeof(TinyJsonIntNullableConverter))]
    public int? Pagina { get; set; }

    [JsonPropertyName("numero_paginas")]
    [JsonConverter(typeof(TinyJsonIntNullableConverter))]
    public int? NumeroPaginas { get; set; }

    [JsonPropertyName("produtos")]
    public List<TinyProdutoPesquisaWrapper>? Produtos { get; set; }

    [JsonPropertyName("erros")]
    public List<TinyProdutosPesquisaErro>? Erros { get; set; }
}

public class TinyProdutosPesquisaErro
{
    [JsonPropertyName("erro")]
    public string? Erro { get; set; }
}

/// <summary>
/// Cada item da lista <c>produtos</c>: <c>{ "produto": { ... } }</c>.
/// </summary>
public class TinyProdutoPesquisaWrapper
{
    [JsonPropertyName("produto")]
    public TinyProdutoPesquisa? Produto { get; set; }
}

/// <summary>
/// Dados do produto em <c>produtos.pesquisa.php</c>.
/// </summary>
public class TinyProdutoPesquisa
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? Id { get; set; }

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("preco")]
    [JsonConverter(typeof(TinyJsonDecimalNullableConverter))]
    public decimal? Preco { get; set; }

    [JsonPropertyName("preco_custo")]
    [JsonConverter(typeof(TinyJsonDecimalNullableConverter))]
    public decimal? PrecoCusto { get; set; }

    /// <summary>Ex.: <c>A</c> ativo, <c>I</c> inativo (conforme documentação Tiny).</summary>
    [JsonPropertyName("situacao")]
    public string? Situacao { get; set; }
}

/// <summary>
/// Opções de deserialização para <c>produtos.pesquisa.php</c>.
/// </summary>
public static class TinyProdutosPesquisaJsonOptions
{
    public static JsonSerializerOptions Default => TinyPedidosPesquisaJsonOptions.Default;
}
