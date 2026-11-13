using Microsoft.Extensions.Hosting;

namespace ASMARA.API.Services.Email;

public class EmailBackgroundWorker(
    IEmailQueue emailQueue,
    ISmtpEmailSender smtpSender,
    ILogger<EmailBackgroundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Email Background Worker started and listening for queue items.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var message = await emailQueue.DequeueAsync(stoppingToken);
                await smtpSender.SendDirectAsync(message.Recipients, message.Subject, message.HtmlBody, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in EmailBackgroundWorker loop.");
            }
        }

        logger.LogInformation("Email Background Worker has stopped.");
    }
}
