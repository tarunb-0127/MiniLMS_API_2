using System;
using System.Collections.Generic;

namespace Mini_LMS.Models;

public partial class Passwordtoken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Email { get; set; } = null!;

    public string Token { get; set; } = null!;

    public DateTime ExpiryTime { get; set; }

    public DateTime SentAt { get; set; }

    public virtual User User { get; set; } = null!;
}
