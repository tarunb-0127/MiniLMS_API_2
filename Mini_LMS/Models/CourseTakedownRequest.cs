using System;
using System.Collections.Generic;

namespace Mini_LMS.Models;

public partial class CourseTakedownRequest
{
    public int Id { get; set; }

    public int CourseId { get; set; }

    public int RequestedBy { get; set; }

    public string? Reason { get; set; }

    public string? Status { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public virtual Course Course { get; set; } = null!;

    public virtual User RequestedByNavigation { get; set; } = null!;
}
