using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Infrastructure.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.ToTable("Carts");
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasOne(x => x.User).WithOne(u => u.Cart)
            .HasForeignKey<Cart>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> b)
    {
        b.ToTable("CartItems");
        b.HasIndex(x => new { x.CartId, x.CourseId }).IsUnique();
        b.HasOne(x => x.Cart).WithMany(c => c.Items)
            .HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Course).WithMany()
            .HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_SubTotal", "[SubTotal] >= 0");
            t.HasCheckConstraint("CK_Orders_Discount", "[DiscountAmount] >= 0");
            t.HasCheckConstraint("CK_Orders_Total", "[TotalAmount] >= 0");
            t.HasCheckConstraint("CK_Orders_Math", "[TotalAmount] = [SubTotal] - [DiscountAmount]");
        });

        b.Property(x => x.OrderCode).HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(x => x.CouponCode).HasMaxLength(50);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.Status).HasDefaultValue(OrderStatus.Pending).HasSentinel(OrderStatus.Pending);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => x.OrderCode).IsUnique();
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => x.Status);

        b.HasOne(x => x.User).WithMany(u => u.Orders)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Coupon).WithMany()
            .HasForeignKey(x => x.CouponId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("OrderItems");
        b.Property(x => x.CourseTitle).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.OrderId, x.CourseId }).IsUnique();
        b.HasIndex(x => x.InstructorId);

        b.HasOne(x => x.Order).WithMany(o => o.Items)
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Course).WithMany()
            .HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments");
        b.Property(x => x.TransactionRef).HasMaxLength(50).IsUnicode(false).IsRequired();
        b.Property(x => x.ProviderTransactionId).HasMaxLength(100).IsUnicode(false);
        b.Property(x => x.BankCode).HasMaxLength(20);
        b.Property(x => x.ResponseCode).HasMaxLength(20);
        b.Property(x => x.Status).HasDefaultValue(PaymentStatus.Pending).HasSentinel(PaymentStatus.Pending);

        b.HasIndex(x => x.TransactionRef).IsUnique();
        // BR-06: một giao dịch cổng chỉ được xử lý một lần
        b.HasIndex(x => new { x.Provider, x.ProviderTransactionId })
            .IsUnique().HasFilter("[ProviderTransactionId] IS NOT NULL");

        b.HasOne(x => x.Order).WithMany(o => o.Payments)
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> b)
    {
        b.ToTable("Coupons", t =>
        {
            t.HasCheckConstraint("CK_Coupons_Value", "[DiscountValue] > 0");
            t.HasCheckConstraint("CK_Coupons_Dates", "[ExpiresAt] > [StartsAt]");
            t.HasCheckConstraint("CK_Coupons_Percentage", "[DiscountType] <> 'Percentage' OR [DiscountValue] <= 100");
            t.HasCheckConstraint("CK_Coupons_UsedCount", "[UsedCount] >= 0");
        });

        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Code).IsUnique();

        b.HasOne(x => x.Course).WithMany()
            .HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
    }
}
