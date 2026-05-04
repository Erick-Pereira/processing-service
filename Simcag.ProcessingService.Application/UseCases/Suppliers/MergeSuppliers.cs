using FluentValidation;
using MediatR;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Exceptions;

namespace Simcag.ProcessingService.Application.UseCases.Suppliers;

public sealed record MergeSuppliersCommand(Guid SourceSupplierId, Guid TargetSupplierId) : IRequest;

public sealed class MergeSuppliersValidator : AbstractValidator<MergeSuppliersCommand>
{
    public MergeSuppliersValidator()
    {
        RuleFor(x => x.SourceSupplierId).NotEmpty();
        RuleFor(x => x.TargetSupplierId).NotEmpty();
        RuleFor(x => x).Must(x => x.SourceSupplierId != x.TargetSupplierId)
            .WithMessage("Origem e destino devem ser fornecedores distintos.");
    }
}

public sealed class MergeSuppliersHandler : IRequestHandler<MergeSuppliersCommand>
{
    private readonly ISupplierRepository _suppliers;
    private readonly IExpenseRepository _expenses;

    public MergeSuppliersHandler(ISupplierRepository suppliers, IExpenseRepository expenses)
    {
        _suppliers = suppliers;
        _expenses = expenses;
    }

    public async Task Handle(MergeSuppliersCommand request, CancellationToken ct)
    {
        var source = await _suppliers.GetByIdAsync(request.SourceSupplierId, ct)
            ?? throw new NotFoundException("Supplier", request.SourceSupplierId);
        var target = await _suppliers.GetByIdAsync(request.TargetSupplierId, ct)
            ?? throw new NotFoundException("Supplier", request.TargetSupplierId);

        if (!target.IsActive)
            throw new DomainException("O fornecedor de destino está inativo; reative-o antes de consolidar.");

        await _expenses.ReassignSupplierAsync(source.Id, target.Id, ct);

        source.Deactivate();
        await _suppliers.SaveChangesAsync(ct);
    }
}
