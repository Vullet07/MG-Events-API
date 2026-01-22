using Data;
using Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Seeding
{
    public class DataSeeder : IDataSeeder
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;

        public DataSeeder(AppDbContext db, IPasswordHasher<User> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        public async Task SeedAsync()
        {
            if (await _db.Users.AnyAsync()) return;

            // ---------------- USERS ----------------

            var admin = CreateUser("admin", "admin@mg-events.com", Role.Admin);
            var teacher = CreateUser("teacher", "teacher@mg-events.com", Role.Teacher);

            var students = Enumerable.Range(1, 5)
                .Select(i => CreateUser($"student{i}", $"student{i}@mg-events.com", Role.Student))
                .ToList();

            _db.Users.AddRange(admin, teacher);
            _db.Users.AddRange(students);
            await _db.SaveChangesAsync();

            // ---------------- THREADS ----------------

            var thread1 = new ForumThread
            {
                Title = "Welcome to MG Events Forum",
                CreatedByUser = admin,
                IsPinned = true,
                CreatedAt = DateTime.UtcNow
            };

            var thread2 = new ForumThread
            {
                Title = "General Discussion",
                CreatedByUser = teacher,
                CreatedAt = DateTime.UtcNow
            };

            _db.ForumThreads.AddRange(thread1, thread2);
            await _db.SaveChangesAsync();

            // ---------------- POSTS ----------------

            var post1 = new ForumPost
            {
                Title = "Forum rules",
                Content = "Please be respectful and follow the rules.",
                User = admin,
                Thread = thread1,
                CreatedAt = DateTime.UtcNow
            };

            var post2 = new ForumPost
            {
                Content = "Thanks! Happy to be here.",
                User = students[0],
                Thread = thread1,
                CreatedAt = DateTime.UtcNow
            };

            post1.Replies.Add(post2);

            var post3 = new ForumPost
            {
                Content = "What events are coming next?",
                User = students[1],
                Thread = thread2,
                CreatedAt = DateTime.UtcNow
            };

            thread1.LastPostAt = post2.CreatedAt;
            thread2.LastPostAt = post3.CreatedAt;

            _db.ForumPosts.AddRange(post1, post3);
            await _db.SaveChangesAsync();
        }

        private User CreateUser(string username, string email, Role role)
        {
            var user = new User
            {
                Username = username,
                Email = email,
                Role = role,
                IsDeleted = false,
                IsBanned = false
            };

            user.PasswordHash =
                _passwordHasher.HashPassword(user, "Test123!");

            return user;
        }
    }
}
