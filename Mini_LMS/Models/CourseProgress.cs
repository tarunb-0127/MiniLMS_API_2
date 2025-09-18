// Models/CourseProgress.cs
using System;

namespace Mini_LMS.Models
{
    public partial class CourseProgress
    {
        public int Id { get; set; }
        public int LearnerId { get; set; }
        public int CourseId { get; set; }
        public decimal ProgressPercentage { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual User Learner { get; set; } = null!;
        public virtual Course Course { get; set; } = null!;
    }
}
