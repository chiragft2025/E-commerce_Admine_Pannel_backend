using InventoryAdmin.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryAdmin.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(ApplicationDbContext context)
        {
            // Apply migrations
            await context.Database.MigrateAsync();

            // ========== PERMISSIONS ==========
            if (!context.Permissions.Any())
            {
                var permissions = new[]
                {
                    new Permission { Name = "Product.View", CreatedBy = "admin", CreatedAt = DateTimeOffset.UtcNow },
                    new Permission { Name = "Product.Show", CreatedBy = "admin", CreatedAt = DateTimeOffset.UtcNow },
                    new Permission { Name = "Product.Create", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                    new Permission { Name = "Product.Edit", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                    new Permission { Name = "Product.Delete", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },

                    new Permission { Name = "Customer.View", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                    new Permission { Name = "Customer.Show", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                     new Permission { Name = "Customer.Create", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                    new Permission { Name = "Customer.Edit", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                     new Permission { Name = "Customer.Delete", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },

                    new Permission { Name = "Category.View", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                    new Permission { Name = "Category.Show", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                    new Permission { Name = "Category.Edit", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                     new Permission { Name = "Category.Create", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                      new Permission { Name = "Category.Delete", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },

                    new Permission { Name = "Order.View", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                    new Permission { Name = "Order.Manage", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },


                     new Permission { Name = "User.View", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                     new Permission { Name = "User.Show", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                     new Permission { Name = "User.Edit", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                     new Permission { Name = "User.Create", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                    new Permission { Name = "User.Delete", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },

                     new Permission { Name = "Role.Manage", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow },
                     new Permission { Name = "Role.View", CreatedBy =  "admin", CreatedAt = DateTimeOffset.UtcNow }
                };

                context.Permissions.AddRange(permissions);
                await context.SaveChangesAsync();
            }

            // ========== ROLES ==========
            if (!context.Roles.Any())
            {
                var adminRole = new Role { Name = "Admin", CreatedBy = "admin", CreatedAt = DateTimeOffset.UtcNow };
                var managerRole = new Role { Name = "Manager", CreatedBy = "admin", CreatedAt = DateTimeOffset.UtcNow };
                var viewerRole = new Role { Name = "Viewer", CreatedBy = "admin", CreatedAt = DateTimeOffset.UtcNow };

                context.Roles.AddRange(adminRole, managerRole, viewerRole);
                await context.SaveChangesAsync();

                var allPerms = context.Permissions.ToList();

                // Admin -> ALL permissions
                context.RolePermissions.AddRange(
                    allPerms.Select(p => new RolePermission
                    {
                        RoleId = adminRole.Id,
                        PermissionId = p.Id
                    })
                );

                // Manager -> most permissions
                var managerPermNames = new[]
                {
                    "Product.View","Product.Manage","Category.Manage","Role.Manage",
                    "Order.View","Order.Manage",
                    "Category.View","Customer.Manage"
                };

                var managerPerms = allPerms.Where(p => managerPermNames.Contains(p.Name)).ToList();

                context.RolePermissions.AddRange(
                    managerPerms.Select(p => new RolePermission
                    {
                        RoleId = managerRole.Id,
                        PermissionId = p.Id
                    })
                );

                // Viewer -> only VIEW permissions
                var viewerPerms = allPerms.Where(p => p.Name.EndsWith(".View")).ToList();

                context.RolePermissions.AddRange(
                    viewerPerms.Select(p => new RolePermission
                    {
                        RoleId = viewerRole.Id,
                        PermissionId = p.Id
                    })
                );

                await context.SaveChangesAsync();
            }

            // ========== ADMIN USER ==========
            if (!context.Users.Any(u => u.UserName == "admin"))
            {
                var hasher = new PasswordHasher<User>();

                var adminUser = new User
                {
                    UserName = "admin",
                    Email = "admin@example.local",
                    IsActive = true,
                    CreatedBy = "admin",
                    CreatedAt = DateTimeOffset.UtcNow
                };

                adminUser.PasswordHash = hasher.HashPassword(adminUser, "Admin@123");

                context.Users.Add(adminUser);
                await context.SaveChangesAsync();

                var adminRole = context.Roles.First(r => r.Name == "Admin");

                context.UserRoles.Add(new UserRole
                {
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id
                });

                await context.SaveChangesAsync();
            }

            // ========== SAMPLE CATEGORY & PRODUCT ==========
            if (!context.Categories.Any())
            {
                var cat = new Category
                {
                    Title = "General",
                    Description = "Default category",
                    CreatedBy = "admin",
                    CreatedAt = DateTimeOffset.UtcNow
                };

                context.Categories.Add(cat);
                await context.SaveChangesAsync();

                var product = new Product
                {
                    Name = "Sample Product",
                    SKU = "SMP-001",
                    Price = 50,
                    Stock = 10,
                    CategoryId = cat.Id,
                    CreatedBy = "admin",
                    CreatedAt = DateTimeOffset.UtcNow
                };

                context.Products.Add(product);
                await context.SaveChangesAsync();
            }
        }
    }
}
