using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Mini_LMS.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        private SmtpClient CreateSmtpClient()
        {
            var username = _config["Email:Username"];
            var password = _config["Email:Password"];

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                _logger.LogError("SMTP credentials are missing or empty.");
                throw new InvalidOperationException("SMTP credentials not configured.");
            }

            return new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true,
                Timeout = 10000
            };
        }

        // Made protected so a subclass can override if needed
        protected virtual MailMessage CreateMessage(string toEmail, string subject, string body)
        {
            var fromEmail = _config["Email:Username"];

            if (string.IsNullOrWhiteSpace(toEmail))
            {
                throw new ArgumentException("Recipient email is required.");
            }

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail, "MiniLMS Notifications"),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            message.To.Add(toEmail);
            return message;
        }

        // All public send methods are now virtual
        public virtual async Task<bool> SendOtpEmailAsync(string toEmail, string otp)
        {
            try
            {
                var client = CreateSmtpClient();
                var message = CreateMessage(
                    toEmail,
                    "🔐 Your MiniLMS OTP",
                    $"Your OTP is: {otp}\nIt expires in 5 minutes."
                );

                _logger.LogInformation("Sending OTP email to {Email}", toEmail);
                await client.SendMailAsync(message);
                _logger.LogInformation("OTP email sent to {Email}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP email to {Email}", toEmail);
                return false;
            }
        }

        public virtual async Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink)
        {
            try
            {
                var client = CreateSmtpClient();
                var message = CreateMessage(
                    toEmail,
                    "🔁 Password Reset Request",
                    $"Hello,\n\nClick the link below to reset your password:\n{resetLink}\n\nThis link will expire in 30 minutes."
                );

                _logger.LogInformation("Sending password reset email to {Email}", toEmail);
                await client.SendMailAsync(message);
                _logger.LogInformation("Password reset email sent to {Email}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
                return false;
            }
        }

        public virtual async Task<bool> SendCourseApprovalEmailAsync(string trainerEmail, string courseName)
        {
            try
            {
                var client = CreateSmtpClient();
                var message = CreateMessage(
                    trainerEmail,
                    "Course Approved",
                    $"Your course '{courseName}' has been added successfully and is now live."
                );

                _logger.LogInformation("Sending course approval email to {Email}", trainerEmail);
                await client.SendMailAsync(message);
                _logger.LogInformation("Course approval email sent to {Email}", trainerEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send course approval email to {Email}", trainerEmail);
                return false;
            }
        }

        public virtual async Task<bool> SendCourseUpdateEmailAsync(string trainerEmail, string courseName)
        {
            try
            {
                var client = CreateSmtpClient();
                var message = CreateMessage(
                    trainerEmail,
                    "Course Updated",
                    $"Your course '{courseName}' has been updated successfully."
                );

                _logger.LogInformation("Sending course update email to {Email}", trainerEmail);
                await client.SendMailAsync(message);
                _logger.LogInformation("Course update email sent to {Email}", trainerEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send course update email to {Email}", trainerEmail);
                return false;
            }
        }

        public virtual async Task<bool> SendTakedownRequestEmailAsync(string adminEmail, string courseName, string trainerEmail)
        {
            try
            {
                var client = CreateSmtpClient();
                var message = CreateMessage(
                    adminEmail,
                    "Course Takedown Request",
                    $"Trainer '{trainerEmail}' has requested to remove the course '{courseName}'."
                );

                _logger.LogInformation("Sending takedown request email to admin {Email}", adminEmail);
                await client.SendMailAsync(message);
                _logger.LogInformation("Takedown request email sent to admin {Email}", adminEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send takedown request email to admin {Email}", adminEmail);
                return false;
            }
        }

        public virtual async Task<bool> SendFeedbackNotificationEmailAsync(string trainerEmail, string learnerName, string courseName)
        {
            try
            {
                var client = CreateSmtpClient();
                var message = CreateMessage(
                    trainerEmail,
                    "📣 New Feedback Received",
                    $"Learner '{learnerName}' has submitted feedback for your course '{courseName}'."
                );

                _logger.LogInformation("Sending feedback notification email to {Email}", trainerEmail);
                await client.SendMailAsync(message);
                _logger.LogInformation("Feedback notification email sent to {Email}", trainerEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send feedback notification to {Email}", trainerEmail);
                return false;
            }
        }

        public virtual async Task<bool> SendNewCourseAvailableEmailAsync(string learnerEmail, string courseName)
        {
            try
            {
                var client = CreateSmtpClient();
                var message = CreateMessage(
                    learnerEmail,
                    "New Course Available",
                    $"Hello,\n\nA new course '{courseName}' is now available. Log in to MiniLMS to enroll!"
                );

                _logger.LogInformation("Sending new-course email to {Email}", learnerEmail);
                await client.SendMailAsync(message);
                _logger.LogInformation("New-course email sent to {Email}", learnerEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send new-course email to {Email}", learnerEmail);
                return false;
            }
        }
    }
}
