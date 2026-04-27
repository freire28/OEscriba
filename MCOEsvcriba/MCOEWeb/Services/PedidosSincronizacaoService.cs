using System.Globalization;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using MCOEWeb.Data;
using MCOEWeb.Data.Entities;

namespace MCOEWeb.Services;

/// <summary>
/// (1) Pesquisa pedidos na Tiny pela data e grava novos em <c>PEDIDOS</c>.
/// (2) Para cada pedido novo, <c>pedido.obter</c> → <c>ItensPedido</c> + <c>id_nota_fiscal</c>.
/// (3) <c>SELECT</c> em <c>PEDIDOS</c> pela data do filtro (e pedidos inseridos nesta execução) com <c>id_nota_fiscal</c> preenchido.
/// (4) Para cada um, <c>nota.fiscal.obter.xml.php</c> e grava em <c>NOTAS_FISCAIS</c>.
/// (5) <c>NOTAS_FISCAIS</c> com <c>CONSOLIDADA</c> = 0: lê o XML, grava em <c>CONSOLIDADO_VENDAS</c> e marca <c>CONSOLIDADA</c> = 1.
/// (6) Para cada linha recém-consolidada, Mercado Livre com <see cref="Pedido.NumeroEcommerce"/>: <c>GET /orders</c>, se não achar <c>GET /packs</c> com o mesmo valor, depois <c>GET /orders</c> com <c>orders[].id</c>.
/// e atualiza <see cref="ConsolidadoVendas.TaxaMarketplace"/> e <see cref="ConsolidadoVendas.ValorFrete"/> (token renovado automaticamente quando expira).
/// </summary>
public class PedidosSincronizacaoService
{
    private readonly TinyApiClient _tiny;
    private readonly OescribaDbContext _db;
    private readonly MercadoLivreApiClient _mercadoLivre;

    public PedidosSincronizacaoService(TinyApiClient tiny, OescribaDbContext db, MercadoLivreApiClient mercadoLivre)
    {
        _tiny = tiny;
        _db = db;
        _mercadoLivre = mercadoLivre;
    }

    /// <summary>
    /// Sincroniza pedidos do dia informado (filtro Tiny: dataInicial = dataFinal = data).
    /// </summary>
    public async Task<SincronizacaoPedidosResultado> SincronizarPorDataAsync(
        string token,
        DateTime dataReferencia,
        IProgress<SincronizacaoProgresso>? progresso = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Informe o token da API Tiny.", nameof(token));

        var refDia = dataReferencia.Date;
        var fimRefDia = refDia.AddDays(1);
        var dataStr = refDia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var agregados = new List<TinyPedidoPesquisa>();

        // (1) Buscar pedidos na Tiny e gravar em PEDIDOS (novos apenas)
        var page = 1;
        var totalPages = 1;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var root = await _tiny.BuscarPedidosPorPeriodoAsync(
                token.Trim(),
                dataStr,
                dataStr,
                page,
                "DESC",
                cancellationToken);

            var ret = root.Retorno;
            if (ret is null)
                throw new InvalidOperationException("Resposta inválida da API Tiny (pedidos).");

            if (!string.Equals(ret.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                var msg = ret.Erros?.FirstOrDefault()?.Erro ?? ret.Status ?? "Erro na pesquisa de pedidos.";
                throw new InvalidOperationException(msg);
            }

            totalPages = Math.Max(1, ret.NumeroPaginas ?? 1);

            foreach (var wrap in ret.Pedidos ?? Enumerable.Empty<TinyPedidoItemWrapper>())
            {
                if (wrap.Pedido is { } ped)
                    agregados.Add(ped);
            }

            var pctBusca = totalPages <= 0 ? 0 : (int)(page * 100.0 / totalPages);
            progresso?.Report(new SincronizacaoProgresso(
                Math.Min(100, pctBusca),
                $"Buscando pedidos — página {page} de {totalPages}…",
                ReiniciarBarra: page == 1));

            if (page >= totalPages)
                break;
            page++;
        }

        var total = agregados.Count;
        if (total == 0)
        {
            progresso?.Report(new SincronizacaoProgresso(0, "Nenhum pedido para a data — consolidando NF-e pendentes…", ReiniciarBarra: true));
            var (c0, e0, idNotas0) = await ConsolidarNotasFiscaisPendentesAsync(
                progresso, cancellationToken, suprimirReinicioBarraNoPrimeiroItem: true);
            var ml0 = await EnriquecerConsolidadoComMercadoLivreAsync(idNotas0, progresso, cancellationToken);
            progresso?.Report(new SincronizacaoProgresso(100,
                $"Nenhum pedido encontrado para esta data. Consolidação: {c0} linha(s) em CONSOLIDADO_VENDAS, {e0} nota(s) com XML inválido. " +
                $"Mercado Livre: {ml0.Atualizados} atualizado(s), {ml0.Pulados} pulado(s), {ml0.Erros} erro(s)."));
            return new SincronizacaoPedidosResultado(0, 0, 0, 0,
                ConsolidadoVendasInseridas: c0, ConsolidadoVendasNaoProcessadas: e0,
                MercadoLivreConsolidadosAtualizados: ml0.Atualizados, MercadoLivrePulados: ml0.Pulados, MercadoLivreErros: ml0.Erros);
        }

        var inseridos = 0;
        var jaExistiam = 0;
        var ignoradosSemId = 0;
        var pedidosInseridosEsteLote = new List<(int IdPedidoDb, string IdPedidoTiny, string NumeroEcommerce)>();
        var primeiraGravacao = true;

        for (var i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var p = agregados[i];
            // id do JSON Tiny → compara com ID_TINY no banco
            var idTiny = (p.Id ?? "").Trim();
            if (idTiny.Length == 0)
            {
                ignoradosSemId++;
                continue;
            }

            if (idTiny.Length > 20)
                idTiny = idTiny[..20];

            var jaExiste = await _db.Pedidos.AsNoTracking().AnyAsync(x => x.IdTiny == idTiny, cancellationToken);
            if (jaExiste)
            {
                jaExistiam++;
                var pctGrava = total <= 0 ? 100 : (int)((i + 1) * 100.0 / total);
                progresso?.Report(new SincronizacaoProgresso(Math.Min(100, pctGrava),
                    $"Pedidos — {i + 1} de {total} (id {idTiny} já existe em ID_TINY, ignorado)…",
                    ReiniciarBarra: primeiraGravacao));
                primeiraGravacao = false;
                continue;
            }

            var numeroEcommerce = (p.NumeroEcommerce ?? "").Trim();
            if (numeroEcommerce.Length > 50)
                numeroEcommerce = numeroEcommerce[..50];

            var dataPedido = ParseDataPedidoTiny(p.DataPedido);
            if (dataPedido == DateTime.MinValue || dataPedido < refDia || dataPedido >= fimRefDia)
                dataPedido = refDia;

            var agora = DateTime.Now;

            var pedidoNovo = new Pedido
            {
                IdTiny = idTiny,
                NumeroEcommerce = numeroEcommerce,
                DataPedido = dataPedido,
                DataSincronizacao = agora
            };
            _db.Pedidos.Add(pedidoNovo);

            await _db.SaveChangesAsync(cancellationToken);
            inseridos++;
            pedidosInseridosEsteLote.Add((pedidoNovo.IdPedido, idTiny, numeroEcommerce));

            var pctGrava2 = total <= 0 ? 100 : (int)((i + 1) * 100.0 / total);
            progresso?.Report(new SincronizacaoProgresso(Math.Min(100, pctGrava2), $"Gravando pedidos — {i + 1} de {total}…",
                ReiniciarBarra: primeiraGravacao));
            primeiraGravacao = false;
        }

        // (2) Itens + id_nota_fiscal via pedido.obter
        var itensPedidoInseridos = 0;
        var detalhesPedidoSemOk = 0;

        if (pedidosInseridosEsteLote.Count > 0)
        {
            var totalDet = pedidosInseridosEsteLote.Count;
            for (var d = 0; d < totalDet; d++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (idPedidoDb, idPedidoTiny, _) = pedidosInseridosEsteLote[d];
                var pctDet = totalDet <= 0 ? 100 : (int)((d + 1) * 100.0 / totalDet);
                progresso?.Report(new SincronizacaoProgresso(Math.Min(100, pctDet),
                    $"Detalhe do pedido — {d + 1} de {totalDet} (ID_TINY {idPedidoTiny})…",
                    ReiniciarBarra: d == 0));

                var (okDet, nItens) = await SincronizarDetalheItensEIdNotaFiscalAsync(
                    token.Trim(),
                    idPedidoDb,
                    idPedidoTiny,
                    cancellationToken);
                itensPedidoInseridos += nItens;
                if (!okDet)
                    detalhesPedidoSemOk++;
            }
        }

        // (3) Pedidos da data do filtro com id_nota_fiscal preenchido (+ inseridos nesta execução, se DATA_PEDIDO divergir).
        var nfInseridas = 0;
        var nfSemNotaNaApi = 0;
        var nfIgnoradas = 0;

        var idsInseridosEsteRun = pedidosInseridosEsteLote.Select(x => x.IdPedidoDb).ToHashSet();
        var candidatosNf = await _db.Pedidos
            .AsNoTracking()
            .Where(p => p.IdNotaFiscal != null && p.IdNotaFiscal != ""
                && ((p.DataPedido >= refDia && p.DataPedido < fimRefDia) || idsInseridosEsteRun.Contains(p.IdPedido)))
            .ToListAsync(cancellationToken);
        var pedidosComNf = candidatosNf
            .Where(p => IsIdNotaFiscalPositivo(p.IdNotaFiscal))
            .ToList();

        // (4) Baixar XML e gravar NOTAS_FISCAIS
        if (pedidosComNf.Count > 0)
        {
            var totalNf = pedidosComNf.Count;
            for (var j = 0; j < totalNf; j++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var ped = pedidosComNf[j];
                var idNf = ped.IdNotaFiscal!.Trim();
                var pct = totalNf <= 0 ? 100 : (int)((j + 1) * 100.0 / totalNf);
                progresso?.Report(new SincronizacaoProgresso(Math.Min(100, pct),
                    $"Notas fiscais — {j + 1} de {totalNf} (id_nota_fiscal {idNf}, pedido {ped.IdTiny})…",
                    ReiniciarBarra: j == 0));

                var r = await TentarInserirNotaFiscalPorIdAsync(
                    token.Trim(),
                    ped.IdTiny,
                    idNf,
                    cancellationToken);
                nfInseridas += r.Inseridas;
                nfSemNotaNaApi += r.SemNotaNaApi;
                nfIgnoradas += r.Ignoradas;
            }
        }

        var (consolidadasInseridas, consolidadasErroXml, idNotasConsolidadas) =
            await ConsolidarNotasFiscaisPendentesAsync(progresso, cancellationToken);

        var ml = await EnriquecerConsolidadoComMercadoLivreAsync(idNotasConsolidadas, progresso, cancellationToken);

        progresso?.Report(new SincronizacaoProgresso(100,
            $"Concluído: {inseridos} pedido(s) inserido(s), {jaExistiam} já existente(s), {ignoradosSemId} sem id; " +
            $"itens: {itensPedidoInseridos} linha(s), detalhe sem OK: {detalhesPedidoSemOk}; " +
            $"NF-e: {nfInseridas} gravada(s), {nfSemNotaNaApi} falha ao obter XML, {nfIgnoradas} ignorada(s) (duplicada/XML inválido); " +
            $"consolidação: {consolidadasInseridas} em CONSOLIDADO_VENDAS, {consolidadasErroXml} nota(s) não processadas (XML); " +
            $"Mercado Livre: {ml.Atualizados} atualizado(s), {ml.Pulados} pulado(s), {ml.Erros} erro(s)."));
        return new SincronizacaoPedidosResultado(
            inseridos, jaExistiam, ignoradosSemId, total,
            nfInseridas, 0, nfSemNotaNaApi, nfIgnoradas,
            itensPedidoInseridos, detalhesPedidoSemOk,
            consolidadasInseridas, consolidadasErroXml,
            ml.Atualizados, ml.Pulados, ml.Erros);
    }

    /// <summary>
    /// Lê <c>NOTAS_FISCAIS</c> com <c>CONSOLIDADA</c> = 0, extrai totais do XML (NF-e) e grava em <c>CONSOLIDADO_VENDAS</c>.
    /// </summary>
    private async Task<(int Inseridas, int ErroXml, List<int> IdNotasFiscaisInseridas)> ConsolidarNotasFiscaisPendentesAsync(
        IProgress<SincronizacaoProgresso>? progresso,
        CancellationToken cancellationToken,
        bool suprimirReinicioBarraNoPrimeiroItem = false)
    {
        var pendentes = await _db.NotasFiscais
            .Where(n => !n.Consolidada)
            .ToListAsync(cancellationToken);

        if (pendentes.Count == 0)
            return (0, 0, new List<int>());

        var inseridas = 0;
        var erroXml = 0;
        var idNotasInseridas = new List<int>();
        var total = pendentes.Count;

        for (var i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var nf = pendentes[i];
            var pctCons = total <= 0 ? 100 : (int)((i + 1) * 100.0 / total);
            progresso?.Report(new SincronizacaoProgresso(Math.Min(100, pctCons),
                $"Consolidando vendas — {i + 1} de {total} (ID_NOTAFISCAL {nf.IdNotaFiscal})…",
                ReiniciarBarra: i == 0 && !suprimirReinicioBarraNoPrimeiroItem));

            if (!TryExtrairConsolidadoVendas(nf.XmlNota, nf.IdNotaFiscal, out var linha))
            {
                erroXml++;
                continue;
            }

            _db.ConsolidadoVendas.Add(linha);
            nf.Consolidada = true;
            await _db.SaveChangesAsync(cancellationToken);
            inseridas++;
            idNotasInseridas.Add(nf.IdNotaFiscal);
        }

        return (inseridas, erroXml, idNotasInseridas);
    }

    /// <summary>
    /// Atualiza <c>TAXA_MARKETPLACE</c> (<c>order_cost - list_cost - sale_fee</c>) e <c>VALOR_FRETE</c> via API do Mercado Livre.
    /// Só processa NF-e consolidadas nesta execução. Sem token ML válido/refresh, marca todas como puladas.
    /// </summary>
    private async Task<(int Atualizados, int Pulados, int Erros)> EnriquecerConsolidadoComMercadoLivreAsync(
        IReadOnlyList<int> idNotasFiscaisConsolidadasNestaExecucao,
        IProgress<SincronizacaoProgresso>? progresso,
        CancellationToken cancellationToken)
    {
        if (idNotasFiscaisConsolidadasNestaExecucao.Count == 0)
            return (0, 0, 0);

        if (!_mercadoLivre.TemTokenParaChamadasApi())
            return (0, idNotasFiscaisConsolidadasNestaExecucao.Count, 0);

        var atualizados = 0;
        var pulados = 0;
        var erros = 0;
        var total = idNotasFiscaisConsolidadasNestaExecucao.Count;

        for (var i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var idNf = idNotasFiscaisConsolidadasNestaExecucao[i];
            var pctMl = total <= 0 ? 100 : (int)((i + 1) * 100.0 / total);
            progresso?.Report(new SincronizacaoProgresso(Math.Min(100, pctMl),
                $"Mercado Livre — {i + 1} de {total} (NF id {idNf})…",
                ReiniciarBarra: i == 0));

            try
            {
                var consolidado = await _db.ConsolidadoVendas
                    .FirstOrDefaultAsync(c => c.IdNota == idNf, cancellationToken);
                if (consolidado is null)
                {
                    pulados++;
                    continue;
                }

                var nf = await _db.NotasFiscais.AsNoTracking()
                    .FirstOrDefaultAsync(n => n.IdNotaFiscal == idNf, cancellationToken);
                if (nf is null)
                {
                    pulados++;
                    continue;
                }

                var idPedidoTiny = (nf.IdPedido ?? "").Trim();
                if (idPedidoTiny.Length == 0)
                {
                    pulados++;
                    continue;
                }

                var ped = await _db.Pedidos.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.IdTiny == idPedidoTiny, cancellationToken);
                var orderIdMl = (ped?.NumeroEcommerce ?? "").Trim();
                if (orderIdMl.Length == 0 || !orderIdMl.All(char.IsAsciiDigit))
                {
                    pulados++;
                    continue;
                }

                var (saleFee, orderCost, listCost, shippingCost) = await _mercadoLivre.ObterCustosDoPedidoAsync(orderIdMl, cancellationToken);

                var alterou = false;
                // ML: taxa = order_cost - list_cost - sale_fee (valores do shipment / pedido conforme API).
                if (orderCost.HasValue && listCost.HasValue && saleFee.HasValue)
                {
                    consolidado.TaxaMarketplace = orderCost - (orderCost - listCost.Value + shippingCost - saleFee.Value) ;
                    alterou = true;
                }
                if (shippingCost.HasValue)
                {
                    consolidado.ValorFrete = listCost.Value - shippingCost;
                    alterou = true;
                }

                if (!alterou)
                {
                    pulados++;
                    continue;
                }

                await _db.SaveChangesAsync(cancellationToken);
                atualizados++;
            }
            catch
            {
                erros++;
            }
        }

        return (atualizados, pulados, erros);
    }

    /// <summary>
    /// Extrai cliente/UF de <c>dest</c>/<c>enderDest</c> e totais de <c>ICMSTot</c>
    /// (vNF, vICMS, vPIS, vCOFINS, <c>vICMSUFDest</c> → imposto DIFAL em <see cref="ConsolidadoVendas.Difal"/>).
    /// </summary>
    private static bool TryExtrairConsolidadoVendas(string xmlNota, int idNotaFiscal, out ConsolidadoVendas linha)
    {
        linha = new ConsolidadoVendas { IdNota = idNotaFiscal };

        if (string.IsNullOrWhiteSpace(xmlNota))
            return false;

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xmlNota, LoadOptions.PreserveWhitespace);
        }
        catch
        {
            return false;
        }

        var infNFe = FindInfNFe(doc);
        if (infNFe is null)
            return false;

        var dest = FirstLocalDescendant(infNFe, "dest");
        if (dest is null)
            return false;

        var xNome = ElementLocalText(dest, "xNome");
        linha.Cliente = TruncateRequired(xNome, 255);

        var enderDest = FirstLocalDescendant(dest, "enderDest");
        var ufTxt = ElementLocalText(enderDest, "UF");
        linha.UfVenda = NormalizeUf(ufTxt);

        var icmsTot = FirstLocalDescendant(infNFe, "ICMSTot");
        if (icmsTot is null)
            return false;

        linha.ValorVenda = ParseDecimalNullable(ElementLocalText(icmsTot, "vNF"));
        linha.Icms = ParseDecimalNullable(ElementLocalText(icmsTot, "vICMS"));
        linha.Pis = ParseDecimalNullable(ElementLocalText(icmsTot, "vPIS"));
        linha.Cofins = ParseDecimalNullable(ElementLocalText(icmsTot, "vCOFINS"));
        // DIFAL (total ICMS UF destino) — grupo ICMSTot, layout NF-e
        linha.Difal = ParseDecimalNullable(ElementLocalText(icmsTot, "vICMSUFDest"));

        return true;
    }

    private static XElement? FindInfNFe(XDocument doc)
    {
        var root = doc.Root;
        if (root is null)
            return null;

        if (root.Name.LocalName == "nfeProc")
        {
            var nfe = root.Elements().FirstOrDefault(e => e.Name.LocalName == "NFe");
            return nfe?.Elements().FirstOrDefault(e => e.Name.LocalName == "infNFe");
        }

        if (root.Name.LocalName == "NFe")
            return root.Elements().FirstOrDefault(e => e.Name.LocalName == "infNFe");

        if (root.Name.LocalName == "infNFe")
            return root;

        return doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "infNFe");
    }

    private static XElement? FirstLocalDescendant(XElement root, string localName) =>
        root.DescendantsAndSelf().FirstOrDefault(e => e.Name.LocalName == localName);

    private static string? ElementLocalText(XElement? parent, string localName)
    {
        if (parent is null)
            return null;
        var el = parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
        return el?.Value?.Trim();
    }

    private static string TruncateRequired(string? s, int maxLen)
    {
        var t = (s ?? "").Trim();
        return t.Length <= maxLen ? t : t[..maxLen];
    }

    private static string NormalizeUf(string? uf)
    {
        var t = (uf ?? "").Trim().ToUpperInvariant();
        if (t.Length >= 2)
            return t[..2];
        return t.PadRight(2);
    }

    private static decimal? ParseDecimalNullable(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        var t = s.Trim();
        if (decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;
        if (decimal.TryParse(t, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out d))
            return d;
        return null;
    }

    /// <summary>
    /// <c>pedido.obter.php</c>: atualiza <see cref="Pedido.IdNotaFiscal"/> e substitui itens em <c>ItensPedido</c> para o <c>ID_PEDIDO</c> interno.
    /// </summary>
    /// <returns>Tupla: resposta OK da API; quantidade de linhas inseridas em <c>ItensPedido</c>.</returns>
    private async Task<(bool RespostaOk, int ItensInseridos)> SincronizarDetalheItensEIdNotaFiscalAsync(
        string token,
        int idPedidoDb,
        string idPedidoTiny,
        CancellationToken cancellationToken)
    {
        TinyPedidoObterRoot root;
        try
        {
            root = await _tiny.ObterPedidoDetalheAsync(token, idPedidoTiny, cancellationToken);
        }
        catch
        {
            return (false, 0);
        }

        var ret = root.Retorno;
        if (ret is null || !string.Equals(ret.Status, "OK", StringComparison.OrdinalIgnoreCase))
            return (false, 0);

        var det = ret.Pedido;
        if (det is null)
            return (false, 0);

        var pedido = await _db.Pedidos
            .FirstOrDefaultAsync(x => x.IdPedido == idPedidoDb && x.IdTiny == idPedidoTiny, cancellationToken);
        if (pedido is null)
            return (false, 0);

        var idNfTxt = (det.IdNotaFiscal ?? "").Trim();
        if (idNfTxt.Length > 0
            && long.TryParse(idNfTxt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idNfNum)
            && idNfNum > 0)
            pedido.IdNotaFiscal = idNfTxt.Length > 30 ? idNfTxt[..30] : idNfTxt;

        var existentes = await _db.ItensPedidos
            .Where(x => x.IdPedido == idPedidoDb)
            .ToListAsync(cancellationToken);
        if (existentes.Count > 0)
            _db.ItensPedidos.RemoveRange(existentes);

        var inseridos = 0;
        foreach (var wrap in det.Itens ?? Enumerable.Empty<TinyPedidoItemObterWrapper>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = wrap.Item;
            if (item is null)
                continue;

            _db.ItensPedidos.Add(new ItensPedido
            {
                IdPedido = idPedidoDb,
                IdProduto = ParseLongNullable(item.IdProduto),
                Codigo = TruncateNullable(item.Codigo, 50),
                Descricao = TruncateNullable(item.Descricao, 255),
                Unidade = TruncateNullable(item.Unidade, 10),
                Quantidade = item.Quantidade,
                ValorUnitario = item.ValorUnitario
            });
            inseridos++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (true, inseridos);
    }

    private static bool IsIdNotaFiscalPositivo(string? idNotaFiscal)
    {
        if (string.IsNullOrWhiteSpace(idNotaFiscal))
            return false;
        return long.TryParse(idNotaFiscal.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0;
    }

    private static long? ParseLongNullable(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        return long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static string? TruncateNullable(string? s, int maxLen)
    {
        if (s is null)
            return null;
        var t = s.Trim();
        if (t.Length == 0)
            return null;
        return t.Length <= maxLen ? t : t[..maxLen];
    }

    /// <summary>
    /// Baixa o XML em <c>nota.fiscal.obter.xml.php</c> pelo id da NF na Tiny e insere em <c>NOTAS_FISCAIS</c> se ainda não existir.
    /// </summary>
    private async Task<(int Inseridas, int SemNotaNaApi, int Ignoradas)> TentarInserirNotaFiscalPorIdAsync(
        string token,
        string idPedidoTiny,
        string idNotaFiscal,
        CancellationToken cancellationToken)
    {
        var idRaw = (idNotaFiscal ?? "").Trim();
        if (idRaw.Length == 0
            || !long.TryParse(idRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idNum)
            || idNum <= 0)
            return (0, 1, 0);

        var idTinyCol = idRaw.Length > 20 ? idRaw[..20] : idRaw;
        var idPedidoCol = idPedidoTiny.Length > 20 ? idPedidoTiny[..20] : idPedidoTiny;

        var jaTemNota = await _db.NotasFiscais.AsNoTracking()
            .AnyAsync(x => x.IdTiny == idTinyCol, cancellationToken);
        if (jaTemNota)
            return (0, 0, 1);

        string xml;
        try
        {
            xml = await _tiny.ObterNotaFiscalXmlAsync(token, idRaw, cancellationToken);
        }
        catch
        {
            return (0, 1, 0);
        }

        if (string.IsNullOrWhiteSpace(xml) || !IsRetornoXmlNfeOk(xml))
            return (0, 1, 0);

        var xmlParaBanco = NormalizeXmlForSqlServerXmlColumn(ExtrairXmlNfeDoRetornoTiny(xml));

        _db.NotasFiscais.Add(new NotaFiscal
        {
            IdTiny = idTinyCol,
            XmlNota = xmlParaBanco,
            IdPedido = idPedidoCol,
            Consolidada = false
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (1, 0, 0);
    }

    /// <summary>
    /// Se a Tiny devolver envelope com <c>&lt;xml_nfe&gt;</c> (com ou sem CDATA), extrai o conteúdo da NF-e para gravar na coluna <c>xml</c>.
    /// </summary>
    private static string ExtrairXmlNfeDoRetornoTiny(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return xml;

        var open = xml.IndexOf("<xml_nfe", StringComparison.OrdinalIgnoreCase);
        if (open < 0)
            return xml;

        var contentStart = xml.IndexOf('>', open);
        if (contentStart < 0)
            return xml;
        contentStart++;

        var close = xml.IndexOf("</xml_nfe>", contentStart, StringComparison.OrdinalIgnoreCase);
        if (close < 0)
            return xml;

        var inner = xml[contentStart..close].Trim();
        if (inner.StartsWith("<![CDATA[", StringComparison.Ordinal))
        {
            inner = inner[9..];
            var cdataEnd = inner.IndexOf("]]>", StringComparison.Ordinal);
            if (cdataEnd >= 0)
                inner = inner[..cdataEnd].Trim();
        }

        return inner.Length > 0 ? inner : xml;
    }

    /// <summary>
    /// Envelope Tiny com <c>&lt;status&gt;OK&lt;/status&gt;</c> ou corpo já é NF-e (sem envelope).
    /// </summary>
    private static bool IsRetornoXmlNfeOk(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return false;
        if (xml.Contains("<status>OK</status>", StringComparison.OrdinalIgnoreCase)
            || xml.Contains("<status>Ok</status>", StringComparison.Ordinal))
            return true;
        if (xml.Contains("<xml_nfe", StringComparison.OrdinalIgnoreCase)
            && (xml.Contains("<status>OK</status>", StringComparison.OrdinalIgnoreCase)
                || xml.Contains("OK</status>", StringComparison.OrdinalIgnoreCase)))
            return true;
        if (xml.Contains("<NFe", StringComparison.OrdinalIgnoreCase)
            || xml.Contains("<nfeProc", StringComparison.OrdinalIgnoreCase)
            || xml.Contains("xmlns=\"http://www.portalfiscal.inf.br/nfe\"", StringComparison.Ordinal))
            return true;
        return false;
    }

    /// <summary>
    /// Remove o prolog <c>&lt;?xml … ?&gt;</c> antes de gravar em coluna <c>xml</c> no SQL Server.
    /// Declarações com <c>encoding="UTF-8"</c> em string Unicode costumam gerar erro 9402 (“unable to switch the encoding”).
    /// </summary>
    private static string NormalizeXmlForSqlServerXmlColumn(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return xml;

        var s = xml.TrimStart();
        if (s.Length < 5 || !s.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            return xml;

        var end = s.IndexOf("?>", StringComparison.Ordinal);
        if (end < 0)
            return xml;

        return s[(end + 2)..].TrimStart();
    }

    private static DateTime ParseDataPedidoTiny(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DateTime.MinValue;

        var s = raw.Trim();
        if (DateTime.TryParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;
        if (DateTime.TryParseExact(s, "dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out dt))
            return dt;
        if (DateTime.TryParse(s, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out dt))
            return dt;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return dt;
        return DateTime.MinValue;
    }
}

public record SincronizacaoProgresso(int Percentual, string Mensagem, bool ReiniciarBarra = false);

/// <param name="Inseridos">Novos registros gravados.</param>
/// <param name="JaExistiamNoBanco">Pedidos cujo <c>id</c> do JSON já existia em <c>ID_TINY</c> — não inseridos.</param>
/// <param name="IgnoradosSemId">Itens sem <c>id</c> no JSON.</param>
/// <param name="TotalRecebidos">Total retornado pela API Tiny.</param>
/// <param name="NotasFiscaisInseridas">Linhas gravadas em <c>NOTAS_FISCAIS</c> nesta execução.</param>
/// <param name="NotasFiscaisIgnoradasSemNumeroEcommerce">Reservado; não usado (sempre 0).</param>
/// <param name="NotasFiscaisSemNotaNaAPI">Falha ao obter XML (<c>nota.fiscal.obter.xml.php</c>) ou resposta sem status OK.</param>
/// <param name="NotasFiscaisIgnoradasDuplicadaOuErro">NF já existente em <c>ID_TINY</c> ou <c>id_nota_fiscal</c> inválido.</param>
/// <param name="ItensPedidoInseridos">Linhas gravadas em <c>ItensPedido</c> após <c>pedido.obter</c>.</param>
/// <param name="DetalhesPedidoSemRespostaOk">Pedidos inseridos cuja chamada a <c>pedido.obter</c> não retornou OK ou falhou.</param>
/// <param name="ConsolidadoVendasInseridas">Linhas gravadas em <c>CONSOLIDADO_VENDAS</c> a partir de <c>NOTAS_FISCAIS</c> com <c>CONSOLIDADA</c> = 0.</param>
/// <param name="ConsolidadoVendasNaoProcessadas">Notas não consolidadas (XML inválido ou estrutura sem <c>dest</c>/<c>ICMSTot</c>).</param>
/// <param name="MercadoLivreConsolidadosAtualizados">Linhas em <c>CONSOLIDADO_VENDAS</c> com taxa/frete preenchidas via ML nesta execução.</param>
/// <param name="MercadoLivrePulados">Sem credenciais ML, sem <c>numero_ecommerce</c>, ou ML não retorna pedido/pack/order id útil, ou JSON sem taxa/frete.</param>
/// <param name="MercadoLivreErros">Falhas de rede/API ao consultar o Mercado Livre.</param>
public record SincronizacaoPedidosResultado(
    int Inseridos,
    int JaExistiamNoBanco,
    int IgnoradosSemId,
    int TotalRecebidos,
    int NotasFiscaisInseridas = 0,
    int NotasFiscaisIgnoradasSemNumeroEcommerce = 0,
    int NotasFiscaisSemNotaNaAPI = 0,
    int NotasFiscaisIgnoradasDuplicadaOuErro = 0,
    int ItensPedidoInseridos = 0,
    int DetalhesPedidoSemRespostaOk = 0,
    int ConsolidadoVendasInseridas = 0,
    int ConsolidadoVendasNaoProcessadas = 0,
    int MercadoLivreConsolidadosAtualizados = 0,
    int MercadoLivrePulados = 0,
    int MercadoLivreErros = 0);
