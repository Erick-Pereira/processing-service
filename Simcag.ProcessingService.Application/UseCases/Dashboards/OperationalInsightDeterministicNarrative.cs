using System.Globalization;
using System.Text;
using Simcag.ProcessingService.Application.DTOs;

namespace Simcag.ProcessingService.Application.UseCases.Dashboards;

/// <summary>
/// Camada de explicabilidade operacional: priorização, narrativa curta e ligações — sem LLM (sempre disponível).
/// </summary>
public static class OperationalInsightDeterministicNarrative
{
    public static OperationalInsightsEnvelope Apply(OperationalInsightsEnvelope source)
    {
        var enriched = source.Items.Select(EnrichOne).ToList();
        return new OperationalInsightsEnvelope
        {
            GeneratedAtUtc = source.GeneratedAtUtc,
            Disclaimer = source.Disclaimer,
            RuleSetVersion = source.RuleSetVersion,
            SnapshotId = source.SnapshotId,
            ServedFrom = source.ServedFrom,
            ExpiresAtUtc = source.ExpiresAtUtc,
            ExecutiveSummary = BuildExecutiveSummary(enriched),
            Items = enriched
        };
    }

    private static OperationalInsightDto EnrichOne(OperationalInsightDto x) =>
        x.Kind switch
        {
            "category-spend-concentration" => EnrichCategoryConcentration(x),
            "month-over-month-total-spend" => EnrichMonthOverMonth(x),
            "market-price-deviation" => EnrichMarketPriceDeviation(x),
            _ => EnrichGeneric(x)
        };

    private static OperationalInsightDto EnrichGeneric(OperationalInsightDto x)
    {
        var impact = x.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase) ? 55 : 30;
        var tier = impact >= 50 ? "attention" : "info";
        var origin = HumanizeDataSources(x.DataSources);
        return CloneWith(
            x,
            uiGroup: "spend",
            tier: tier,
            impactScore: impact,
            simple: x.Summary,
            detailed: $"{x.Summary} Origem: {origin}.",
            why: "Ajuda a priorizar revisões de compras e fornecedores.",
            recommendation: "Revise o detalhe técnico abaixo e cruze com as despesas recentes.",
            suggested: "Abrir a lista de compras e filtrar pelo período indicado.",
            originLabel: origin,
            benchmark: "",
            compliance: "Quando há mudança brusca nos números, as validações de conformidade podem exigir contexto extra (ex.: justificativa de excepção).",
            anomaly: "",
            links: new[]
            {
                new OperationalInsightLinkDto { Label = "Compras", Href = "/compras" },
                new OperationalInsightLinkDto { Label = "Conformidades", Href = "/conformidades" }
            });
    }

    private static OperationalInsightDto EnrichCategoryConcentration(OperationalInsightDto x)
    {
        var shareStr = x.Evidence.GetValueOrDefault("shareOfTotal", "0%");
        var share = ParsePercent(shareStr);
        var top = x.Evidence.GetValueOrDefault("topCategory", "—");
        var impact = (int)Math.Clamp(Math.Round(share * 100m), 0, 100);
        var tier = share >= 0.55m ? "critical" : share >= 0.45m ? "attention" : "info";
        var origin = "Resumo mensal de despesas agregadas por categoria (últimos 12 meses).";
        var simple =
            $"A categoria «{top}» concentra cerca de {share:P0} do gasto analisado — vale a pena rever fornecedores e contratos ligados a ela.";
        var detailed =
            $"{x.Summary} Isto significa que pequenas variações nessa categoria movem uma fatia grande do orçamento. {origin}";
        var why =
            "Concentração elevada aumenta risco de dependência de poucos fornecedores e reduz margem de negociação.";
        var rec = "Pedir relatório por fornecedor na categoria dominante e avaliar oportunidades de concurso ou renegociação.";
        var act = "Abrir relatórios e, em seguida, filtrar compras pela categoria destacada.";
        var bench =
            "Benchmark interno: o percentual compara apenas categorias do mesmo condomínio no período — não é comparação com mercado externo.";
        var compliance =
            "Em auditorias, categorias dominantes são onde duplicidades e preços fora da média aparecem com mais frequência.";
        return CloneWith(
            x,
            uiGroup: "spend",
            tier: tier,
            impactScore: impact,
            simple: simple,
            detailed: detailed,
            why: why,
            recommendation: rec,
            suggested: act,
            originLabel: origin,
            benchmark: bench,
            compliance: compliance,
            anomaly: "",
            links: new[]
            {
                new OperationalInsightLinkDto { Label = "Relatórios", Href = "/relatorios" },
                new OperationalInsightLinkDto { Label = "Compras", Href = "/compras" },
                new OperationalInsightLinkDto { Label = "Conformidades", Href = "/conformidades" }
            });
    }

    private static OperationalInsightDto EnrichMonthOverMonth(OperationalInsightDto x)
    {
        var deltaStr = x.Evidence.GetValueOrDefault("relativeChange", "0%");
        var delta = ParsePercent(deltaStr);
        var abs = Math.Abs(delta);
        var impact = (int)Math.Clamp(Math.Round(abs * 100m), 0, 100);
        var tier = abs >= 0.35m ? "critical" : abs >= 0.25m ? "attention" : "info";
        var origin = "Totais mensais agregados do condomínio (série histórica interna).";
        var direction = delta >= 0 ? "subiu" : "desceu";
        var simple =
            $"O total de gastos do mês mais recente {direction} cerca de {abs:P0} face ao mês anterior — verifique se há compras pontuais ou alteração de volume.";
        var detailed =
            $"{x.Summary} {origin} Variações sazonais (limpeza, obras, energia) são comuns; confirme se há facturas atípicas.";
        var why =
            "Saltos mês-a-mês afectam caixa e previsão; detectar cedo evita surpresas em assembleia.";
        var rec = "Isolar as 5 maiores despesas do mês corrente e comparar com o mês anterior.";
        var act = "Abrir Compras ordenado por valor e rever categorias com maior delta.";
        var bench =
            "Benchmark: a comparação é sempre contra o mês imediatamente anterior no mesmo condomínio.";
        var compliance =
            "Picos de gasto podem disparar alertas de preço ou duplicidade — valide na central de conformidade.";
        var anomaly =
            abs >= 0.35m
                ? "Magnitude elevada para uma única transição mensal: trate como anomalia até prova em contrário."
                : "Variação moderada: pode ser operação normal, mas merece confirmação rápida.";
        return CloneWith(
            x,
            uiGroup: "trend",
            tier: tier,
            impactScore: impact,
            simple: simple,
            detailed: detailed,
            why: why,
            recommendation: rec,
            suggested: act,
            originLabel: origin,
            benchmark: bench,
            compliance: compliance,
            anomaly: anomaly,
            links: new[]
            {
                new OperationalInsightLinkDto { Label = "Compras", Href = "/compras" },
                new OperationalInsightLinkDto { Label = "Alertas", Href = "/alertas" },
                new OperationalInsightLinkDto { Label = "Insights (recalcular)", Href = "/insights" }
            });
    }

    private static OperationalInsightDto EnrichMarketPriceDeviation(OperationalInsightDto x)
    {
        var product = x.Evidence.GetValueOrDefault("productName", "—");
        var devStr = x.Evidence.GetValueOrDefault("deviationPercent", "0");
        _ = decimal.TryParse(devStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var dev);
        var impact = (int)Math.Clamp(Math.Round(Math.Abs(dev)), 0, 100);
        var tier = dev >= 50m ? "critical" : dev >= 30m ? "attention" : "info";
        var origin = "Benchmark de mercado obtido após upload e análise de preço (price-analysis).";
        var simple =
            $"O produto «{product}» está significativamente acima do preço de referência de mercado (+{dev:F0}%) — confirme se o valor da NF está correcto.";
        var detailed =
            $"{x.Summary} {origin} Desvios elevados podem indicar superfaturamento ou diferença de especificação do item.";
        var why = "Comparar com o mercado reduz risco de pagar acima do valor usual na categoria.";
        var rec = "Validar a NF, consultar outros fornecedores e registar justificativa se o preço for legítimo.";
        var act = "Abrir Alertas e o catálogo de produtos para rever histórico e variações.";
        return CloneWith(
            x,
            uiGroup: "price",
            tier: tier,
            impactScore: impact,
            simple: simple,
            detailed: detailed,
            why: why,
            recommendation: rec,
            suggested: act,
            originLabel: origin,
            benchmark: "Referência: média/benchmark externo via market-data-service.",
            compliance: "Desvios persistentes podem gerar alertas automáticos de superfaturamento.",
            anomaly: dev >= 50m ? "Desvio acentuado face ao mercado — priorize revisão." : "",
            links: new[]
            {
                new OperationalInsightLinkDto { Label = "Alertas", Href = "/alertas" },
                new OperationalInsightLinkDto { Label = "Produtos", Href = "/produtos" },
                new OperationalInsightLinkDto { Label = "Insights (recalcular)", Href = "/insights" }
            });
    }

    private static OperationalInsightDto CloneWith(
        OperationalInsightDto x,
        string uiGroup,
        string tier,
        int impactScore,
        string simple,
        string detailed,
        string why,
        string recommendation,
        string suggested,
        string originLabel,
        string benchmark,
        string compliance,
        string anomaly,
        IReadOnlyList<OperationalInsightLinkDto> links) =>
        new()
        {
            Id = x.Id,
            Kind = x.Kind,
            Title = x.Title,
            Summary = x.Summary,
            Severity = x.Severity,
            Confidence = x.Confidence,
            PrimaryPeriod = x.PrimaryPeriod,
            ComparePeriod = x.ComparePeriod,
            DataSources = x.DataSources,
            Criteria = x.Criteria,
            Evidence = x.Evidence,
            UiGroup = uiGroup,
            Tier = tier,
            ImpactScore = impactScore,
            SimpleExplanation = simple,
            DetailedExplanation = detailed,
            WhyItMatters = why,
            Recommendation = recommendation,
            SuggestedAction = suggested,
            DataOriginLabel = originLabel,
            BenchmarkNote = benchmark,
            ComplianceNote = compliance,
            AnomalyNote = anomaly,
            OperationalLinks = links
        };

    private static string HumanizeDataSources(IReadOnlyList<string> sources)
    {
        if (sources.Count == 0)
            return "Dados internos do condomínio.";
        return string.Join(
            "; ",
            sources.Select(s =>
                s.Contains("mv_monthly", StringComparison.OrdinalIgnoreCase)
                    ? "Resumo mensal de despesas (vista agregada do condomínio)"
                    : s));
    }

    private static decimal ParsePercent(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0m;
        var t = raw.Trim().TrimEnd('%');
        if (decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d >= 1m ? d / 100m : d;
        return 0m;
    }

    private static string BuildExecutiveSummary(IReadOnlyList<OperationalInsightDto> items)
    {
        if (items.Count == 0)
            return "Neste momento não há sinais automáticos acima dos limiares — continue a registar despesas para manter o painel útil.";

        var sb = new StringBuilder();
        sb.Append("Resumo para decisão: ");
        var critical = items.Where(i => i.Tier == "critical").ToList();
        if (critical.Count > 0)
        {
            sb.Append("há ");
            sb.Append(critical.Count);
            sb.Append(" ponto(s) que requerem atenção imediata — comece por «");
            sb.Append(critical[0].Title);
            sb.Append("». ");
        }
        else
        {
            sb.Append("não há alertas críticos automáticos; ");
        }

        sb.Append("Os cartões abaixo ordenam-se por impacto e explicam o que mudou, porque importa e qual o próximo passo sugerido.");
        return sb.ToString();
    }
}