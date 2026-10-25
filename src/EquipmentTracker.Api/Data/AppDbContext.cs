using Microsoft.EntityFrameworkCore;

namespace EquipmentTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<Checkout> Checkouts => Set<Checkout>();
    public DbSet<CheckoutLine> CheckoutLines => Set<CheckoutLine>();
    public DbSet<HistoryEvent> HistoryEvents => Set<HistoryEvent>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums are stored as readable strings.
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(30);
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Person>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(100);
            e.Property(p => p.PinHash).HasMaxLength(200);
            e.HasIndex(p => p.Name).IsUnique();
        });

        model.Entity<Equipment>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.ConditionNote).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.Name).IsUnique();
            e.HasOne(x => x.Approver).WithMany().HasForeignKey(x => x.ApproverId);
        });

        model.Entity<Checkout>(e =>
        {
            e.HasOne(c => c.Borrower).WithMany().HasForeignKey(c => c.BorrowerId);
            e.HasMany(c => c.Lines).WithOne(l => l.Checkout).HasForeignKey(l => l.CheckoutId);
        });

        model.Entity<CheckoutLine>(e =>
        {
            e.Property(l => l.RejectionReason).HasMaxLength(500);
            e.HasOne(l => l.Equipment).WithMany().HasForeignKey(l => l.EquipmentId);
            e.HasOne<Person>().WithMany().HasForeignKey(l => l.DecidedById);
        });

        model.Entity<HistoryEvent>(e =>
        {
            e.Property(h => h.Note).HasMaxLength(500);
            e.HasIndex(h => h.AtUtc);
            e.HasOne(h => h.Actor).WithMany().HasForeignKey(h => h.ActorId);
            e.HasOne<Equipment>().WithMany().HasForeignKey(h => h.EquipmentId);
            e.HasOne<Checkout>().WithMany().HasForeignKey(h => h.CheckoutId);
            e.HasOne<CheckoutLine>().WithMany().HasForeignKey(h => h.CheckoutLineId);
            e.HasOne<HistoryEvent>().WithMany().HasForeignKey(h => h.CorrectsEventId);
        });

        // Nothing is ever deleted, so no cascades (also avoids multiple cascade paths on SQL Server).
        foreach (var fk in model.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
