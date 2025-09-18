using System;
using System.Collections.Generic;

namespace Mini_LMS.Models;

public partial class Module
{
    public int Id { get; set; }

    public int CourseId { get; set; }

    public string Name { get; set; } = null!;

    public string? Difficulty { get; set; }

    public string? Description { get; set; }

    public string? FilePath { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Course Course { get; set; } = null!;

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<Moduleprogress> Moduleprogresses { get; set; } = new List<Moduleprogress>();
}
