
using System.Net;
using System.Net.Mail;

namespace BC_ASP.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string body);
        Task<bool> SendOTPEmailAsync(string toEmail, string otpCode);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var emailSettings = _configuration.GetSection("EmailSettings");
                var username = emailSettings["Username"] ?? "";
                var password = emailSettings["Password"] ?? "";
                
                // Check if email is configured
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    _logger.LogWarning("Email chưa được cấu hình. Vui lòng cập nhật appsettings.json");
                    return false;
                }

                var smtpHost = emailSettings["Mail"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(emailSettings["Port"] ?? "587");
                var fromEmail = emailSettings["FromEmail"] ?? username;
                var fromName = emailSettings["FromName"] ?? "BC ASP";

                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(username, password);
                    client.Timeout = 30000;

                    var message = new MailMessage
                    {
                        From = new MailAddress(fromEmail, fromName),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    };
                    message.To.Add(toEmail);

                    await client.SendMailAsync(message);
                    _logger.LogInformation("Email đã được gửi đến: {Email}", toEmail);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gửi email: {Message}", ex.Message);
                return false;
            }
        }

        public async Task<bool> SendOTPEmailAsync(string toEmail, string otpCode)
        {
            _logger.LogInformation("OTP has been generated for {Email}: {OTP}", toEmail, otpCode);

            var subject = "Mã xác nhận đăng ký - BC ASP Bakery";
            var body = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; margin: 0; padding: 20px; }}
                        .container {{ max-width: 500px; margin: 0 auto; background: white; border-radius: 10px; overflow: hidden; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
                        .header {{ background: linear-gradient(135deg, #d4a574, #b8956a); padding: 30px; text-align: center; }}
                        .header h1 {{ color: white; margin: 0; font-size: 24px; }}
                        .content {{ padding: 30px; text-align: center; }}
                        .otp-code {{ font-size: 36px; font-weight: bold; color: #5d4037; letter-spacing: 10px; margin: 20px 0; }}
                        .note {{ color: #757575; font-size: 14px; margin-top: 20px; }}
                        .footer {{ background: #f8f5f2; padding: 20px; text-align: center; color: #999; font-size: 12px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>🎂 BC ASP Bakery</h1>
                        </div>
                        <div class='content'>
                            <h2>Xác nhận đăng ký</h2>
                            <p>Cảm ơn bạn đã đăng ký tài khoản tại BC ASP Bakery!</p>
                            <p>Mã xác nhận của bạn là:</p>
                            <div class='otp-code'>{otpCode}</div>
                            <p class='note'>Mã này có hiệu lực trong 5 phút. Vui lòng không chia sẻ mã này với bất kỳ ai.</p>
                        </div>
                        <div class='footer'>
                            <p>© 2024 BC ASP Bakery. All rights reserved.</p>
                        </div>
                    </div>
                </body>
                </html>
            ";

            return await SendEmailAsync(toEmail, subject, body);
        }
    }
}

