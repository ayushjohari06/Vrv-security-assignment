using Microsoft.EntityFrameworkCore;
using System;

namespace CrudApi.Models
{
    public class UserContext :DbContext
    {
        public UserContext(DbContextOptions<UserContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Seed data
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = "shubham@gmail.com",
                    Password = "shubham@123",
                    IsAdmin = true,
                    Age = 27,
                    Hobbies = new string[] { "Reading", "Gaming" }
                },
                new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = "rahul@gmail.com",
                    Password = "rahul@123",
                    IsAdmin = false,
                    Age = 30,
                    Hobbies = new string[] { "Traveling", "Photography" }
                }
            );
        }
    }
}
