using Simcag.ProcessingService.Application.UseCases.Expenses;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.Shared.Events;
using Xunit;

namespace Simcag.ProcessingService.Tests.UseCases;

public sealed class ExpenseItemPriceReconcilerTests
{
    [Fact]
    public void TryReconcile_corrects_unit_when_pipeline_used_line_total()
    {
        var items = new List<ExpenseItem>
        {
            ExpenseItem.Create(
                Guid.NewGuid(),
                "Camera IP Full HD 2MP",
                quantity: 12,
                unitPrice: 890m),
        };

        var evt = new PriceAnalyzedEvent
        {
            EventId = Guid.NewGuid(),
            ProductName = "Camera IP Full HD 2MP",
            LastPrice = 10680m,
            Quantity = 1,
            MarketAverage = 185m,
            DeviationPercentage = 5670m,
        };

        var result = ExpenseItemPriceReconciler.TryReconcile(evt, items);

        Assert.NotNull(result);
        Assert.Equal(890m, result!.NfUnitPrice);
        Assert.Equal(12, result.NfQuantity);
        Assert.True(result.PriceAuditCorrected);
        Assert.True(result.CorrectedDeviationPercentage > 300m);
    }
}
