using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ASMARA.API.Services.Email;

public class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : ISmtpEmailSender
{
    private readonly EmailOptions _emailOptions = options.Value;

    public async Task SendDirectAsync(IEnumerable<string> toEmails, string subject, string htmlBody, CancellationToken ct = default)
    {
        var recipients = toEmails.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct().ToList();
        if (recipients.Count == 0) return;

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailOptions.SenderName, _emailOptions.SenderEmail));

            foreach (var email in recipients)
            {
                message.To.Add(MailboxAddress.Parse(email));
            }

            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            var secureOption = _emailOptions.UseSSL
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.None;

            await client.ConnectAsync(_emailOptions.Host, _emailOptions.Port, secureOption, ct);

            if (!string.IsNullOrWhiteSpace(_emailOptions.Username))
            {
                await client.AuthenticateAsync(_emailOptions.Username, _emailOptions.Password, ct);
            }

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            logger.LogInformation("[SMTP] Email successfully sent to {Recipients} with subject: {Subject}",
                string.Join(", ", recipients), subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[SMTP] Failed to send email to {Recipients} with subject: {Subject}",
                string.Join(", ", recipients), subject);
        }
    }
}
