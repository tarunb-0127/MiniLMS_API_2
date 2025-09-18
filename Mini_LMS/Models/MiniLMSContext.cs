using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Mini_LMS.Models;

public partial class MiniLMSContext : DbContext
{
    public MiniLMSContext()
    {
    }

    public MiniLMSContext(DbContextOptions<MiniLMSContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Course> Courses { get; set; }
    public virtual DbSet<CourseApproval> CourseApprovals { get; set; }
    public virtual DbSet<CourseTakedownRequest> CourseTakedownRequests { get; set; }
    public virtual DbSet<Emailotp> Emailotps { get; set; }
    public virtual DbSet<Enrollment> Enrollments { get; set; }
    public virtual DbSet<Feedback> Feedbacks { get; set; }
    public virtual DbSet<Module> Modules { get; set; }
    public virtual DbSet<CourseProgress> CourseProgresses { get; set; }
    public virtual DbSet<Moduleprogress> Moduleprogresses { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<Passwordreset> Passwordresets { get; set; }
    public virtual DbSet<Passwordtoken> Passwordtokens { get; set; }
    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseMySql(
            "Server=localhost;Port=3307;Database=MiniLMSDB;User=root;Password=rootpassword;",
            Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.41-mysql"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        // all your entity configurations stay here (Course, Feedback, User, etc.)
        // ...

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
