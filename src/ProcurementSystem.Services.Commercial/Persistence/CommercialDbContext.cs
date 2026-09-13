using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Commercial;

namespace ProcurementSystem.Services.Commercial.Persistence;

public class CommercialDbContext(DbContextOptions<CommercialDbContext> options) : DbContext(options)
{
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<InvoiceAttachment> InvoiceAttachments => Set<InvoiceAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("commercial");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommercialDbContext).Assembly);
    }
}
