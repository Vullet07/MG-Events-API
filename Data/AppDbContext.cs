using Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data
{
    public class AppDbContext: DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }

        public DbSet<User> Users { get; set; }
        
        public DbSet<ForumPost> ForumPosts { get; set; }
        
        public DbSet<ForumThread> ForumThread { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Username).IsRequired().HasMaxLength(200);
                entity.Property(u => u.Role).IsRequired();
                entity.Property(u => u.PhotoUrl).HasMaxLength(500);
            });

            modelBuilder.Entity<ForumThread>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Title).IsRequired();

                entity.HasOne(t => t.CreatedByUser)
                      .WithMany()
                      .HasForeignKey("CreatedByUserId")
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(t => t.Posts)
                      .WithOne(p => p.Thread)
                      .HasForeignKey("ThreadId")
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ForumPost>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Content).IsRequired();

                entity.HasOne(p => p.User)
                      .WithMany()
                      .HasForeignKey("UserId")
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Thread)
                      .WithMany(t => t.Posts)
                      .HasForeignKey("ThreadId")
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(p => p.Replies)
                      .WithOne()
                      .HasForeignKey("ParentPostId")
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
