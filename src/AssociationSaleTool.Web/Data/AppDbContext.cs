using AssociationSaleTool.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SellerEntity> Sellers => Set<SellerEntity>();

    public DbSet<ItemEntity> Items => Set<ItemEntity>();

    public DbSet<SaleEntity> Sales => Set<SaleEntity>();

    public DbSet<SaleLineEntity> SaleLines => Set<SaleLineEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SellerEntity>(entity =>
        {
            entity.ToTable("seller");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SellerCode).HasColumnName("seller_code").IsRequired();
            entity.Property(x => x.FirstName).HasColumnName("first_name").IsRequired();
            entity.Property(x => x.LastName).HasColumnName("last_name").IsRequired();
            entity.Property(x => x.Email).HasColumnName("email");
            entity.Property(x => x.Phone).HasColumnName("phone");
            entity.Property(x => x.Notes).HasColumnName("notes");

            entity.HasIndex(x => x.SellerCode)
                .IsUnique();
        });

        modelBuilder.Entity<ItemEntity>(entity =>
        {
            entity.ToTable("item");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SellerId).HasColumnName("seller_id").IsRequired();
            entity.Property(x => x.Reference).HasColumnName("reference").IsRequired();
            entity.Property(x => x.ItemNumber).HasColumnName("item_number").IsRequired();
            entity.Property(x => x.Title).HasColumnName("title").IsRequired();
            entity.Property(x => x.SellerPriceCents).HasColumnName("seller_price_cents").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();

            entity.HasOne(x => x.Seller)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.SellerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.Reference)
                .IsUnique();
            entity.HasIndex(x => new { x.SellerId, x.ItemNumber })
                .IsUnique();
            entity.HasIndex(x => x.Title);
            entity.HasIndex(x => x.SellerId);
            entity.HasIndex(x => x.Status);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_item_status", "status IN ('registered', 'in_stock', 'sold', 'missing')");
                table.HasCheckConstraint("ck_item_seller_price_cents", "seller_price_cents >= 0");
            });
        });

        modelBuilder.Entity<SaleEntity>(entity =>
        {
            entity.ToTable("sale");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SoldAt).HasColumnName("sold_at").IsRequired();
            entity.Property(x => x.SubtotalSellerPriceCents).HasColumnName("subtotal_seller_price_cents").IsRequired();
            entity.Property(x => x.CheckoutFeeCents).HasColumnName("checkout_fee_cents").IsRequired();
            entity.Property(x => x.TotalPaidCents).HasColumnName("total_paid_cents").IsRequired();
            entity.Property(x => x.ReceiptDetails).HasColumnName("receipt_details");
            entity.HasIndex(x => x.SoldAt);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_sale_subtotal_seller_price_cents", "subtotal_seller_price_cents >= 0");
                table.HasCheckConstraint("ck_sale_checkout_fee_cents", "checkout_fee_cents >= 0");
                table.HasCheckConstraint("ck_sale_total_paid_cents", "total_paid_cents >= 0");
            });
        });

        modelBuilder.Entity<SaleLineEntity>(entity =>
        {
            entity.ToTable("sale_line");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SaleId).HasColumnName("sale_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.SellerPriceAtSaleCents).HasColumnName("seller_price_at_sale_cents").IsRequired();

            entity.HasOne(x => x.Sale)
                .WithMany(x => x.SaleLines)
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Item)
                .WithOne(x => x.SaleLine)
                .HasForeignKey<SaleLineEntity>(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.ItemId)
                .IsUnique();
            entity.HasIndex(x => x.SaleId);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_sale_line_seller_price_cents", "seller_price_at_sale_cents >= 0");
            });
        });
    }
}
