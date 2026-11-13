namespace ASMARA.API.Services.Email;

public interface ISmtpEmailSender
{
    Task SendDirectAsync(IEnumerable<string> toEmails, string subject, string htmlBody, CancellationToken ct = default);
}
