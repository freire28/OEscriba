using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCOEWeb.Services;

/// <summary>
/// Raiz da resposta JSON de <c>notas.fiscais.pesquisa.php</c>.
/// </summary>
public class TinyNotasFiscaisPesquisaRoot
{
    [JsonPropertyName("retorno")]
    public TinyNotasFiscaisPesquisaRetorno? Retorno { get; set; }
}

/// <summary>
/// Elemento <c>retorno</c> da pesquisa de notas fiscais.
/// </summary>
public class TinyNotasFiscaisPesquisaRetorno
{
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

    [JsonPropertyName("notas_fiscais")]
    public List<TinyNotaFiscalItemWrapper>? NotasFiscais { get; set; }

    [JsonPropertyName("erros")]
    public List<TinyNotasFiscaisPesquisaErro>? Erros { get; set; }
}

public class TinyNotasFiscaisPesquisaErro
{
    [JsonPropertyName("erro")]
    public string? Erro { get; set; }
}

public class TinyNotaFiscalItemWrapper
{
    [JsonPropertyName("nota_fiscal")]
    public TinyNotaFiscalPesquisa? NotaFiscal { get; set; }
}

/// <summary>
/// Nota fiscal retornada na pesquisa (estrutura conforme API Tiny).
/// </summary>
public class TinyNotaFiscalPesquisa
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? Id { get; set; }

    [JsonPropertyName("tipo")]
    public string? Tipo { get; set; }

    [JsonPropertyName("serie")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? Serie { get; set; }

    [JsonPropertyName("numero")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? Numero { get; set; }

    [JsonPropertyName("numero_ecommerce")]
    public string? NumeroEcommerce { get; set; }

    [JsonPropertyName("data_emissao")]
    public string? DataEmissao { get; set; }

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("cliente")]
    public TinyNotaFiscalClientePesquisa? Cliente { get; set; }

    [JsonPropertyName("endereco_entrega")]
    public TinyNotaFiscalEnderecoEntregaPesquisa? EnderecoEntrega { get; set; }

    [JsonPropertyName("transportador")]
    public TinyNotaFiscalTransportadorPesquisa? Transportador { get; set; }

    [JsonPropertyName("valor")]
    [JsonConverter(typeof(TinyJsonDecimalNullableConverter))]
    public decimal? Valor { get; set; }

    [JsonPropertyName("valor_produtos")]
    [JsonConverter(typeof(TinyJsonDecimalNullableConverter))]
    public decimal? ValorProdutos { get; set; }

    [JsonPropertyName("valor_frete")]
    [JsonConverter(typeof(TinyJsonDecimalNullableConverter))]
    public decimal? ValorFrete { get; set; }

    [JsonPropertyName("id_vendedor")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? IdVendedor { get; set; }

    [JsonPropertyName("nome_vendedor")]
    public string? NomeVendedor { get; set; }

    [JsonPropertyName("situacao")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? Situacao { get; set; }

    [JsonPropertyName("chave_acesso")]
    public string? ChaveAcesso { get; set; }

    [JsonPropertyName("descricao_situacao")]
    public string? DescricaoSituacao { get; set; }

    [JsonPropertyName("id_forma_frete")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? IdFormaFrete { get; set; }

    [JsonPropertyName("id_forma_envio")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? IdFormaEnvio { get; set; }

    [JsonPropertyName("codigo_rastreamento")]
    public string? CodigoRastreamento { get; set; }

    [JsonPropertyName("url_rastreamento")]
    public string? UrlRastreamento { get; set; }
}

public class TinyNotaFiscalClientePesquisa
{
    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("tipo_pessoa")]
    public string? TipoPessoa { get; set; }

    [JsonPropertyName("cpf_cnpj")]
    public string? CpfCnpj { get; set; }

    [JsonPropertyName("ie")]
    public string? Ie { get; set; }

    [JsonPropertyName("endereco")]
    public string? Endereco { get; set; }

    [JsonPropertyName("numero")]
    public string? Numero { get; set; }

    [JsonPropertyName("complemento")]
    public string? Complemento { get; set; }

    [JsonPropertyName("bairro")]
    public string? Bairro { get; set; }

    [JsonPropertyName("cep")]
    public string? Cep { get; set; }

    [JsonPropertyName("cidade")]
    public string? Cidade { get; set; }

    [JsonPropertyName("uf")]
    public string? Uf { get; set; }

    [JsonPropertyName("fone")]
    public string? Fone { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

public class TinyNotaFiscalEnderecoEntregaPesquisa
{
    [JsonPropertyName("tipo_pessoa")]
    public string? TipoPessoa { get; set; }

    [JsonPropertyName("cpf_cnpj")]
    public string? CpfCnpj { get; set; }

    [JsonPropertyName("endereco")]
    public string? Endereco { get; set; }

    [JsonPropertyName("numero")]
    public string? Numero { get; set; }

    [JsonPropertyName("complemento")]
    public string? Complemento { get; set; }

    [JsonPropertyName("bairro")]
    public string? Bairro { get; set; }

    [JsonPropertyName("cep")]
    public string? Cep { get; set; }

    [JsonPropertyName("cidade")]
    public string? Cidade { get; set; }

    [JsonPropertyName("uf")]
    public string? Uf { get; set; }

    [JsonPropertyName("fone")]
    public string? Fone { get; set; }

    [JsonPropertyName("nome_destinatario")]
    public string? NomeDestinatario { get; set; }
}

public class TinyNotaFiscalTransportadorPesquisa
{
    [JsonPropertyName("nome")]
    public string? Nome { get; set; }
}

/// <summary>
/// Opções de deserialização para pesquisa de notas fiscais.
/// </summary>
public static class TinyNotasFiscaisPesquisaJsonOptions
{
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
