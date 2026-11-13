namespace ASMARA.API.Services.Email;

public interface IEmailQueue
{
    ValueTask QueueEmailAsync(EmailMessage message, CancellationToken ct = default);
    ValueTask<EmailMessage> DequeueAsync(CancellationToken ct = default);
}
