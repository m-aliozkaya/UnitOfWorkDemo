using Microsoft.EntityFrameworkCore;
using UnitOfWorkDemo.Models;

namespace UnitOfWorkDemo.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>().Property(p => p.Price).IsRequired().HasPrecision(18, 4);

            // Seed data
            modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = 1,
                    Name = "Laptop",
                    Description = "High-performance laptop for work and gaming",
                    Price = 1299.99m,
                    Stock = 15
                },
                new Product
                {
                    Id = 2,
                    Name = "Mouse",
                    Description = "Wireless gaming mouse with RGB lighting",
                    Price = 79.99m,
                    Stock = 50
                },
                new Product
                {
                    Id = 3,
                    Name = "Keyboard",
                    Description = "Mechanical keyboard with Cherry MX switches",
                    Price = 149.99m,
                    Stock = 25
                }
            );
        }
    }
}