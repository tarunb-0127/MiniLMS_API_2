using System;
using System.Collections.Generic;

namespace Mini_LMS.Models;

public partial class Emailotp
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    public string OtpCode { get; set; } = null!;

    public DateTime ExpiryTime { get; set; }

    public DateTime SentAt { get; set; }
}
