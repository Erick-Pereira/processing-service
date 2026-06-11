using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Api.Reports;

/// <summary>
/// Documento PDF condominial (mensal, trimestral ou anual) gerado com QuestPDF.
/// </summary>
public sealed class ExpenseReportDocument : IDocument
{
    private readonly ReportData _data;

    public ExpenseReportDocument(ReportData data) => _data = data;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(40);
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(t => t.FontSize(10));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Text("Econdomiza — Relatório Condominial").FontSize(18).Bold();
            col.Item().Text($"Tenant: {_data.TenantId}");
            col.Item().Text($"Período ({_data.PeriodLabel}): {_data.From:dd/MM/yyyy} → {_data.To:dd/MM/yyyy}");
            col.Item().Text($"Gerado em: {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC");
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(10);

            col.Item().Text("Resumo").FontSize(14).Bold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.RelativeColumn();
                });
                table.Cell().Text("Total emitido").Bold();
                table.Cell().AlignRight().Text(FormatCurrency(_data.TotalAmount)).Bold();
                table.Cell().Text("Total pago");
                table.Cell().AlignRight().Text(FormatCurrency(_data.TotalPaid));
                table.Cell().Text("Quantidade");
                table.Cell().AlignRight().Text(_data.TotalCount.ToString("N0"));
                table.Cell().Text("Ticket médio");
                table.Cell().AlignRight().Text(FormatCurrency(_data.TotalCount == 0 ? 0 : _data.TotalAmount / _data.TotalCount));
                table.Cell().Text("Fornecedores ativos");
                table.Cell().AlignRight().Text(_data.SuppliersCount.ToString("N0"));
            });

            col.Item().PaddingTop(10).Text("Despesas").FontSize(14).Bold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(70);
                    c.RelativeColumn();
                    c.ConstantColumn(80);
                    c.ConstantColumn(80);
                });
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Emissão").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Categoria").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Status").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Valor").Bold();
                });
                foreach (var e in _data.Expenses)
                {
                    table.Cell().Padding(3).Text(e.IssueDate.ToString("dd/MM/yyyy"));
                    table.Cell().Padding(3).Text(e.Category);
                    table.Cell().Padding(3).AlignRight().Text(e.Status.ToString());
                    table.Cell().Padding(3).AlignRight().Text(FormatCurrency(e.TotalAmount));
                }
            });
        });
    }

    private static string FormatCurrency(decimal v) => v.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

    public sealed record ReportData(
        Guid TenantId,
        string PeriodLabel,
        DateTime From,
        DateTime To,
        decimal TotalAmount,
        decimal TotalPaid,
        int TotalCount,
        int SuppliersCount,
        IReadOnlyList<Expense> Expenses);
}
