using System;
using System.Collections.Generic;

namespace Mini_LMS.Models;

public partial class Passwordreset
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Email { get; set; } = null!;

    public string Token { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public DateTime ExpiryTime { get; set; }

    public virtual User User { get; set; } = null!;
}
