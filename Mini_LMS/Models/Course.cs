using System;
using System.Collections.Generic;

namespace Mini_LMS.Models;

public partial class Course
{
    public int Id { get; set; }

    public int TrainerId { get; set; }

    public string Name { get; set; } = null!;

    public string? Type { get; set; }

    public int? Duration { get; set; }

    public string? Visibility { get; set; }

    public bool? IsApproved { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<CourseApproval> CourseApprovals { get; set; } = new List<CourseApproval>();

    public virtual ICollection<CourseTakedownRequest> CourseTakedownRequests { get; set; } = new List<CourseTakedownRequest>();

    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<Module> Modules { get; set; } = new List<Module>();

    public virtual User Trainer { get; set; } = null!;
}
