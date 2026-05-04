using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Exceptions;

namespace Simcag.ProcessingService.Application.UseCases.Suppliers;

public sealed record ListSuppliersQuery(string? Category) : IRequest<IReadOnlyList<SupplierDto>>;

public sealed class ListSuppliersHandler : IRequestHandler<ListSuppliersQuery, IReadOnlyList<SupplierDto>>
{
    private readonly ISupplierRepository _suppliers;
    public ListSuppliersHandler(ISupplierRepository suppliers) => _suppliers = suppliers;

    public async Task<IReadOnlyList<SupplierDto>> Handle(ListSuppliersQuery q, CancellationToken ct)
    {
        var items = await _suppliers.ListAsync(q.Category, ct);
        return items.Select(Map).ToList();
    }

    internal static SupplierDto Map(Supplier s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Document = s.Document,
        DocumentType = s.DocumentType.ToString(),
        Email = s.Contact.Email,
        Phone = s.Contact.Phone,
        Address = s.Contact.Address,
        Category = s.Category,
        IsActive = s.IsActive,
        CreatedAt = s.CreatedAt,
    };
}

public sealed record GetSupplierByIdQuery(Guid Id) : IRequest<SupplierDto>;

public sealed class GetSupplierByIdHandler : IRequestHandler<GetSupplierByIdQuery, SupplierDto>
{
    private readonly ISupplierRepository _suppliers;
    public GetSupplierByIdHandler(ISupplierRepository suppliers) => _suppliers = suppliers;

    public async Task<SupplierDto> Handle(GetSupplierByIdQuery q, CancellationToken ct)
    {
        var s = await _suppliers.GetByIdAsync(q.Id, ct)
            ?? throw new NotFoundException("Supplier", q.Id);
        return ListSuppliersHandler.Map(s);
    }
}
