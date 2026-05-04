using AssociationSaleTool.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SellerEntity> Sellers => Set<SellerEntity>();

    public DbSet<ItemEntity> Items => Set<ItemEntity>();

    public DbSet<SaleEntity> Sales => Set<SaleEntity>();

    public DbSet<SaleLineEntity> SaleLines => Set<SaleLineEntity>();

    public DbSet<EventStateEntity> EventState => Set<EventStateEntity>();

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
            entity.Property(x => x.Phone).HasColumnName("phone").IsRequired();
            entity.Property(x => x.PostalAddress).HasColumnName("postal_address").IsRequired();
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
            entity.Property(x => x.PaymentMethod).HasColumnName("payment_method");
            entity.Property(x => x.ChequeNumber).HasColumnName("cheque_number");
            entity.Property(x => x.ChequeBankName).HasColumnName("cheque_bank_name");
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
            entity.Property(x => x.CorrectedAt).HasColumnName("corrected_at");

            entity.HasOne(x => x.Sale)
                .WithMany(x => x.SaleLines)
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Item)
                .WithMany(x => x.SaleLines)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.ItemId)
                .IsUnique()
                .HasFilter("corrected_at IS NULL");
            entity.HasIndex(x => x.SaleId);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_sale_line_seller_price_cents", "seller_price_at_sale_cents >= 0");
            });
        });

        modelBuilder.Entity<EventStateEntity>(entity =>
        {
            entity.ToTable("event_state");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.SalePhase).HasColumnName("sale_phase").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasData(new EventStateEntity
            {
                Id = 1,
                SalePhase = "presale",
                UpdatedAt = new DateTime(2026, 5, 4, 0, 0, 0, DateTimeKind.Utc)
            });

            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_event_state_singleton", "Id = 1");
                table.HasCheckConstraint("ck_event_state_sale_phase", "sale_phase IN ('presale', 'sale', 'post_sale')");
            });
        });
    }
}
