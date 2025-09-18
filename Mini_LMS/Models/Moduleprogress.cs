using System;
using System.Collections.Generic;

namespace Mini_LMS.Models;

public partial class Moduleprogress
{
    public int Id { get; set; }

    public int LearnerId { get; set; }

    public int ModuleId { get; set; }

    public int CourseId { get; set; }  // <-- Add this

    public decimal? ProgressPercentage { get; set; }

    public bool? IsCompleted { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User Learner { get; set; } = null!;

    public virtual Module Module { get; set; } = null!;
}
