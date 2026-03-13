using System;
using Easshas.Domain.Entities;
using Easshas.Domain.ValueObjects;
using Easshas.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Easshas.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<GeoLocation> GeoLocations => Set<GeoLocation>();
        public DbSet<AdminSubscription> AdminSubscriptions => Set<AdminSubscription>();
        public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
        public DbSet<PasswordResetOtp> PasswordResetOtps => Set<PasswordResetOtp>();
        public DbSet<AgentConversation> AgentConversations => Set<AgentConversation>();
        public DbSet<AgentMessage> AgentMessages => Set<AgentMessage>();
        public DbSet<AgentActionLog> AgentActionLogs => Set<AgentActionLog>();
        public DbSet<UserFact> UserFacts => Set<UserFact>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Product>(b =>
            {
                b.Property(p => p.Name).IsRequired().HasMaxLength(200);
                b.Property(p => p.BrandName).IsRequired().HasMaxLength(200);
                b.Property(p => p.Category).IsRequired().HasMaxLength(100);
                b.Property(p => p.Price).HasColumnType("decimal(18,2)");
                b.Property(p => p.Sku).HasMaxLength(100);
                b.Property(p => p.Inventory).HasDefaultValue(0);
                b.Property(p => p.Available).HasDefaultValue(true);
                b.Property(p => p.ImageUrl1).HasMaxLength(2048);
                b.Property(p => p.ImageUrl2).HasMaxLength(2048);
                b.Property(p => p.ImageUrl3).HasMaxLength(2048);
                b.HasIndex(p => p.Sku).IsUnique();
                b.HasOne(p => p.CategoryRef)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Category>(b =>
            {
                b.Property(c => c.Name).IsRequired().HasMaxLength(100);
                b.Property(c => c.Slug).IsRequired().HasMaxLength(150);
                b.HasIndex(c => c.Slug).IsUnique();
            });

            builder.Entity<Order>(b =>
            {
                b.OwnsOne(o => o.BillingAddress, a =>
                {
                    a.Property(p => p.FullName).HasMaxLength(200);
                    a.Property(p => p.Line1).HasMaxLength(200);
                    a.Property(p => p.Line2).HasMaxLength(200);
                    a.Property(p => p.City).HasMaxLength(100);
                    a.Property(p => p.State).HasMaxLength(100);
                    a.Property(p => p.Country).HasMaxLength(100);
                    a.Property(p => p.PostalCode).HasMaxLength(20);
                    a.Property(p => p.PhoneNumber).HasMaxLength(50);
                });
                b.Property(p => p.TotalAmount).HasColumnType("decimal(18,2)");
                b.OwnsMany(o => o.Items, i =>
                {
                    i.WithOwner();
                    i.Property(p => p.UnitPrice).HasColumnType("decimal(18,2)");
                });
            });

            builder.Entity<Payment>(b =>
            {
                b.Property(p => p.Amount).HasColumnType("decimal(18,2)");
                b.Property(p => p.Reference).IsRequired();
            });

            builder.Entity<RefreshToken>(b =>
            {
                b.Property(r => r.Token).IsRequired();
                b.HasIndex(r => r.Token).IsUnique();
            });

            builder.Entity<ContactMessage>(b =>
            {
                b.Property(m => m.Name).IsRequired().HasMaxLength(100);
                b.Property(m => m.Message).IsRequired().HasMaxLength(5000);
                b.Property(m => m.PhoneNumber).IsRequired().HasMaxLength(20);
            });

            builder.Entity<PasswordResetOtp>(b =>
            {
                b.Property(o => o.UserId).IsRequired();
                b.Property(o => o.Email).IsRequired().HasMaxLength(200);
                b.Property(o => o.Otp).IsRequired().HasMaxLength(16);
                b.Property(o => o.ExpiresAt).IsRequired();
                b.Property(o => o.Used).HasDefaultValue(false);
                b.HasIndex(o => o.Email);
                b.HasIndex(o => o.UserId);
            });
            
            builder.Entity<AgentConversation>(b =>
            {
                b.Property(c => c.SessionId).HasMaxLength(200);
                b.HasMany<AgentMessage>().WithOne().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<AgentMessage>(b =>
            {
                b.Property(m => m.Role).HasMaxLength(20);
                b.Property(m => m.Language).HasMaxLength(10);
                b.Property(m => m.Text).IsRequired().HasMaxLength(4000);
                b.HasIndex(m => m.ConversationId);
            });

            builder.Entity<AgentActionLog>(b =>
            {
                b.Property(a => a.ActionId).IsRequired().HasMaxLength(200);
                b.Property(a => a.ActionType).HasMaxLength(200);
                b.Property(a => a.Status).HasMaxLength(50);
                b.HasIndex(a => a.ConversationId);
            });

            builder.Entity<UserFact>(b =>
            {
                b.Property(f => f.Text).IsRequired().HasMaxLength(4000);
                b.Property(f => f.EmbeddingJson).IsRequired();
                b.HasIndex(f => f.UserId);
            });
            
        }
    }
}
