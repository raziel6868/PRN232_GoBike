using Microsoft.EntityFrameworkCore;
using BusinessObjects.Entities;

namespace DataAccessObjects;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<MotorcycleType> MotorcycleTypes => Set<MotorcycleType>();
    public DbSet<Motorcycle> Motorcycles => Set<Motorcycle>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<RentalContract> RentalContracts => Set<RentalContract>();
    public DbSet<RentalInspection> RentalInspections => Set<RentalInspection>();
    public DbSet<RentalPayment> RentalPayments => Set<RentalPayment>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique indexes
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<MotorcycleType>().HasIndex(m => m.Name).IsUnique();
        modelBuilder.Entity<Motorcycle>().HasIndex(m => m.LicensePlate).IsUnique();
        modelBuilder.Entity<Motorcycle>().HasIndex(m => m.RegistrationNo).IsUnique().HasFilter("[RegistrationNo] IS NOT NULL");
        modelBuilder.Entity<Customer>().HasIndex(c => c.CCCD).IsUnique();
        modelBuilder.Entity<Customer>().HasIndex(c => c.DriverLicenseNo).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.CustomerId).IsUnique().HasFilter("[CustomerId] IS NOT NULL");
        modelBuilder.Entity<RentalInspection>().HasIndex(i => new { i.RentalContractId, i.InspectionType }).IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(user => user.Customer)
            .WithOne(customer => customer.User)
            .HasForeignKey<User>(user => user.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Decimal precision
        modelBuilder.Entity<MotorcycleType>().Property(m => m.DefaultDailyRate).HasPrecision(18, 2);
        modelBuilder.Entity<MotorcycleType>().Property(m => m.DefaultDepositAmount).HasPrecision(18, 2);
        modelBuilder.Entity<RentalContract>().Property(r => r.DailyPrice).HasPrecision(18, 2);
        modelBuilder.Entity<RentalContract>().Property(r => r.TotalAmount).HasPrecision(18, 2);
        modelBuilder.Entity<RentalContract>().Property(r => r.DepositAmount).HasPrecision(18, 2);
        modelBuilder.Entity<RentalContract>().Property(r => r.LateFee).HasPrecision(18, 2);
        modelBuilder.Entity<RentalContract>().Property(r => r.DamageFee).HasPrecision(18, 2);
        modelBuilder.Entity<RentalContract>().Property(r => r.OtherFee).HasPrecision(18, 2);
        modelBuilder.Entity<RentalContract>().Property(r => r.DiscountAmount).HasPrecision(18, 2);
        modelBuilder.Entity<RentalContract>().Property(r => r.FinalAmount).HasPrecision(18, 2);
        modelBuilder.Entity<RentalContract>().Property(r => r.RemainingAmount).HasPrecision(18, 2);
        modelBuilder.Entity<RentalContract>().Property(r => r.AdditionalPaymentAmount).HasPrecision(18, 2);
        modelBuilder.Entity<RentalContract>().Property(r => r.RefundAmount).HasPrecision(18, 2);
        modelBuilder.Entity<RentalContract>().Property(r => r.CancellationFee).HasPrecision(18, 2);
        modelBuilder.Entity<RentalPayment>().Property(p => p.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<MaintenanceRecord>().Property(m => m.RepairCost).HasPrecision(18, 2);

        modelBuilder.Entity<RentalInspection>()
            .HasOne(i => i.RentalContract)
            .WithMany(c => c.Inspections)
            .HasForeignKey(i => i.RentalContractId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RentalPayment>()
            .HasOne(p => p.RentalContract)
            .WithMany(c => c.Payments)
            .HasForeignKey(p => p.RentalContractId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MaintenanceRecord>()
            .HasOne(m => m.Motorcycle)
            .WithMany(v => v.MaintenanceRecords)
            .HasForeignKey(m => m.MotorcycleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MaintenanceRecord>()
            .HasOne(m => m.RentalContract)
            .WithMany(c => c.MaintenanceRecords)
            .HasForeignKey(m => m.RentalContractId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
