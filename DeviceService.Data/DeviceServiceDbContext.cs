using DeviceService.Core.Entities;
using Microsoft.EntityFrameworkCore;



namespace DeviceService.Data;

public class DeviceServiceDbContext : DbContext
{
    public DeviceServiceDbContext(DbContextOptions<DeviceServiceDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<ServiceTicket> ServiceTickets { get; set; }
    public DbSet<StatusHistory> StatusHistories { get; set; }
    public DbSet<TrackingLink> TrackingLinks { get; set; }
    public DbSet<TrackingVerificationAttempt> TrackingVerificationAttempts { get; set; }
    public DbSet<UserAccount> UserAccounts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Customer
        modelBuilder.Entity<Customer>()
            .HasKey(c => c.Id);
        modelBuilder.Entity<Customer>()
            .HasMany(c => c.Devices)
            .WithOne(d => d.Customer)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Device
        modelBuilder.Entity<Device>()
            .HasKey(d => d.Id);
        modelBuilder.Entity<Device>()
            .HasMany(d => d.ServiceTickets)
            .WithOne(st => st.Device)
            .HasForeignKey(st => st.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        // ServiceTicket
        modelBuilder.Entity<ServiceTicket>()
            .HasKey(st => st.Id);
        modelBuilder.Entity<ServiceTicket>()
            .Property(st => st.EstimatedPrice)
            .HasPrecision(18, 2);
        modelBuilder.Entity<ServiceTicket>()
            .HasMany(st => st.StatusHistories)
            .WithOne(sh => sh.ServiceTicket)
            .HasForeignKey(sh => sh.ServiceTicketId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ServiceTicket>()
            .HasOne(st => st.TrackingLink)
            .WithOne(tl => tl.ServiceTicket)
            .HasForeignKey<TrackingLink>(tl => tl.ServiceTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // StatusHistory
        modelBuilder.Entity<StatusHistory>()
            .HasKey(sh => sh.Id);

        // TrackingLink
        modelBuilder.Entity<TrackingLink>()
            .HasKey(tl => tl.Id);
        modelBuilder.Entity<TrackingLink>()
            .HasIndex(tl => tl.Token)
            .IsUnique();

        modelBuilder.Entity<TrackingVerificationAttempt>()
            .HasKey(attempt => attempt.Id);
        modelBuilder.Entity<TrackingVerificationAttempt>()
            .HasIndex(attempt => new { attempt.Token, attempt.RemoteAddress })
            .IsUnique();

        modelBuilder.Entity<UserAccount>()
            .HasKey(account => account.Id);
        modelBuilder.Entity<UserAccount>()
            .HasIndex(account => account.Email)
            .IsUnique();
    }
}
