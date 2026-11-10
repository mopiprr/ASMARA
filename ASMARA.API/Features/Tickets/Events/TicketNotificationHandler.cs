using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Services.Email;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Tickets.Events;

public class TicketNotificationHandler(
    IEmailService emailService,
    IUnitOfWork uow,
    ILogger<TicketNotificationHandler> logger
) : INotificationHandler<TicketCreatedEvent>,
    INotificationHandler<TicketStatusChangedEvent>
{
    private async Task<List<string>> GetEmailsByRoleAsync(string roleCode, CancellationToken ct)
    {
        return await uow.Employees.QueryNoTracking()
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
            ?? (ticket.CurrentTaskEmployeeId.HasValue ? await uow.Employees.GetByIdAsync(ticket.CurrentTaskEmployeeId.Value, ct) : null)
            ?? (requester.ManagerId.HasValue ? await uow.Employees.GetByIdAsync(requester.ManagerId.Value, ct) : null);

        if (manager == null || string.IsNullOrWhiteSpace(manager.Email))
        {
            logger.LogWarning("Manager email not found for ticket {TicketNumber}", notification.Ticket.TicketNumber);
            return;
        }

        if (ticket.TicketType == (byte)TicketTypeEnum.Procurement)
        {
            var items = notification.ProcurementItems ?? 
                await uow.ProcurementDetails.QueryNoTracking().Where(p => p.TicketId == ticket.Id).ToListAsync(ct);

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
                : await uow.MaintenanceDetails.QueryNoTracking().Include(m => m.Asset).FirstOrDefaultAsync(m => m.TicketId == ticket.Id, ct);

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
                var createManager = (ticket.CurrentTaskEmployeeId.HasValue ? await uow.Employees.GetByIdAsync(ticket.CurrentTaskEmployeeId.Value, ct) : null)
                    ?? (requester.ManagerId.HasValue ? await uow.Employees.GetByIdAsync(requester.ManagerId.Value, ct) : null);

                if (createManager != null && !string.IsNullOrWhiteSpace(createManager.Email))
                {
                    if (ticket.TicketType == (byte)TicketTypeEnum.Procurement)
                    {
                        var items = notification.ProcurementItems ?? 
                            await uow.ProcurementDetails.QueryNoTracking().Where(p => p.TicketId == ticket.Id).ToListAsync(ct);

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
                            await uow.MaintenanceDetails.QueryNoTracking().Include(m => m.Asset).FirstOrDefaultAsync(m => m.TicketId == ticket.Id, ct);

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
                var approveNote = !string.IsNullOrWhiteSpace(notification.Note) 
                    ? notification.Note 
                    : "Disetujui untuk diteruskan ke tahap berikutnya.";

                if (ticket.TicketType == (byte)TicketTypeEnum.Procurement)
                {
                    var currentStep = await uow.Tickets.GetCurrentStepAsync(ticket.Id, ct);
                    var items = notification.ProcurementItems ?? 
                        await uow.ProcurementDetails.QueryNoTracking().Where(p => p.TicketId == ticket.Id).ToListAsync(ct);

                    if (currentStep == StepCodes.Purchasing)
                    {
                        var purchasingReqBody = EmailTemplates.Notification(
                            recipientName: requester.FullName,
                            title: "Anggaran Tiket Disetujui",
                            message: $"Anggaran tiket pengadaan Anda telah disetujui oleh Finance (<strong>{actor.FullName}</strong>) dan diteruskan ke tim Purchasing untuk proses pembelian barang.",
                            ticketNumber: ticket.TicketNumber,
                            note: approveNote
                        );
                        await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Anggaran Pengadaan Disetujui: {ticket.TicketNumber}", purchasingReqBody, ct);

                        var purchasingEmails = await GetEmailsByRoleAsync(Roles.Purchasing, ct);
                        if (purchasingEmails.Count == 0) purchasingEmails = ["purchasing.team@asmara.local"];

                        var purchasingBody = EmailTemplates.Procurement(
                            recipientName: "Tim Purchasing",
                            title: "Proses Pembelian Barang Pengadaan",
                            message: $"Anggaran pengadaan tiket <strong>{ticket.TicketNumber}</strong> dari <strong>{requester.FullName}</strong> telah disetujui oleh Finance dan siap diproses pembelian barangnya oleh tim Purchasing.",
                            ticketNumber: ticket.TicketNumber,
                            items: items,
                            note: approveNote
                        );
                        await emailService.SendEmailAsync(purchasingEmails, $"[ASMARA] Proses Pembelian Barang: {ticket.TicketNumber}", purchasingBody, ct);
                    }
                    else
                    {
                        var managerApprovedReqBody = EmailTemplates.Notification(
                            recipientName: requester.FullName,
                            title: "Tiket Anda Disetujui",
                            message: $"Tiket Anda telah disetujui oleh <strong>{actor.FullName}</strong> dan diteruskan ke Finance untuk peninjauan anggaran.",
                            ticketNumber: ticket.TicketNumber,
                            note: approveNote
                        );
                        await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Tiket Disetujui: {ticket.TicketNumber}", managerApprovedReqBody, ct);

                        var financeEmails = await GetEmailsByRoleAsync(Roles.Finance, ct);
                        if (financeEmails.Count == 0) financeEmails = ["finance.team@asmara.local"];

                        var financeBody = EmailTemplates.Procurement(
                            recipientName: "Tim Finance",
                            title: "Tiket Masuk: Tinjauan Anggaran Pengadaan",
                            message: $"Tiket pengadaan <strong>{ticket.TicketNumber}</strong> dari <strong>{requester.FullName}</strong> telah disetujui Manajer dan masuk ke Finance untuk ditinjau anggarannya.",
                            ticketNumber: ticket.TicketNumber,
                            items: items,
                            note: approveNote
                        );
                        await emailService.SendEmailAsync(financeEmails, $"[ASMARA] Tiket Pengadaan Masuk: {ticket.TicketNumber}", financeBody, ct);
                    }
                }
                else if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
                {
                    var maintApprovedReqBody = EmailTemplates.Notification(
                        recipientName: requester.FullName,
                        title: "Tiket Anda Disetujui",
                        message: $"Tiket Anda telah disetujui oleh <strong>{actor.FullName}</strong> dan saat ini sedang diteruskan ke tahap berikutnya.",
                        ticketNumber: ticket.TicketNumber,
                        note: approveNote
                    );
                    await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Tiket Disetujui: {ticket.TicketNumber}", maintApprovedReqBody, ct);

                    var itSupportEmails = await GetEmailsByRoleAsync(Roles.ItSupport, ct);
                    if (itSupportEmails.Count == 0) itSupportEmails = ["itsupport.team@asmara.local"];

                    var detail = notification.MaintenanceDetail ??
                        await uow.MaintenanceDetails.QueryNoTracking().Include(m => m.Asset).FirstOrDefaultAsync(m => m.TicketId == ticket.Id, ct);

                    var extraInfo = $"<strong>Pemohon:</strong> {requester.FullName}<br><strong>Asset Tag:</strong> {detail?.Asset?.AssetTag ?? "Aset Terdaftar"}";
                    var itBody = EmailTemplates.Notification(
                        recipientName: "Tim IT Support",
                        title: "Penugasan Perawatan Baru",
                        message: $"Tiket perawatan {ticket.TicketNumber} telah disetujui Manajer. Silakan buka aplikasi ASMARA untuk menugaskan teknisi penanggung jawab.",
                        ticketNumber: ticket.TicketNumber,
                        note: approveNote,
                        extraInfo: extraInfo
                    );
                    await emailService.SendEmailAsync(itSupportEmails, $"[ASMARA] Penugasan Perawatan IT: {ticket.TicketNumber}", itBody, ct);
                }
                break;

            case "cancel":
                var cancelNote = !string.IsNullOrWhiteSpace(notification.Note) 
                    ? notification.Note 
                    : "Tiket telah dibatalkan atas permintaan pemohon.";

                var cancelBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: "Tiket Dibatalkan",
                    message: $"Tiket <strong>{ticket.TicketNumber}</strong> telah berhasil dibatalkan dan status tiket kini ditutup.",
                    ticketNumber: ticket.TicketNumber,
                    note: cancelNote
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Pembatalan Tiket: {ticket.TicketNumber}", cancelBody, ct);

                var cancelManager = (ticket.CurrentTaskEmployeeId.HasValue ? await uow.Employees.GetByIdAsync(ticket.CurrentTaskEmployeeId.Value, ct) : null)
                    ?? (requester.ManagerId.HasValue ? await uow.Employees.GetByIdAsync(requester.ManagerId.Value, ct) : null);

                if (cancelManager != null && !string.IsNullOrWhiteSpace(cancelManager.Email) && cancelManager.Id != actor.Id)
                {
                    var mgrCancelBody = EmailTemplates.Notification(
                        recipientName: cancelManager.FullName,
                        title: "Tiket Dibatalkan oleh Pemohon",
                        message: $"Staf Anda, <strong>{requester.FullName}</strong>, telah membatalkan tiket <strong>{ticket.TicketNumber}</strong>.",
                        ticketNumber: ticket.TicketNumber,
                        note: cancelNote
                    );
                    await emailService.SendEmailAsync(cancelManager.Email, $"[ASMARA] Pembatalan Tiket: {ticket.TicketNumber}", mgrCancelBody, ct);
                }
                break;

            case "revise":
                var reviseNote = !string.IsNullOrWhiteSpace(notification.Note) 
                    ? notification.Note 
                    : "Harap periksa kembali detail tiket dan ajukan perbaikan data yang diperlukan.";

                var reviseBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: "Permintaan Revisi Tiket",
                    message: $"Tiket Anda membutuhkan revisi data dari <strong>{actor.FullName}</strong>. Silakan periksa catatan revisi di bawah dan ajukan kembali setelah diperbarui.",
                    ticketNumber: ticket.TicketNumber,
                    // note: notification.Note ?? "Harap periksa kembali detail tiket Anda."
                    note: reviseNote
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Permintaan Revisi: {ticket.TicketNumber}", reviseBody, ct);
                break;

            case "reject":
                var rejectNote = !string.IsNullOrWhiteSpace(notification.Note) 
                    ? notification.Note 
                    : "Pengajuan tiket tidak disetujui / dihentikan oleh peninjau.";

                var rejectBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: "Tiket Ditolak",
                    message: $"Pengajuan tiket Anda telah ditolak oleh <strong>{actor.FullName}</strong> dan tiket telah ditutup.",
                    ticketNumber: ticket.TicketNumber,
                    // note: notification.Note
                    note: rejectNote
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Tiket Ditolak: {ticket.TicketNumber}", rejectBody, ct);
                break;

            case "assignhandler":
                var assignedTech = ticket.AssignedToEmployeeId.HasValue
                    ? await uow.Employees.GetByIdAsync(ticket.AssignedToEmployeeId.Value, ct)
                    : null;

                var maintDetail = notification.MaintenanceDetail ??
                    await uow.MaintenanceDetails.QueryNoTracking().Include(m => m.Asset).FirstOrDefaultAsync(m => m.TicketId == ticket.Id, ct);

                var assetInfo = maintDetail?.Asset?.AssetTag ?? "Aset Terdaftar";
                var complaint = maintDetail?.Description ?? ticket.Description ?? "-";

                // 1. Email ke Teknisi yang ditugaskan (assignedTo)
                if (assignedTech != null && !string.IsNullOrWhiteSpace(assignedTech.Email))
                {
                    var techExtraInfo = $"<strong>Pemohon:</strong> {requester.FullName}<br><strong>Asset Tag:</strong> {assetInfo}<br><strong>Keluhan:</strong> {complaint}";
                    var techBody = EmailTemplates.Notification(
                        recipientName: assignedTech.FullName,
                        title: "Penugasan Perbaikan Unit",
                        message: $"Anda telah ditugaskan oleh IT Support (<strong>{actor.FullName}</strong>) untuk menangani perbaikan perangkat pada tiket ini. Silakan tunggu penyerahan unit dari pemohon.",
                        ticketNumber: ticket.TicketNumber,
                        note: !string.IsNullOrWhiteSpace(notification.Note) ? notification.Note : "Harap segera tindak lanjuti setelah perangkat diterima.",
                        extraInfo: techExtraInfo
                    );
                    await emailService.SendEmailAsync(assignedTech.Email, $"[ASMARA] Penugasan Perbaikan: {ticket.TicketNumber}", techBody, ct);
                }

                // 2. Email ke Pemohon (Requester) bahwa teknisi sudah ditugaskan
                var reqExtraInfo = $"<strong>Teknisi Penanggung Jawab:</strong> {assignedTech?.FullName ?? "Teknisi IT Support"}<br><strong>Asset Tag:</strong> {assetInfo}";
                var reqBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: "Teknisi Perbaikan Ditugaskan",
                    message: $"Tiket perawatan Anda telah ditugaskan kepada teknisi <strong>{assignedTech?.FullName ?? "Teknisi IT"}</strong>. Silakan serahkan perangkat Anda kepada teknisi terkait untuk proses pemeriksaan dan perbaikan.",
                    ticketNumber: ticket.TicketNumber,
                    note: notification.Note,
                    extraInfo: reqExtraInfo
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Teknisi Ditugaskan: {ticket.TicketNumber}", reqBody, ct);
                break;

            case "resubmit":
                var resubmitManager = (ticket.CurrentTaskEmployeeId.HasValue ? await uow.Employees.GetByIdAsync(ticket.CurrentTaskEmployeeId.Value, ct) : null)
                    ?? (requester.ManagerId.HasValue ? await uow.Employees.GetByIdAsync(requester.ManagerId.Value, ct) : null);

                if (resubmitManager != null && !string.IsNullOrWhiteSpace(resubmitManager.Email))
                {
                    var items = notification.ProcurementItems?.ToList() ?? 
                        await uow.ProcurementDetails.QueryNoTracking().Where(p => p.TicketId == ticket.Id).ToListAsync(ct);

                    var resubmitNote = !string.IsNullOrWhiteSpace(notification.Note)
                        ? notification.Note
                        : "Data tiket pengadaan telah diperbarui oleh pemohon dan diajukan kembali.";

                    var resubmitBody = EmailTemplates.Procurement(
                        recipientName: resubmitManager.FullName,
                        title: "Review Revisi Tiket Pengadaan",
                        message: $"Staf Anda, <strong>{requester.FullName}</strong>, telah memperbarui detail pengadaan tiket <strong>{ticket.TicketNumber}</strong> dan mengajukannya kembali untuk ditinjau.",
                        ticketNumber: ticket.TicketNumber,
                        items: items,
                        note: resubmitNote
                    );

                    await emailService.SendEmailAsync(resubmitManager.Email, $"[ASMARA] Review Revisi Tiket Pengadaan: {ticket.TicketNumber}", resubmitBody, ct);
                }
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
