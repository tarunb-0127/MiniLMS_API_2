using System;
using System.Collections.Generic;

namespace Mini_LMS.Models;

public partial class User
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    public string? Username { get; set; }

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public bool? IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<CourseApproval> CourseApprovals { get; set; } = new List<CourseApproval>();

    public virtual ICollection<CourseTakedownRequest> CourseTakedownRequests { get; set; } = new List<CourseTakedownRequest>();

    public virtual ICollection<Course> Courses { get; set; } = new List<Course>();

    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<Moduleprogress> Moduleprogresses { get; set; } = new List<Moduleprogress>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Passwordreset> Passwordresets { get; set; } = new List<Passwordreset>();

    public virtual ICollection<Passwordtoken> Passwordtokens { get; set; } = new List<Passwordtoken>();
}
