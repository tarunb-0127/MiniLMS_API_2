using System;
using System.Collections.Generic;

namespace Mini_LMS.Models;

public partial class Enrollment
{
    public int Id { get; set; }

    public int LearnerId { get; set; }

    public int CourseId { get; set; }

    public DateTime EnrolledAt { get; set; }

    public string? Status { get; set; }

    public virtual Course Course { get; set; } = null!;

    public virtual User Learner { get; set; } = null!;
}
