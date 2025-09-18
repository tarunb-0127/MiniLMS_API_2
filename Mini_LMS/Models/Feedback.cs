using System;
using System.Collections.Generic;

namespace Mini_LMS.Models;

public partial class Feedback
{
    public int Id { get; set; }

    public int LearnerId { get; set; }

    public int CourseId { get; set; }

    public int? ModuleId { get; set; }

    public string Message { get; set; } = null!;

    public DateTime SubmittedAt { get; set; }

    public int Rating { get; set; }

    public virtual Course Course { get; set; } = null!;

    public virtual User Learner { get; set; } = null!;

    public virtual Module? Module { get; set; }
}
