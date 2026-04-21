using Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }

        public DbSet<User> Users { get; set; }
        public DbSet<ForumPost> ForumPosts { get; set; }
        public DbSet<ForumThread> ForumThreads { get; set; }
        public DbSet<EventPin> EventPins { get; set; }
        public DbSet<EventPinResolveConfirmation> EventPinResolveConfirmations { get; set; }
        public DbSet<PostVote> PostVotes { get; set; }
        public DbSet<PinVote> PinVotes { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<TeacherRegistrationRequest> TeacherRegistrationRequests { get; set; }
        public DbSet<UserActionQuotaEvent> UserActionQuotaEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasQueryFilter(u => !u.IsDeleted);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.ScheduledDeletionAt);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.EmailConfirmationTokenHash)
                .IsUnique();

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Username).IsRequired().HasMaxLength(200);
                entity.Property(u => u.PasswordHash).IsRequired();
                entity.Property(u => u.Role).IsRequired();
                entity.Property(u => u.PhotoUrl).HasMaxLength(500);
                entity.Property(u => u.EmailConfirmationTokenHash).HasMaxLength(200);
                entity.Property(u => u.EmailConfirmationTokenExpiresAt);
                entity.Property(u => u.EmailConfirmedAt);
                entity.Property(u => u.GradeLevel);
                entity.Property(u => u.SchoolYearStart);
                entity.Property(u => u.ScheduledDeletionAt);
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
                    .WithOne(p => p.ParentPost)
                    .HasForeignKey(p => p.ParentPostId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<EventPin>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired();
                entity.Property(e => e.Category).IsRequired().HasMaxLength(80);
                entity.Property(e => e.PhotoUrl).HasMaxLength(500);
                entity.Property(e => e.ArchivedAt);
                entity.Property(e => e.ResolvedAt);

                entity.HasOne(e => e.CreatedByUser)
                    .WithMany()
                    .HasForeignKey("CreatedByUserId")
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ResolvedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.ResolvedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.IsResolved);
                entity.HasIndex(e => e.ArchivedAt);
            });

            modelBuilder.Entity<EventPinResolveConfirmation>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.CreatedAt).IsRequired();

                entity.HasOne(c => c.User)
                    .WithMany()
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Pin)
                    .WithMany(p => p.ResolveConfirmations)
                    .HasForeignKey(c => c.PinId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(c => c.PinId);
                entity.HasIndex(c => new { c.PinId, c.UserId }).IsUnique();
            });

            modelBuilder.Entity<PostVote>(entity =>
            {
                entity.HasKey(v => v.Id);

                entity.HasOne(v => v.User)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(v => v.Post)
                    .WithMany()
                    .HasForeignKey("PostId")
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex("UserId", "PostId")
                    .IsUnique();
            });

            modelBuilder.Entity<PinVote>(entity =>
            {
                entity.HasKey(v => v.Id);

                entity.HasOne(v => v.User)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(v => v.Pin)
                    .WithMany()
                    .HasForeignKey("PinId")
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex("UserId", "PinId")
                    .IsUnique();
            });

            modelBuilder.Entity<Report>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Reason).IsRequired().HasMaxLength(200);
                entity.Property(r => r.Details).HasMaxLength(2000);

                entity.HasOne(r => r.Reporter)
                    .WithMany()
                    .HasForeignKey("ReporterId")
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.ResolvedBy)
                    .WithMany()
                    .HasForeignKey("ResolvedByUserId")
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TeacherRegistrationRequest>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Username).IsRequired().HasMaxLength(100);
                entity.Property(r => r.Email).IsRequired();
                entity.Property(r => r.PasswordHash).IsRequired();
                entity.Property(r => r.Motivation).HasMaxLength(500);
                entity.Property(r => r.ReviewNote).HasMaxLength(300);
                entity.Property(r => r.EmailConfirmationTokenHash).HasMaxLength(200);
                entity.HasIndex(r => r.EmailConfirmationTokenHash).IsUnique();

                entity.HasOne(r => r.ReviewedBy)
                    .WithMany()
                    .HasForeignKey("ReviewedByUserId")
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<UserActionQuotaEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ActionType).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UserId, e.ActionType, e.CreatedAt });
            });

            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(x => x.TokenHash)
                .IsUnique();
        }
    }
}
