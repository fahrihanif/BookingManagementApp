﻿using API.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Data;

public class BookingManagementDbContext : DbContext
{
    public BookingManagementDbContext(DbContextOptions<BookingManagementDbContext> options) : base (options) { }
    
    // Add Models to migrate
    public DbSet<Account> Accounts { get; set; }
    public DbSet<AccountRole> AccountRoles { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Room> Rooms { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Education> Educations { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<University> Universities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Employee>().HasIndex(e => new {
            e.Nik,
            e.Email,
            e.PhoneNumber
        }).IsUnique();
        
        // One University has many Educations
        modelBuilder.Entity<University>()
                    .HasMany(e => e.Educations)
                    .WithOne(u => u.University)
                    .HasForeignKey(e => e.UniversityGuid)
                    .OnDelete(DeleteBehavior.Restrict);

        /*modelBuilder.Entity<Education>()
                    .HasOne(u => u.University)
                    .WithMany(e => e.Educations)
                    .HasForeignKey(u => u.UniversityGuid);*/
        
        // One Education has one Employee
        modelBuilder.Entity<Education>()
                    .HasOne(e => e.Employee)
                    .WithOne(e => e.Education)
                    .HasForeignKey<Education>(e => e.Guid);
    }
}
