using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Services.Email;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Tickets.Events;

public class TicketNotificationHandler(
    IEmailService emailService,
    IEmployeeRepository employeeRepository,
    IProcurementDetailRepository procurementDetailRepository,
    IMaintenanceDetailRepository maintenanceDetailRepository,
    ILogger<TicketNotificationHandler> logger
) : INotificationHandler<TicketCreatedEvent>,
    INotificationHandler<TicketStatusChangedEvent>
{
    private async Task<List<string>> GetEmailsByRoleAsync(string roleCode, CancellationToken ct)
    {
        return await employeeRepository.QueryNoTracking()
            .Where(e => e.Account != null && e.Account.Roles.Any(r => r.RoleCode == roleCode))
            .Select(e => e.Email)
            .ToListAsync(ct);
    }

    // Create Ticket
    public async Task Handle(TicketCreatedEvent notification, CancellationToken ct)
    {
        var ticket = notification.Ticket;
        var requester = notification.Requester;
        var manager = notification.DirectManager
            ?? (ticket.CurrentTaskEmployeeId.HasValue ? await employeeRepository.GetByIdAsync(ticket.CurrentTaskEmployeeId.Value, ct) : null)
            ?? (requester.ManagerId.HasValue ? await employeeRepository.GetByIdAsync(requester.ManagerId.Value, ct) : null);

        if (manager == null || string.IsNullOrWhiteSpace(manager.Email))
        {
            logger.LogWarning("Manager email not found for ticket {TicketNumber}", notification.Ticket.TicketNumber);
            return;
        }

        if (ticket.TicketType == (byte)TicketTypeEnum.Procurement)
        {
            var items = notification.ProcurementItems ?? 
                await procurementDetailRepository.QueryNoTracking().Where(p => p.TicketId == ticket.Id).ToListAsync(ct);

            var body = EmailTemplates.Procurement(
                recipientName: manager.FullName,
                title: "Review Tiket Pengadaan",
                message: $"Staf Anda, <strong>{requester.FullName}</strong>, telah mengajukan tiket pengadaan baru dan menunggu peninjauan Anda.",
                ticketNumber: ticket.TicketNumber,
                items: items,
                note: ticket.Description
            );

            await emailService.SendEmailAsync(manager.Email, $"[ASMARA] Review Tiket Pengadaan: {ticket.TicketNumber}", body, ct);
        }
        else if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
        {
            var detail = notification.MaintenanceDetail?.Asset != null
                ? notification.MaintenanceDetail
                : await maintenanceDetailRepository.QueryNoTracking().Include(m => m.Asset).FirstOrDefaultAsync(m => m.TicketId == ticket.Id, ct);

            var assetTag = detail?.Asset?.AssetTag ?? "Aset Terdaftar";
            var problemDesc = detail?.Description ?? ticket.Description ?? "-";

            var extraInfo = $"<strong>Asset Tag:</strong> {assetTag}<br><strong>Keluhan:</strong> {problemDesc}";
            var body = EmailTemplates.Notification(
                recipientName: manager.FullName,
                title: "Review Tiket Perawatan",
                message: $"Staf Anda, <strong>{requester.FullName}</strong>, telah mengajukan tiket perawatan perangkat dan menunggu peninjauan Anda.",
                ticketNumber: ticket.TicketNumber,
                note: ticket.Description,
                extraInfo: extraInfo
            );

            await emailService.SendEmailAsync(manager.Email, $"[ASMARA] Review Tiket Perawatan: {ticket.TicketNumber}", body, ct);
        }
    }

    // Status change
    public async Task Handle(TicketStatusChangedEvent notification, CancellationToken ct)
    {
        var ticket = notification.Ticket;
        var requester = notification.Requester;
        var actor = notification.Actor;
        var action = notification.Action.Trim().ToLowerInvariant();

        switch (action)
        {
            case "create":
                var createManager = (ticket.CurrentTaskEmployeeId.HasValue ? await employeeRepository.GetByIdAsync(ticket.CurrentTaskEmployeeId.Value, ct) : null)
                    ?? (requester.ManagerId.HasValue ? await employeeRepository.GetByIdAsync(requester.ManagerId.Value, ct) : null);

                if (createManager != null && !string.IsNullOrWhiteSpace(createManager.Email))
                {
                    if (ticket.TicketType == (byte)TicketTypeEnum.Procurement)
                    {
                        var items = notification.ProcurementItems ?? 
                            await procurementDetailRepository.QueryNoTracking().Where(p => p.TicketId == ticket.Id).ToListAsync(ct);

                        var body = EmailTemplates.Procurement(
                            recipientName: createManager.FullName,
                            title: "Review Tiket Pengadaan",
                            message: $"Staf Anda, <strong>{requester.FullName}</strong>, telah mengajukan tiket pengadaan baru dan menunggu peninjauan Anda.",
                            ticketNumber: ticket.TicketNumber,
                            items: items,
                            note: ticket.Description ?? notification.Note
                        );

                        await emailService.SendEmailAsync(createManager.Email, $"[ASMARA] Review Tiket Pengadaan: {ticket.TicketNumber}", body, ct);
                    }
                    else if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
                    {
                        var detail = notification.MaintenanceDetail ??
                            await maintenanceDetailRepository.QueryNoTracking().Include(m => m.Asset).FirstOrDefaultAsync(m => m.TicketId == ticket.Id, ct);

                        var assetTag = detail?.Asset?.AssetTag ?? "Aset Terdaftar";
                        var problemDesc = detail?.Description ?? ticket.Description ?? "-";

                        var extraInfo = $"<strong>Asset Tag:</strong> {assetTag}<br><strong>Keluhan:</strong> {problemDesc}";
                        var body = EmailTemplates.Notification(
                            recipientName: createManager.FullName,
                            title: "Review Tiket Perawatan",
                            message: $"Staf Anda, <strong>{requester.FullName}</strong>, telah mengajukan tiket perawatan perangkat dan menunggu peninjauan Anda.",
                            ticketNumber: ticket.TicketNumber,
                            note: ticket.Description ?? notification.Note,
                            extraInfo: extraInfo
                        );

                        await emailService.SendEmailAsync(createManager.Email, $"[ASMARA] Review Tiket Perawatan: {ticket.TicketNumber}", body, ct);
                    }
                }
                break;
            case "approve":
                var requesterMessage = ticket.TicketType == (byte)TicketTypeEnum.Procurement
                    ? $"Tiket Anda telah disetujui oleh <strong>{actor.FullName}</strong> dan diteruskan ke Finance untuk peninjauan anggaran."
                    : $"Tiket Anda telah disetujui oleh <strong>{actor.FullName}</strong> dan saat ini sedang diteruskan ke tahap berikutnya.";

                var approveBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: "Tiket Anda Disetujui",
                    message: requesterMessage,
                    ticketNumber: ticket.TicketNumber,
                    note: notification.Note
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Tiket Disetujui: {ticket.TicketNumber}", approveBody, ct);

                if (ticket.TicketType == (byte)TicketTypeEnum.Procurement)
                {
                    var financeEmails = await GetEmailsByRoleAsync(Roles.Finance, ct);
                    if (financeEmails.Count == 0) financeEmails = ["finance.team@asmara.local"];

                    var items = notification.ProcurementItems ?? 
                        await procurementDetailRepository.QueryNoTracking().Where(p => p.TicketId == ticket.Id).ToListAsync(ct);

                    var financeBody = EmailTemplates.Procurement(
                        recipientName: "Tim Finance",
                        title: "Tiket Masuk: Tinjauan Anggaran Pengadaan",
                        message: $"Tiket pengadaan <strong>{ticket.TicketNumber}</strong> dari <strong>{requester.FullName}</strong> telah disetujui Manajer dan masuk ke Finance untuk ditinjau anggarannya.",
                        ticketNumber: ticket.TicketNumber,
                        items: items,
                        note: notification.Note
                    );
                    await emailService.SendEmailAsync(financeEmails, $"[ASMARA] Tiket Pengadaan Masuk: {ticket.TicketNumber}", financeBody, ct);
                }
                else if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
                {
                    var itSupportEmails = await GetEmailsByRoleAsync(Roles.ItSupport, ct);
                    if (itSupportEmails.Count == 0) itSupportEmails = ["itsupport.team@asmara.local"];

                    var detail = notification.MaintenanceDetail ??
                        await maintenanceDetailRepository.QueryNoTracking().Include(m => m.Asset).FirstOrDefaultAsync(m => m.TicketId == ticket.Id, ct);

                    var extraInfo = $"<strong>Pemohon:</strong> {requester.FullName}<br><strong>Asset Tag:</strong> {detail?.Asset?.AssetTag ?? "Aset Terdaftar"}";
                    var itBody = EmailTemplates.Notification(
                        recipientName: "Tim IT Support",
                        title: "Penugasan Perawatan Baru",
                        message: $"Tiket perawatan {ticket.TicketNumber} telah disetujui Manajer. Silakan buka aplikasi ASMARA untuk menugaskan teknisi penanggung jawab.",
                        ticketNumber: ticket.TicketNumber,
                        note: notification.Note,
                        extraInfo: extraInfo
                    );
                    await emailService.SendEmailAsync(itSupportEmails, $"[ASMARA] Penugasan Perawatan IT: {ticket.TicketNumber}", itBody, ct);
                }
                break;

            case "revise":
                var reviseBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: "Permintaan Revisi Tiket",
                    message: $"Tiket Anda membutuhkan revisi data dari <strong>{actor.FullName}</strong>. Silakan periksa catatan revisi di bawah dan ajukan kembali setelah diperbarui.",
                    ticketNumber: ticket.TicketNumber,
                    note: notification.Note ?? "Harap periksa kembali detail tiket Anda."
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Permintaan Revisi: {ticket.TicketNumber}", reviseBody, ct);
                break;

            case "reject":
                var rejectBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: "Tiket Ditolak",
                    message: $"Pengajuan tiket Anda telah ditolak oleh <strong>{actor.FullName}</strong> dan tiket telah ditutup.",
                    ticketNumber: ticket.TicketNumber,
                    note: notification.Note
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Tiket Ditolak: {ticket.TicketNumber}", rejectBody, ct);
                break;

            case "completed":
            case "receivegoods":
            case "repaired":
            case "close":
                var completedTitle = ticket.TicketType == (byte)TicketTypeEnum.Procurement
                    ? "Barang Pengadaan Tiba di Inventory"
                    : "Perbaikan Perangkat Selesai";

                var completedMessage = ticket.TicketType == (byte)TicketTypeEnum.Procurement
                    ? "Barang yang diajukan telah dibeli oleh Purchasing dan sudah tiba di gudang Inventory. Silakan ambil barang dan lakukan penutupan tiket."
                    : $"Perangkat Anda telah selesai diperbaiki oleh teknisi (<strong>{actor.FullName}</strong>). Silakan ambil perangkat Anda dan tutup tiket di sistem ASMARA.";

                var completeBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: completedTitle,
                    message: completedMessage,
                    ticketNumber: ticket.TicketNumber,
                    note: notification.Note
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] {completedTitle}: {ticket.TicketNumber}", completeBody, ct);
                break;

            default:
                logger.LogInformation("Action {Action} does not have an email trigger defined.", notification.Action);
                break;
        }
    }
}
