namespace Mini_LMS.DTOs
{
    public class AdminLoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class RegisterDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
    }

    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
    }

    public class PasswordResetRequestDto
    {
        public int UserId { get; set; }
    }

    public class PasswordResetDto
    {
        public string Email { get; set; }
        public string Token { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }

    public class CourseCreateDTO
    {
        public int TrainerId { get; set; }
        public string Name { get; set; }
        public string? Type { get; set; }
        public int? Duration { get; set; }
        public string? Visibility { get; set; }
    }

     public class CourseAnalyticsDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int LearnerCount { get; set; }
        public double AvgRating { get; set; }
        public double AvgProgress { get; set; }
    }

    public class TrainerAnalyticsDto
    {
        public int TotalCourses { get; set; }
        public int TotalLearners { get; set; }
        public List<CourseAnalyticsDto> Courses { get; set; } = new();
    }


}
