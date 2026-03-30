using System;
using System.Collections.Generic;
using System.Text;

namespace Simcag.ProcessingService.Domain.Entities
{
    public class ProductPrice
    {
        public Guid Id { get; set; }

        public required string ProductName { get; set; }

        public decimal Price { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}