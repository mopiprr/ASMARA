namespace ASMARA.API.Services.Email;

public class EmailService(IEmailQueue emailQueue, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        await SendEmailAsync([toEmail], subject, htmlBody, ct);
    }

    public async Task SendEmailAsync(IEnumerable<string> toEmails, string subject, string htmlBody, CancellationToken ct = default)
    {
        var recipients = toEmails.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct().ToList();
        if (recipients.Count == 0) return;

        var message = new EmailMessage(recipients, subject, htmlBody);
        await emailQueue.QueueEmailAsync(message, ct);

        logger.LogDebug("[Queue] Email to {Recipients} with subject '{Subject}' has been queued for background delivery.",
            string.Join(", ", recipients), subject);
    }
}
