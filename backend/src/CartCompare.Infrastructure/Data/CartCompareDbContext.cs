using CartCompare.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CartCompare.Entities.Enums;

namespace CartCompare.Infrastructure.Data;

public class CartCompareDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    public CartCompareDbContext(DbContextOptions<CartCompareDbContext> options)
        : base(options)
    {
    }

    public DbSet<Retailer> Retailers => Set<Retailer>();

    public DbSet<StoreLocation> StoreLocations => Set<StoreLocation>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<RetailerProduct> RetailerProducts => Set<RetailerProduct>();

    public DbSet<Price> Prices => Set<Price>();

    public DbSet<SavedItem> SavedItems => Set<SavedItem>();

    public DbSet<GroceryListItem> GroceryListItems => Set<GroceryListItem>();

    public DbSet<UserRetailerMembership> UserRetailerMemberships
        => Set<UserRetailerMembership>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Retailer>(entity =>
    {
        entity.HasKey(r => r.Id);

        entity.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(r => r.SupportsMembership)
            .HasDefaultValue(false);

        entity.Property(r => r.IsActive)
            .HasDefaultValue(true);
    });

    modelBuilder.Entity<StoreLocation>(entity =>
{
    entity.HasKey(s => s.Id);

    entity.Property(s => s.ExternalLocationId)
        .IsRequired()
        .HasMaxLength(100);

    entity.Property(s => s.Name)
        .HasMaxLength(200);

    entity.Property(s => s.AddressLine1)
        .IsRequired()
        .HasMaxLength(200);

    entity.Property(s => s.AddressLine2)
        .HasMaxLength(200);

    entity.Property(s => s.City)
        .IsRequired()
        .HasMaxLength(100);

    entity.Property(s => s.State)
        .IsRequired()
        .HasMaxLength(50);

    entity.Property(s => s.PostalCode)
        .IsRequired()
        .HasMaxLength(20);

    entity.Property(s => s.Latitude)
        .HasPrecision(9, 6);

    entity.Property(s => s.Longitude)
        .HasPrecision(9, 6);

    entity.Property(s => s.IsActive)
        .HasDefaultValue(true);

    entity.HasIndex(s => new { s.RetailerId, s.ExternalLocationId })
        .IsUnique();

    entity.HasOne<Retailer>()
        .WithMany()
        .HasForeignKey(s => s.RetailerId)
        .OnDelete(DeleteBehavior.Restrict);
});
modelBuilder.Entity<Item>(entity =>
{
    entity.HasKey(i => i.Id);

    entity.Property(i => i.Name)
        .IsRequired()
        .HasMaxLength(200);

    entity.Property(i => i.Brand)
        .HasMaxLength(100);

    entity.Property(i => i.Size)
        .HasMaxLength(100);

    entity.Property(i => i.Category)
        .HasMaxLength(100);

    entity.Property(i => i.IsActive)
        .HasDefaultValue(true);
});
modelBuilder.Entity<RetailerProduct>(entity =>
{
    entity.HasKey(p => p.Id);

    entity.Property(p => p.ExternalProductId)
        .IsRequired()
        .HasMaxLength(150);

    entity.Property(p => p.Name)
        .IsRequired()
        .HasMaxLength(200);

    entity.Property(p => p.Brand)
        .HasMaxLength(100);

    entity.Property(p => p.Size)
        .HasMaxLength(100);

    entity.Property(p => p.Upc)
        .HasMaxLength(20);

    entity.Property(p => p.MatchMethod)
        .HasDefaultValue(ProductMatchMethod.Standalone);

    entity.Property(p => p.MatchConfidence)
        .HasPrecision(5, 4);

    entity.Property(p => p.IsActive)
        .HasDefaultValue(true);

    entity.HasIndex(p => new { p.RetailerId, p.ExternalProductId })
        .IsUnique();

    entity.HasOne<Item>()
        .WithMany()
        .HasForeignKey(p => p.ItemId)
        .OnDelete(DeleteBehavior.Restrict);

    entity.HasOne<Retailer>()
        .WithMany()
        .HasForeignKey(p => p.RetailerId)
        .OnDelete(DeleteBehavior.Restrict);
});
modelBuilder.Entity<Price>(entity =>
{
    entity.HasKey(p => p.Id);

    entity.Property(p => p.RegularPrice)
        .HasPrecision(10, 2);

    entity.Property(p => p.SalePrice)
        .HasPrecision(10, 2);

    entity.Property(p => p.MemberPrice)
        .HasPrecision(10, 2);

    entity.Property(p => p.AvailabilityStatus)
        .HasDefaultValue(AvailabilityStatus.Unknown);

    entity.Property(p => p.SourceProvider)
        .IsRequired()
        .HasMaxLength(100);

    entity.HasIndex(p => new
    {
        p.RetailerProductId,
        p.StoreLocationId
    })
    .IsUnique();

    entity.HasOne<RetailerProduct>()
        .WithMany()
        .HasForeignKey(p => p.RetailerProductId)
        .OnDelete(DeleteBehavior.Restrict);

    entity.HasOne<StoreLocation>()
        .WithMany()
        .HasForeignKey(p => p.StoreLocationId)
        .OnDelete(DeleteBehavior.Restrict);
});
modelBuilder.Entity<GroceryListItem>(entity =>
{
    entity.HasKey(g => g.Id);

    entity.Property(g => g.Quantity)
        .HasDefaultValue(1);

    entity.HasIndex(g => new
    {
        g.UserId,
        g.ItemId
    })
    .IsUnique();

    entity.ToTable(t =>
        t.HasCheckConstraint(
            "CK_GroceryListItems_Quantity_Positive",
            "\"Quantity\" > 0"
        )
    );

    entity.HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(g => g.UserId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne<Item>()
        .WithMany()
        .HasForeignKey(g => g.ItemId)
        .OnDelete(DeleteBehavior.Restrict);
});
modelBuilder.Entity<UserRetailerMembership>(entity =>
{
    entity.HasKey(m => m.Id);

    entity.HasIndex(m => new
    {
        m.UserId,
        m.RetailerId
    })
    .IsUnique();

    entity.HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(m => m.UserId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne<Retailer>()
        .WithMany()
        .HasForeignKey(m => m.RetailerId)
        .OnDelete(DeleteBehavior.Restrict);
});
}
}