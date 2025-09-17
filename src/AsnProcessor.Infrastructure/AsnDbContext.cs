using AsnProcessor.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsnProcessor.Infrastructure;

public class AsnDbContext(DbContextOptions<AsnDbContext> options) : DbContext(options)
{
    public DbSet<Box> Boxes => Set<Box>();
    public DbSet<BoxLine> BoxLines => Set<BoxLine>();
    public DbSet<ProcessedFile> ProcessedFiles => Set<ProcessedFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Box>(b =>
        {
            b.Property(x => x.SupplierIdentifier).HasMaxLength(64);
            b.Property(x => x.Identifier).HasMaxLength(128);
            b.HasIndex(x => new { x.SupplierIdentifier, x.Identifier }).IsUnique();
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.BoxId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BoxLine>(l =>
        {
            l.Property(x => x.PoNumber).HasMaxLength(64);
            l.Property(x => x.Isbn).HasMaxLength(32);
        });

        modelBuilder.Entity<ProcessedFile>(pf =>
        {
            pf.Property(x => x.FileName).HasMaxLength(512);
            pf.HasIndex(x => x.ChecksumSha256).IsUnique();
        });
    }
}