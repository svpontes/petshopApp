using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetShopApp.Models;

namespace PetShopApp.Data;

/// <summary>
/// Maps the existing order aggregate explicitly without rewriting purchased-item snapshots.
/// OrderItem values are stored inside SerializedItems rather than in a separate entity table.
/// </summary>
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> order)
    {
        order.ToTable("Orders");
        order.HasKey(item => item.Id);
        order.Property(item => item.Id).ValueGeneratedOnAdd();

        // Preserve existing SQLite column types and nullability, including legacy unassociated orders.
        order.Property(item => item.OrderDate).HasColumnType("TEXT").IsRequired();
        order.Property(item => item.TotalAmount).HasColumnType("TEXT").IsRequired();
        order.Property(item => item.Status).IsRequired();
        order.Property(item => item.CustomerName).IsRequired();
        order.Property(item => item.CustomerEmail).IsRequired();
        order.Property(item => item.ShippingAddress).IsRequired();
        order.Property(item => item.SerializedItems).IsRequired();
        order.Property(item => item.UserId).IsRequired(false);
        order.Ignore(item => item.OrderItems);

        // Do not delete orders or clear their owner IDs when a tracked account is deleted.
        // The existing non-cascading SQLite FK rejects deleting an account that owns orders.
        order.HasOne<User>().WithMany(user => user.OrderHistory)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        // Match the authorized history query: one owner, newest date and ID first.
        order.HasIndex(item => new { item.UserId, item.OrderDate, item.Id })
            .HasDatabaseName("IX_Orders_UserId_OrderDate_Id")
            .IsDescending(false, true, true);
    }
}
