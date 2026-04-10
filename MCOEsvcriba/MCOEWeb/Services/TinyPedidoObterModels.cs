using System.Text.Json.Serialization;

namespace MCOEWeb.Services;

/// <summary>
/// Raiz da resposta JSON de <c>pedido.obter.php</c>.
/// </summary>
public class TinyPedidoObterRoot
{
    [JsonPropertyName("retorno")]
    public TinyPedidoObterRetorno? Retorno { get; set; }
}

/// <summary>
/// Elemento <c>retorno</c> de <c>pedido.obter.php</c>.
/// </summary>
public class TinyPedidoObterRetorno
{
    [JsonPropertyName("status_processamento")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? StatusProcessamento { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("codigo_erro")]
    [JsonConverter(typeof(TinyJsonIntNullableConverter))]
    public int? CodigoErro { get; set; }

    [JsonPropertyName("erros")]
    public List<TinyPedidosPesquisaErro>? Erros { get; set; }

    [JsonPropertyName("pedido")]
    public TinyPedidoObter? Pedido { get; set; }
}

/// <summary>
/// Pedido completo retornado por <c>pedido.obter.php</c> (campos usuais; propriedades desconhecidas no JSON são ignoradas).
/// </summary>
public class TinyPedidoObter
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

    [JsonPropertyName("data_faturamento")]
    public string? DataFaturamento { get; set; }

    [JsonPropertyName("data_envio")]
    public string? DataEnvio { get; set; }

    [JsonPropertyName("data_entrega")]
    public string? DataEntrega { get; set; }

    [JsonPropertyName("valor")]
    [JsonConverter(typeof(TinyJsonDecimalNullableConverter))]
    public decimal? Valor { get; set; }

    [JsonPropertyName("situacao")]
    public string? Situacao { get; set; }

    [JsonPropertyName("cliente")]
    public TinyPedidoClienteObter? Cliente { get; set; }

    [JsonPropertyName("endereco_entrega")]
    public TinyPedidoEnderecoEntregaObter? EnderecoEntrega { get; set; }

    [JsonPropertyName("itens")]
    public List<TinyPedidoItemObterWrapper>? Itens { get; set; }

    [JsonPropertyName("condicao_pagamento")]
    public string? CondicaoPagamento { get; set; }

    [JsonPropertyName("forma_pagamento")]
    public string? FormaPagamento { get; set; }

    [JsonPropertyName("meio_pagamento")]
    public string? MeioPagamento { get; set; }

    [JsonPropertyName("valor_frete")]
    [JsonConverter(typeof(TinyJsonDecimalNullableConverter))]
    public decimal? ValorFrete { get; set; }

    [JsonPropertyName("valor_desconto")]
    [JsonConverter(typeof(TinyJsonDecimalNullableConverter))]
    public decimal? ValorDesconto { get; set; }

    [JsonPropertyName("outras_despesas")]
    [JsonConverter(typeof(TinyJsonDecimalNullableConverter))]
    public decimal? OutrasDespesas { get; set; }

    [JsonPropertyName("total_produtos")]
    [JsonConverter(typeof(TinyJsonDecimalNullableConverter))]
    public decimal? TotalProdutos { get; set; }

    [JsonPropertyName("total_pedido")]
    [JsonConverter(typeof(TinyJsonDecimalNullableConverter))]
    public decimal? TotalPedido { get; set; }

    [JsonPropertyName("id_vendedor")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? IdVendedor { get; set; }

    [JsonPropertyName("nome_vendedor")]
    public string? NomeVendedor { get; set; }

    [JsonPropertyName("codigo_rastreamento")]
    public string? CodigoRastreamento { get; set; }

    [JsonPropertyName("url_rastreamento")]
    public string? UrlRastreamento { get; set; }

    [JsonPropertyName("obs")]
    public string? Obs { get; set; }

    [JsonPropertyName("obs_interna")]
    public string? ObsInterna { get; set; }

    [JsonPropertyName("nome_transportador")]
    public string? NomeTransportador { get; set; }

    [JsonPropertyName("frete_por_conta")]
    public string? FretePorConta { get; set; }

    [JsonPropertyName("forma_frete")]
    public string? FormaFrete { get; set; }

    [JsonPropertyName("forma_envio")]
    public string? FormaEnvio { get; set; }

    [JsonPropertyName("numero_ordem_compra")]
    public string? NumeroOrdemCompra { get; set; }

    [JsonPropertyName("id_nota_fiscal")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? IdNotaFiscal { get; set; }

    [JsonPropertyName("deposito")]
    public string? Deposito { get; set; }
}

public class TinyPedidoClienteObter
{
    [JsonPropertyName("codigo")]
    public string? Codigo { get; set; }

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("nome_fantasia")]
    public string? NomeFantasia { get; set; }

    [JsonPropertyName("tipo_pessoa")]
    public string? TipoPessoa { get; set; }

    [JsonPropertyName("cpf_cnpj")]
    public string? CpfCnpj { get; set; }

    [JsonPropertyName("ie")]
    public string? Ie { get; set; }

    [JsonPropertyName("rg")]
    public string? Rg { get; set; }

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

    [JsonPropertyName("pais")]
    public string? Pais { get; set; }

    [JsonPropertyName("fone")]
    public string? Fone { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

public class TinyPedidoEnderecoEntregaObter
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

    [JsonPropertyName("ie")]
    public string? Ie { get; set; }
}

/// <summary>
/// Cada elemento de <c>itens</c>: <c>{ "item": { ... } }</c>.
/// </summary>
public class TinyPedidoItemObterWrapper
{
    [JsonPropertyName("item")]
    public TinyPedidoItemObter? Item { get; set; }
}

public class TinyPedidoItemObter
{
    [JsonPropertyName("id_produto")]
    [JsonConverter(typeof(TinyJsonStringOrNumberReadConverter))]
    public string? IdProduto { get; set; }

    [JsonPropertyName("codigo")]
    public string? Codigo { get; set; }

    [JsonPropertyName("descricao")]
    public string? Descricao { get; set; }

    [JsonPropertyName("unidade")]
    public string? Unidade { get; set; }

    [JsonPropertyName("quantidade")]
    [JsonConverter(typeof(TinyJsonDecimalNullableConverter))]
    public decimal? Quantidade { get; set; }

    [JsonPropertyName("valor_unitario")]
    [JsonConverter(typeof(TinyJsonDecimalNullableConverter))]
    public decimal? ValorUnitario { get; set; }

    [JsonPropertyName("info_adicional")]
    public string? InfoAdicional { get; set; }
}
