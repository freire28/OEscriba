using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCOEWeb.Services;

/// <summary>
/// Raiz da resposta JSON de <c>pedidos.pesquisa.php</c>.
/// </summary>
public class TinyPedidosPesquisaRoot
{
    [JsonPropertyName("retorno")]
    public TinyPedidosPesquisaRetorno? Retorno { get; set; }
}

/// <summary>
/// Elemento <c>retorno</c> da pesquisa de pedidos.
/// </summary>
public class TinyPedidosPesquisaRetorno
{
    /// <summary>Status de processamento (a API pode enviar como número ou texto).</summary>
    [JsonPropertyName("status_processamento")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? StatusProcessamento { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("pagina")]
    [JsonConverter(typeof(TinyJsonIntNullableConverter))]
    public int? Pagina { get; set; }

    [JsonPropertyName("numero_paginas")]
    [JsonConverter(typeof(TinyJsonIntNullableConverter))]
    public int? NumeroPaginas { get; set; }

    [JsonPropertyName("pedidos")]
    public List<TinyPedidoItemWrapper>? Pedidos { get; set; }

    [JsonPropertyName("erros")]
    public List<TinyPedidosPesquisaErro>? Erros { get; set; }
}

public class TinyPedidosPesquisaErro
{
    [JsonPropertyName("erro")]
    public string? Erro { get; set; }
}

/// <summary>
/// Cada item da lista <c>pedidos</c>: <c>{ "pedido": { ... } }</c>.
/// </summary>
public class TinyPedidoItemWrapper
{
    [JsonPropertyName("pedido")]
    public TinyPedidoPesquisa? Pedido { get; set; }
}

/// <summary>
/// Dados do pedido retornados na pesquisa (conforme API Tiny).
/// </summary>
public class TinyPedidoPesquisa
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? Id { get; set; }

    [JsonPropertyName("numero")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? Numero { get; set; }

    [JsonPropertyName("numero_ecommerce")]
    public string? NumeroEcommerce { get; set; }

    [JsonPropertyName("data_pedido")]
    public string? DataPedido { get; set; }

    [JsonPropertyName("data_prevista")]
    public string? DataPrevista { get; set; }

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("valor")]
    [JsonConverter(typeof(TinyJsonDecimalNullableConverter))]
    public decimal? Valor { get; set; }

    [JsonPropertyName("id_vendedor")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? IdVendedor { get; set; }

    [JsonPropertyName("nome_vendedor")]
    public string? NomeVendedor { get; set; }

    [JsonPropertyName("situacao")]
    public string? Situacao { get; set; }

    [JsonPropertyName("codigo_rastreamento")]
    public string? CodigoRastreamento { get; set; }

    [JsonPropertyName("url_rastreamento")]
    public string? UrlRastreamento { get; set; }
}

/// <summary>
/// Opções de deserialização compartilhadas para a pesquisa de pedidos.
/// </summary>
public static class TinyPedidosPesquisaJsonOptions
{
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>
/// Aceita string ou número JSON em propriedade <see cref="string"/>.
/// </summary>
public sealed class TinyJsonStringOrNumberReadConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l)
                ? l.ToString(CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.Null => null,
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value);
    }
}

/// <summary>
/// Aceita número JSON ou string numérica para <see cref="int"/>.
/// </summary>
public sealed class TinyJsonIntNullableConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var i))
                    return i;
                if (reader.TryGetInt64(out var l))
                    return checked((int)l);
                return (int)reader.GetDouble();
            case JsonTokenType.String:
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    return null;
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var inv))
                    return inv;
                if (int.TryParse(s, NumberStyles.Integer, new CultureInfo("pt-BR"), out var br))
                    return br;
                return null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteNumberValue(value.Value);
    }
}

/// <summary>
/// Aceita número JSON ou string numérica para <see cref="decimal"/>.
/// </summary>
public sealed class TinyJsonDecimalNullableConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                if (reader.TryGetDecimal(out var d))
                    return d;
                return (decimal)reader.GetDouble();
            case JsonTokenType.String:
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    return null;
                if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var inv))
                    return inv;
                if (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("pt-BR"), out var br))
                    return br;
                return null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteNumberValue(value.Value);
    }
}
