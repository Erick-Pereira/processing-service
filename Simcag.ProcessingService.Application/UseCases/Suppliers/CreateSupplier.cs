using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.ValueObjects;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.Application.UseCases.Suppliers;

public sealed record CreateSupplierCommand(
    string Name,
    string Document,
    string? Email,
    string? Phone,
    string? Address,
    string? Category) : IRequest<Guid>;

public sealed class CreateSupplierValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Document).NotEmpty();
    }
}

public sealed class CreateSupplierHandler : IRequestHandler<CreateSupplierCommand, Guid>
{
    private readonly ISupplierRepository _suppliers;
    private readonly ITenantContext _tenant;

    public CreateSupplierHandler(ISupplierRepository suppliers, ITenantContext tenant)
    {
        _suppliers = suppliers;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateSupplierCommand request, CancellationToken ct)
    {
        var contact = new ContactInfo(request.Email, request.Phone, request.Address);
        var supplier = Supplier.Create(_tenant.TenantId, request.Name, request.Document, contact, request.Category);
        await _suppliers.AddAsync(supplier, ct);
        await _suppliers.SaveChangesAsync(ct);
        return supplier.Id;
    }
}
