using Data;
using Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

            var students = new List<User>
            {
                CreateUser("maria", "maria@mg-events.com", Role.Student),
                CreateUser("ivan", "ivan@mg-events.com", Role.Student),
                CreateUser("elena", "elena@mg-events.com", Role.Student),
                CreateUser("georgi", "georgi@mg-events.com", Role.Student),
                CreateUser("stefan", "stefan@mg-events.com", Role.Student)
            };

            var helpers = new List<User>
            {
                CreateUser("petya", "petya@mg-events.com", Role.Student),
                CreateUser("nikolay", "nikolay@mg-events.com", Role.Student)
            };

            var users = new List<User> { admin, teacher };
            users.AddRange(students);
            users.AddRange(helpers);

            _db.Users.AddRange(users);
            await _db.SaveChangesAsync();

            // ---------------- THREADS ----------------

            var thread1 = new ForumThread
            {
                Title = "Welcome to MG Events",
                CreatedByUser = admin,
                IsPinned = true,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            };

            var thread2 = new ForumThread
            {
                Title = "Report local issues (potholes, lights, debris)",
                CreatedByUser = teacher,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            };

            var thread3 = new ForumThread
            {
                Title = "Community cleanups & volunteering",
                CreatedByUser = students[0],
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            _db.ForumThreads.AddRange(thread1, thread2, thread3);
            await _db.SaveChangesAsync();

            // ---------------- POSTS ----------------

            var post1 = new ForumPost
            {
                Title = "Community guidelines",
                Content = "Be respectful, include clear details, and add photos when possible.",
                User = admin,
                Thread = thread1,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            };

            var post2 = new ForumPost
            {
                Content = "Excited to help keep the city safe and organized.",
                User = students[0],
                Thread = thread1,
                CreatedAt = DateTime.UtcNow.AddDays(-3).AddHours(2)
            };

            post1.Replies.Add(post2);

            var post3 = new ForumPost
            {
                Title = "Broken streetlight on Central Blvd",
                Content = "Lamp near the crosswalk flickers at night. Unsafe for pedestrians.",
                User = students[1],
                Thread = thread2,
                CreatedAt = DateTime.UtcNow.AddDays(-2).AddHours(3)
            };

            var post4 = new ForumPost
            {
                Content = "Noted. Can someone attach a photo if possible?",
                User = teacher,
                Thread = thread2,
                CreatedAt = DateTime.UtcNow.AddDays(-2).AddHours(4),
                ParentPost = post3
            };

            var post5 = new ForumPost
            {
                Title = "Park cleanup this Saturday",
                Content = "Meet at 10:00 AM. Gloves and bags will be provided.",
                User = students[1],
                Thread = thread3,
                CreatedAt = DateTime.UtcNow.AddDays(-1).AddHours(2)
            };

            thread1.LastPostAt = post2.CreatedAt;
            thread2.LastPostAt = post4.CreatedAt;
            thread3.LastPostAt = post5.CreatedAt;

            _db.ForumPosts.AddRange(post1, post3, post4, post5);
            await _db.SaveChangesAsync();

            // ---------------- EVENT PINS ----------------

            var pins = new List<EventPin>
            {
                new EventPin
                {
                    Title = "Pothole near Central Blvd",
                    Description = "Large pothole causing traffic to swerve.",
                    Latitude = 42.6967,
                    Longitude = 23.3211,
                    CreatedByUser = students[2],
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new EventPin
                {
                    Title = "Fallen tree branch",
                    Description = "Blocking the sidewalk after last night’s storm.",
                    Latitude = 42.6991,
                    Longitude = 23.3178,
                    CreatedByUser = helpers[0],
                    CreatedAt = DateTime.UtcNow.AddDays(-1).AddHours(6)
                },
                new EventPin
                {
                    Title = "Community cleanup spot",
                    Description = "Meet here for the Saturday park cleanup.",
                    Latitude = 42.6982,
                    Longitude = 23.3304,
                    CreatedByUser = students[0],
                    CreatedAt = DateTime.UtcNow.AddDays(-1).AddHours(2)
                }
            };

            _db.EventPins.AddRange(pins);
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
