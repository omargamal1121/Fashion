﻿using E_Commers.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace E_Commers.Context
{
	public class AppDbContext : IdentityDbContext
	{


		public AppDbContext(DbContextOptions options) : base(options)
		{

		}
		protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
		{
			
			configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
			base.ConfigureConventions(configurationBuilder);
		}
		public DbSet<Customer> customers { get; set; }
		public DbSet<UserOperationsLog>   userOperationsLogs { get; set; }
		public DbSet<AdminOperationsLog>  adminOperationsLogs { get; set; }
		public DbSet<Cart> Cart { get; set; }
		public DbSet<Order> Orders { get; set; }
		public DbSet<Item> Items { get; set; }
		public DbSet<Product> Products { get; set; }
		public DbSet<Payment> Payments { get; set; }
		public DbSet<Category> Categories { get; set; }
		public DbSet<SubCategory> SubCategories { get; set; }
		public DbSet<ProductInventory> ProductInventory { get; set; }
		public DbSet<PaymentMethod> PaymentMethods { get; set; }
		public DbSet<PaymentProvider> PaymentProviders { get; set; }
		public DbSet<Warehouse> Warehouses { get; set; }
		public DbSet<Image> Images { get; set; }
		public DbSet<ProductVariant> ProductVariants { get; set; }
		public DbSet<Collection> Collections { get; set; }
		public DbSet<ProductCollection> ProductCollections { get; set; }
		public DbSet<Review> Reviews { get; set; }
		public DbSet<ReturnRequest> ReturnRequests { get; set; }
		public DbSet<ReturnRequestProduct> ReturnRequestProducts { get; set; }
		public DbSet<WishlistItem> WishlistItems { get; set; }
		public DbSet<CustomerAddress> CustomerAddresses { get; set; }
		public DbSet<CartItem> CartItems { get; set; }

		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);

			// Category - SubCategory (1:M)
			builder.Entity<Category>()
				.HasMany(c => c.SubCategories)
				.WithOne(sc => sc.Category)
				.HasForeignKey(sc => sc.CategoryId)
				.OnDelete(DeleteBehavior.Restrict);

			// SubCategory - Product (1:M)
			builder.Entity<SubCategory>()
				.HasMany(sc => sc.Products)
				.WithOne(p => p.SubCategory)
				.HasForeignKey(p => p.SubCategoryId)
				.OnDelete(DeleteBehavior.Restrict);

			// Product - ProductVariant (1:M)
			builder.Entity<Product>()
				.HasMany(p => p.ProductVariants)
				.WithOne(pv => pv.Product)
				.HasForeignKey(pv => pv.ProductId)
				.OnDelete(DeleteBehavior.Restrict);

			// Product - ProductInventory (1:M)
			builder.Entity<Product>()
				.HasMany(p => p.InventoryEntries)
				.WithOne(pi => pi.Product)
				.HasForeignKey(pi => pi.ProductId)
				.OnDelete(DeleteBehavior.Restrict);

			// Warehouse - ProductInventory (1:M)
			builder.Entity<Warehouse>()
				.HasMany(w => w.ProductInventories)
				.WithOne(pi => pi.Warehouse)
				.HasForeignKey(pi => pi.WarehouseId)
				.OnDelete(DeleteBehavior.Restrict);

			// Product - Image (1:M)
			builder.Entity<Product>()
				.HasMany(p => p.Images)
				.WithOne(i => i.Product)
				.HasForeignKey(i => i.ProductId)
				.OnDelete(DeleteBehavior.Restrict);

			// Category - Image (1:M)
			builder.Entity<Category>()
				.HasMany(c => c.Images)
				.WithOne(i => i.Category)
				.HasForeignKey(i => i.CategoryId)
				.OnDelete(DeleteBehavior.Cascade);

			// SubCategory - Image (1:M)
			builder.Entity<SubCategory>()
				.HasMany(sc => sc.Images)
				.WithOne(i => i.SubCategory)
				.HasForeignKey(i => i.SubCategoryId)
				.OnDelete(DeleteBehavior.Cascade);

			// Collection - Image (1:M)
			builder.Entity<Collection>()
				.HasMany(c => c.Images)
				.WithOne(i => i.Collection)
				.HasForeignKey(i => i.CollectionId)
				.OnDelete(DeleteBehavior.Cascade);

			// Customer - Image (1:M)
			builder.Entity<Customer>()
				.HasOne(c => c.Image)
				.WithMany(i => i.Customers)
				.HasForeignKey(c => c.ImageId)
				.OnDelete(DeleteBehavior.Cascade);

			// Product - Review (1:M)
			builder.Entity<Product>()
				.HasMany(p => p.Reviews)
				.WithOne(r => r.Product)
				.HasForeignKey(r => r.ProductId)
				.OnDelete(DeleteBehavior.Cascade);

			// Customer - Review (1:M)
			builder.Entity<Customer>()
				.HasMany(c => c.Reviews)
				.WithOne(r => r.Customer)
				.HasForeignKey(r => r.CustomerId)
				.OnDelete(DeleteBehavior.Cascade);

			// Product - WishlistItem (1:M)
			builder.Entity<Product>()
				.HasMany(p => p.WishlistItems)
				.WithOne(wi => wi.Product)
				.HasForeignKey(wi => wi.ProductId)
				.OnDelete(DeleteBehavior.Cascade);

			// Customer - WishlistItem (1:M)
			builder.Entity<Customer>()
				.HasMany(c => c.WishlistItems)
				.WithOne(wi => wi.Customer)
				.HasForeignKey(wi => wi.CustomerId)
				.OnDelete(DeleteBehavior.Cascade);

			// Product - ReturnRequestProduct (1:M)
			builder.Entity<Product>()
				.HasMany(p => p.ReturnRequestProducts)
				.WithOne(rrp => rrp.Product)
				.HasForeignKey(rrp => rrp.ProductId)
				.OnDelete(DeleteBehavior.Restrict);

			// ReturnRequest - ReturnRequestProduct (1:M)
			builder.Entity<ReturnRequest>()
				.HasMany(rr => rr.ReturnRequestProducts)
				.WithOne(rrp => rrp.ReturnRequest)
				.HasForeignKey(rrp => rrp.ReturnRequestId)
				.OnDelete(DeleteBehavior.Restrict);

			// Product - ProductCollection (1:M)
			builder.Entity<Product>()
				.HasMany(p => p.ProductCollections)
				.WithOne(pc => pc.Product)
				.HasForeignKey(pc => pc.ProductId)
				.OnDelete(DeleteBehavior.Restrict);

			// Collection - ProductCollection (1:M)
			builder.Entity<Collection>()
				.HasMany(c => c.ProductCollections)
				.WithOne(pc => pc.Collection)
				.HasForeignKey(pc => pc.CollectionId)
				.OnDelete(DeleteBehavior.Restrict);

			// ProductCollection (Composite Key)
			builder.Entity<ProductCollection>()
				.HasKey(pc => new { pc.ProductId, pc.CollectionId });

			// ReturnRequestProduct (Composite Key)
			builder.Entity<ReturnRequestProduct>()
				.HasKey(rrp => new { rrp.ReturnRequestId, rrp.ProductId });

			// Customer - Order (1:M)
			builder.Entity<Customer>()
				.HasMany(c => c.Orders)
				.WithOne(o => o.Customer)
				.HasForeignKey(o => o.CustomerId)
				.OnDelete(DeleteBehavior.Restrict);

			// Order - OrderItem (1:M)
			builder.Entity<Order>()
				.HasMany(o => o.Items)
				.WithOne(oi => oi.Order)
				.HasForeignKey(oi => oi.OrderId)
				.OnDelete(DeleteBehavior.Restrict);

			// Order - Payment (1:1)
			builder.Entity<Order>()
				.HasOne(o => o.Payment)
				.WithOne(p => p.Order)
				.HasForeignKey<Payment>(p => p.OrderId)
				.OnDelete(DeleteBehavior.Restrict);

			// Customer - CustomerAddress (1:M)
			builder.Entity<Customer>()
				.HasMany(c => c.Addresses)
				.WithOne(a => a.Customer)
				.HasForeignKey(a => a.CustomerId)
				.OnDelete(DeleteBehavior.Restrict);

			// Cart - CartItem (1:M)
			builder.Entity<Cart>()
				.HasMany(c => c.Items)
				.WithOne(ci => ci.Cart)
				.HasForeignKey(ci => ci.CartId)
				.OnDelete(DeleteBehavior.Restrict);

			// Product - CartItem (1:M)
			builder.Entity<Product>()
				.HasMany(p => p.CartItems)
				.WithOne(ci => ci.Product)
				.HasForeignKey(ci => ci.ProductId)
				.OnDelete(DeleteBehavior.Restrict);

			// ProductVariant - CartItem (1:M)
			builder.Entity<ProductVariant>()
				.HasMany(pv => pv.CartItems)
				.WithOne(ci => ci.ProductVariant)
				.HasForeignKey(ci => ci.ProductVariantId)
				.OnDelete(DeleteBehavior.SetNull);

			// Product - OrderItem (1:M)
			builder.Entity<Product>()
				.HasMany(p => p.OrderItems)
				.WithOne(oi => oi.Product)
				.HasForeignKey(oi => oi.ProductId)
				.OnDelete(DeleteBehavior.Restrict);

			// ProductVariant - OrderItem (1:M)
			builder.Entity<ProductVariant>()
				.HasMany(pv => pv.OrderItems)
				.WithOne(oi => oi.ProductVariant)
				.HasForeignKey(oi => oi.ProductVariantId)
				.OnDelete(DeleteBehavior.SetNull);

			// Customer - ReturnRequest (1:M)
			builder.Entity<Customer>()
				.HasMany(c => c.ReturnRequests)
				.WithOne(rr => rr.Customer)
				.HasForeignKey(rr => rr.CustomerId)
				.OnDelete(DeleteBehavior.Restrict);

			// Order - ReturnRequest (1:M)
			builder.Entity<Order>()
				.HasMany(o => o.ReturnRequests)
				.WithOne(rr => rr.Order)
				.HasForeignKey(rr => rr.OrderId)
				.OnDelete(DeleteBehavior.Restrict);

			// Customer - UserOperationsLog (1:M)
			builder.Entity<Customer>()
				.HasMany(c => c.userOperationsLogs)
				.WithOne(uol => uol.User)
				.HasForeignKey(uol => uol.UserId)
				.OnDelete(DeleteBehavior.Restrict);

			// Customer - AdminOperationsLog (1:M)
			builder.Entity<Customer>()
				.HasMany(c => c.adminOperationsLogs)
				.WithOne(aol => aol.Admin)
				.HasForeignKey(aol => aol.AdminId)
				.OnDelete(DeleteBehavior.Restrict);

			// Discount - Product (1:M)
			builder.Entity<Discount>()
				.HasMany(d => d.products)
				.WithOne(p => p.Discount)
				.HasForeignKey(p => p.DiscountId)
				.OnDelete(DeleteBehavior.SetNull);

			// PaymentMethod - PaymentProvider (1:M)
			builder.Entity<PaymentMethod>()
				.HasMany(pm => pm.PaymentProviders)
				.WithOne(pp => pp.PaymentMethod)
				.HasForeignKey(pp => pp.PaymentMethodId)
				.OnDelete(DeleteBehavior.Restrict);

			// PaymentMethod - Payment (1:M)
			builder.Entity<PaymentMethod>()
				.HasMany(pm => pm.Payments)
				.WithOne(p => p.PaymentMethod)
				.HasForeignKey(p => p.PaymentMethodId)
				.OnDelete(DeleteBehavior.Restrict);

			// PaymentProvider - Payment (1:M)
			builder.Entity<PaymentProvider>()
				.HasMany(pp => pp.Payments)
				.WithOne(p => p.PaymentProvider)
				.HasForeignKey(p => p.PaymentProviderId)
				.OnDelete(DeleteBehavior.Restrict);
		}
	}
}
