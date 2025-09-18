using System;
using System.Collections.Generic;

namespace Mini_LMS.Models;

public partial class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Type { get; set; } = null!;

    public string Message { get; set; } = null!;

    public bool? IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public int? CourseId { get; set; }
    public string? Reason { get; set; }
    public Course?  Course     { get; set; }
    
}
