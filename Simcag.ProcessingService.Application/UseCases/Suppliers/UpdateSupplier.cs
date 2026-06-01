using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Exceptions;
using Simcag.ProcessingService.Domain.ValueObjects;

namespace Simcag.ProcessingService.Application.UseCases.Suppliers;

public sealed record UpdateSupplierCommand(
    Guid Id,
    string Name,
    string Document,
    string? Email,
    string? Phone,
    string? Address,
    string? Category) : IRequest;

public sealed class UpdateSupplierValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Document).NotEmpty();
    }
}

public sealed class UpdateSupplierHandler : IRequestHandler<UpdateSupplierCommand>
{
    private readonly ISupplierRepository _suppliers;
    public UpdateSupplierHandler(ISupplierRepository suppliers) => _suppliers = suppliers;

    public async Task Handle(UpdateSupplierCommand request, CancellationToken ct)
    {
        var supplier = await _suppliers.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Supplier", request.Id);
        var contact = new ContactInfo(request.Email, request.Phone, request.Address);
        supplier.Update(request.Name, request.Document, contact, request.Category, null);
        await _suppliers.SaveChangesAsync(ct);
    }
}

public sealed record DeactivateSupplierCommand(Guid Id) : IRequest;

public sealed class DeactivateSupplierHandler : IRequestHandler<DeactivateSupplierCommand>
{
    private readonly ISupplierRepository _suppliers;
    public DeactivateSupplierHandler(ISupplierRepository suppliers) => _suppliers = suppliers;

    public async Task Handle(DeactivateSupplierCommand request, CancellationToken ct)
    {
        var supplier = await _suppliers.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Supplier", request.Id);
        supplier.Deactivate();
        await _suppliers.SaveChangesAsync(ct);
    }
}
