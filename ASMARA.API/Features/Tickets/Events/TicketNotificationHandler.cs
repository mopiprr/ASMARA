using System.Net;
using System.Text.Json;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Models.Ticket;
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private async Task<List<string>> GetEmailsByRoleAsync(string roleCode, CancellationToken ct)
    {
        return await uow.Employees.QueryNoTracking()
            .Where(e => e.EmployeeStatus == (byte)EmployeeStatusEnum.Available
                        && !string.IsNullOrWhiteSpace(e.Email)
                        && e.Account != null
                        && e.Account!.IsActive
                        && e.Account!.Roles.Any(r => r.IsActive && r.RoleCode == roleCode))
            .Select(e => e.Email)
            .Distinct()
            .ToListAsync(ct);
    }

    private async Task<Employee?> GetActiveEmployeeAsync(Guid? employeeId, CancellationToken ct)
    {
        if (!employeeId.HasValue)
            return null;

        return await uow.Employees.QueryNoTracking()
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == employeeId.Value && e.EmployeeStatus == (byte)EmployeeStatusEnum.Available, ct);
    }

    private async Task<MaintenanceDetail?> GetMaintenanceDetailAsync(Guid ticketId, CancellationToken ct)
    {
        return await uow.MaintenanceDetails.QueryNoTracking()
            .Include(m => m.Asset)
                .ThenInclude(a => a.Item)
                    .ThenInclude(i => i.ItemCategory)
            .Include(m => m.ReplacementAsset)
                .ThenInclude(a => a!.Item)
            .FirstOrDefaultAsync(m => m.TicketId == ticketId, ct);
    }

    private static string FormatAssetSummary(MaintenanceDetail? detail)
    {
        var assetTag = detail?.Asset?.AssetTag ?? "Aset Terdaftar";
        var itemName = detail?.Asset?.Item?.ItemName;
        var categoryName = detail?.Asset?.Item?.ItemCategory?.CategoryName;

        var summary = WebUtility.HtmlEncode(assetTag);
        if (!string.IsNullOrWhiteSpace(itemName))
        {
            summary += $" - {WebUtility.HtmlEncode(itemName)}";
        }
        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            summary += $" ({WebUtility.HtmlEncode(categoryName)})";
        }

        return summary;
    }

    private static List<MaintenancePartItemRequestDto> ParseSpareParts(string? partRequirement)
    {
        if (string.IsNullOrWhiteSpace(partRequirement))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<MaintenancePartItemRequestDto>>(partRequirement, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task SendTicketCreatedNotificationAsync(
        Ticket ticket,
        Employee requester,
        Employee? directManager,
        IEnumerable<ProcurementDetail>? cachedProcurementItems,
        MaintenanceDetail? cachedMaintenanceDetail,
        string? note,
        CancellationToken ct)
    {
        var manager = directManager
            ?? await GetActiveEmployeeAsync(ticket.CurrentTaskEmployeeId, ct)
            ?? await GetActiveEmployeeAsync(requester.ManagerId, ct);

        if (ticket.TicketType == (byte)TicketTypeEnum.Procurement)
        {
            var items = cachedProcurementItems?.ToList()
                ?? await uow.ProcurementDetails.QueryNoTracking().Where(p => p.TicketId == ticket.Id).ToListAsync(ct);

            string? parentTicketNumber = null;
            if (ticket.ParentTicketId.HasValue)
            {
                parentTicketNumber = await uow.Tickets.QueryNoTracking()
                    .Where(t => t.Id == ticket.ParentTicketId.Value)
                    .Select(t => t.TicketNumber)
                    .FirstOrDefaultAsync(ct);
            }

            var childContext = string.IsNullOrWhiteSpace(parentTicketNumber)
                ? string.Empty
                : $" (pengadaan lanjutan untuk tiket perawatan <strong>{WebUtility.HtmlEncode(parentTicketNumber)}</strong>)";

            // Jika CurrentTaskEmployeeId kosong dan langsung menuju FinanceApproval (misal pemohon adalah top-level manager)
            if (manager == null || string.IsNullOrWhiteSpace(manager.Email))
            {
                var currentStep = await uow.Tickets.GetCurrentStepAsync(ticket.Id, ct);
                if (currentStep == StepCodes.FinanceApproval)
                {
                    var financeEmails = await GetEmailsByRoleAsync(Roles.Finance, ct);
                    if (financeEmails.Count == 0) financeEmails = ["finance.team@asmara.local"];

                    var financeBody = EmailTemplates.Procurement(
                        recipientName: "Tim Finance",
                        title: "Tiket Masuk: Tinjauan Anggaran Pengadaan",
                        message: $"Pengajuan pengadaan{childContext} dari <strong>{WebUtility.HtmlEncode(requester.FullName)}</strong> masuk ke tahap Finance untuk ditinjau anggarannya.",
                        ticketNumber: ticket.TicketNumber,
                        items: items,
                        note: note
                    );
                    await emailService.SendEmailAsync(financeEmails, $"[ASMARA] Tiket Pengadaan Masuk: {ticket.TicketNumber}", financeBody, ct);
                    return;
                }

                logger.LogWarning("Manager email not found for ticket {TicketNumber}", ticket.TicketNumber);
                return;
            }

            var body = EmailTemplates.Procurement(
                recipientName: manager.FullName,
                title: "Review Tiket Pengadaan",
                message: $"<strong>{WebUtility.HtmlEncode(requester.FullName)}</strong> telah mengajukan tiket pengadaan{childContext} dan menunggu peninjauan Anda.",
                ticketNumber: ticket.TicketNumber,
                items: items,
                note: note
            );

            await emailService.SendEmailAsync(manager.Email, $"[ASMARA] Review Tiket Pengadaan: {ticket.TicketNumber}", body, ct);
            return;
        }

        if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
        {
            if (manager == null || string.IsNullOrWhiteSpace(manager.Email))
            {
                logger.LogWarning("Manager email not found for ticket {TicketNumber}", ticket.TicketNumber);
                return;
            }

            var detail = cachedMaintenanceDetail?.Asset?.Item != null
                ? cachedMaintenanceDetail
                : await GetMaintenanceDetailAsync(ticket.Id, ct);

            var assetSummary = FormatAssetSummary(detail);
            var problemDesc = WebUtility.HtmlEncode(detail?.Description ?? ticket.Description ?? "-");

            var extraInfo = $"<strong>Perangkat:</strong> {assetSummary}<br><strong>Keluhan:</strong> {problemDesc}";
            var body = EmailTemplates.Notification(
                recipientName: manager.FullName,
                title: "Review Tiket Perawatan",
                message: $"Staf Anda, <strong>{WebUtility.HtmlEncode(requester.FullName)}</strong>, telah mengajukan tiket perawatan perangkat dan menunggu peninjauan Anda.",
                ticketNumber: ticket.TicketNumber,
                note: note,
                extraInfo: extraInfo
            );

            await emailService.SendEmailAsync(manager.Email, $"[ASMARA] Review Tiket Perawatan: {ticket.TicketNumber}", body, ct);
        }
    }

    // Create Ticket
    public async Task Handle(TicketCreatedEvent notification, CancellationToken ct)
    {
        await SendTicketCreatedNotificationAsync(
            notification.Ticket,
            notification.Requester,
            notification.DirectManager,
            notification.ProcurementItems,
            notification.MaintenanceDetail,
            notification.Ticket.Description,
            ct);
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
                await SendTicketCreatedNotificationAsync(
                    ticket,
                    requester,
                    null,
                    notification.ProcurementItems,
                    notification.MaintenanceDetail,
                    ticket.Description ?? notification.Note,
                    ct);
                break;

            case "approve":
            {
                var approveNote = !string.IsNullOrWhiteSpace(notification.Note)
                    ? notification.Note
                    : "Disetujui untuk diteruskan ke tahap berikutnya.";

                if (ticket.TicketType == (byte)TicketTypeEnum.Procurement)
                {
                    var currentStep = await uow.Tickets.GetCurrentStepAsync(ticket.Id, ct);
                    var items = notification.ProcurementItems
                        ?? await uow.ProcurementDetails.QueryNoTracking().Where(p => p.TicketId == ticket.Id).ToListAsync(ct);

                    if (currentStep == StepCodes.Purchasing)
                    {
                        var purchasingReqBody = EmailTemplates.Notification(
                            recipientName: requester.FullName,
                            title: "Anggaran Tiket Disetujui",
                            message: $"Anggaran tiket pengadaan Anda telah disetujui oleh Finance (<strong>{WebUtility.HtmlEncode(actor.FullName)}</strong>) dan diteruskan ke tim Purchasing untuk proses pembelian barang.",
                            ticketNumber: ticket.TicketNumber,
                            note: approveNote
                        );
                        await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Anggaran Pengadaan Disetujui: {ticket.TicketNumber}", purchasingReqBody, ct);

                        var purchasingEmails = await GetEmailsByRoleAsync(Roles.Purchasing, ct);
                        if (purchasingEmails.Count == 0) purchasingEmails = ["purchasing.team@asmara.local"];

                        var purchasingBody = EmailTemplates.Procurement(
                            recipientName: "Tim Purchasing",
                            title: "Proses Pembelian Barang Pengadaan",
                            message: $"Anggaran pengadaan tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> dari <strong>{WebUtility.HtmlEncode(requester.FullName)}</strong> telah disetujui oleh Finance dan siap diproses pembelian barangnya oleh tim Purchasing.",
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
                            message: $"Tiket Anda telah disetujui oleh <strong>{WebUtility.HtmlEncode(actor.FullName)}</strong> dan diteruskan ke Finance untuk peninjauan anggaran.",
                            ticketNumber: ticket.TicketNumber,
                            note: approveNote
                        );
                        await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Tiket Disetujui: {ticket.TicketNumber}", managerApprovedReqBody, ct);

                        var financeEmails = await GetEmailsByRoleAsync(Roles.Finance, ct);
                        if (financeEmails.Count == 0) financeEmails = ["finance.team@asmara.local"];

                        var financeBody = EmailTemplates.Procurement(
                            recipientName: "Tim Finance",
                            title: "Tiket Masuk: Tinjauan Anggaran Pengadaan",
                            message: $"Tiket pengadaan <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> dari <strong>{WebUtility.HtmlEncode(requester.FullName)}</strong> telah disetujui Manajer dan masuk ke Finance untuk ditinjau anggarannya.",
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
                        message: $"Tiket Anda telah disetujui oleh <strong>{WebUtility.HtmlEncode(actor.FullName)}</strong> dan saat ini sedang diteruskan ke tahap berikutnya.",
                        ticketNumber: ticket.TicketNumber,
                        note: approveNote
                    );
                    await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Tiket Disetujui: {ticket.TicketNumber}", maintApprovedReqBody, ct);

                    var itSupportEmails = await GetEmailsByRoleAsync(Roles.ItSupport, ct);
                    if (itSupportEmails.Count == 0) itSupportEmails = ["itsupport.team@asmara.local"];

                    var detail = await GetMaintenanceDetailAsync(ticket.Id, ct);
                    var extraInfo = $"<strong>Pemohon:</strong> {WebUtility.HtmlEncode(requester.FullName)}<br><strong>Perangkat:</strong> {FormatAssetSummary(detail)}";

                    var itBody = EmailTemplates.Notification(
                        recipientName: "Tim IT Support",
                        title: "Penugasan Perawatan Baru",
                        message: $"Tiket perawatan <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> telah disetujui Manajer. Silakan buka aplikasi ASMARA untuk menugaskan teknisi penanggung jawab.",
                        ticketNumber: ticket.TicketNumber,
                        note: approveNote,
                        extraInfo: extraInfo
                    );
                    await emailService.SendEmailAsync(itSupportEmails, $"[ASMARA] Penugasan Perawatan IT: {ticket.TicketNumber}", itBody, ct);
                }
                break;
            }

            case "cancel":
            {
                var cancelNote = !string.IsNullOrWhiteSpace(notification.Note)
                    ? notification.Note
                    : "Tiket telah dibatalkan atas permintaan pemohon.";

                var cancelBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: "Tiket Dibatalkan",
                    message: $"Tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> telah berhasil dibatalkan dan status tiket kini ditutup.",
                    ticketNumber: ticket.TicketNumber,
                    note: cancelNote
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Pembatalan Tiket: {ticket.TicketNumber}", cancelBody, ct);

                var cancelManager = await GetActiveEmployeeAsync(ticket.CurrentTaskEmployeeId, ct)
                    ?? await GetActiveEmployeeAsync(requester.ManagerId, ct);

                if (cancelManager != null && !string.IsNullOrWhiteSpace(cancelManager.Email) && cancelManager.Id != actor.Id)
                {
                    var mgrCancelBody = EmailTemplates.Notification(
                        recipientName: cancelManager.FullName,
                        title: "Tiket Dibatalkan oleh Pemohon",
                        message: $"Staf Anda, <strong>{WebUtility.HtmlEncode(requester.FullName)}</strong>, telah membatalkan tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong>.",
                        ticketNumber: ticket.TicketNumber,
                        note: cancelNote
                    );
                    await emailService.SendEmailAsync(cancelManager.Email, $"[ASMARA] Pembatalan Tiket: {ticket.TicketNumber}", mgrCancelBody, ct);
                }
                break;
            }

            case "revise":
            {
                var reviseNote = !string.IsNullOrWhiteSpace(notification.Note)
                    ? notification.Note
                    : "Harap periksa kembali detail tiket dan ajukan perbaikan data yang diperlukan.";

                var reviseBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: "Permintaan Revisi Tiket",
                    message: $"Tiket Anda membutuhkan revisi data dari <strong>{WebUtility.HtmlEncode(actor.FullName)}</strong>. Silakan periksa catatan revisi di bawah dan ajukan kembali setelah diperbarui.",
                    ticketNumber: ticket.TicketNumber,
                    note: reviseNote,
                    headerColor: "#d97706"
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Permintaan Revisi: {ticket.TicketNumber}", reviseBody, ct);
                break;
            }

            case "reject":
            {
                var rejectNote = !string.IsNullOrWhiteSpace(notification.Note)
                    ? notification.Note
                    : "Pengajuan tiket tidak disetujui / dihentikan oleh peninjau.";

                var rejectMessage = $"Pengajuan tiket Anda telah ditolak oleh <strong>{WebUtility.HtmlEncode(actor.FullName)}</strong> dan tiket telah ditutup.";
                if (ticket.ParentTicketId.HasValue)
                {
                    var parentNumber = await uow.Tickets.QueryNoTracking()
                        .Where(t => t.Id == ticket.ParentTicketId.Value)
                        .Select(t => t.TicketNumber)
                        .FirstOrDefaultAsync(ct);

                    if (!string.IsNullOrWhiteSpace(parentNumber))
                    {
                        rejectMessage += $" Tiket perawatan induk <strong>{WebUtility.HtmlEncode(parentNumber)}</strong> telah dikembalikan ke tahap sebelumnya untuk ditindaklanjuti kembali.";
                    }
                }

                var rejectBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: "Tiket Ditolak",
                    message: rejectMessage,
                    ticketNumber: ticket.TicketNumber,
                    note: rejectNote,
                    headerColor: "#dc2626"
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Tiket Ditolak: {ticket.TicketNumber}", rejectBody, ct);
                break;
            }

            case "assignhandler":
            {
                var assignedTech = await GetActiveEmployeeAsync(ticket.AssignedToEmployeeId, ct);
                var maintDetail = await GetMaintenanceDetailAsync(ticket.Id, ct);
                var assetSummary = FormatAssetSummary(maintDetail);
                var complaint = WebUtility.HtmlEncode(maintDetail?.Description ?? ticket.Description ?? "-");

                if (assignedTech != null && !string.IsNullOrWhiteSpace(assignedTech.Email))
                {
                    var techExtraInfo = $"<strong>Pemohon:</strong> {WebUtility.HtmlEncode(requester.FullName)}<br><strong>Perangkat:</strong> {assetSummary}<br><strong>Keluhan:</strong> {complaint}";
                    var techBody = EmailTemplates.Notification(
                        recipientName: assignedTech.FullName,
                        title: "Penugasan Perbaikan Unit",
                        message: $"Anda telah ditugaskan oleh IT Support (<strong>{WebUtility.HtmlEncode(actor.FullName)}</strong>) untuk menangani perbaikan perangkat pada tiket ini. Silakan tunggu penyerahan unit dari pemohon.",
                        ticketNumber: ticket.TicketNumber,
                        note: !string.IsNullOrWhiteSpace(notification.Note) ? notification.Note : "Harap segera tindak lanjuti setelah perangkat diterima.",
                        extraInfo: techExtraInfo
                    );
                    await emailService.SendEmailAsync(assignedTech.Email, $"[ASMARA] Penugasan Perbaikan: {ticket.TicketNumber}", techBody, ct);
                }

                var reqExtraInfo = $"<strong>Teknisi Penanggung Jawab:</strong> {WebUtility.HtmlEncode(assignedTech?.FullName ?? "Teknisi IT Support")}<br><strong>Perangkat:</strong> {assetSummary}";
                var reqBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: "Teknisi Perbaikan Ditugaskan",
                    message: $"Tiket perawatan Anda telah ditugaskan kepada teknisi <strong>{WebUtility.HtmlEncode(assignedTech?.FullName ?? "Teknisi IT")}</strong>. Silakan serahkan perangkat Anda kepada teknisi terkait untuk proses pemeriksaan dan perbaikan.",
                    ticketNumber: ticket.TicketNumber,
                    note: notification.Note,
                    extraInfo: reqExtraInfo
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Teknisi Ditugaskan: {ticket.TicketNumber}", reqBody, ct);
                break;
            }

            case "resubmit":
            {
                var resubmitManager = await GetActiveEmployeeAsync(ticket.CurrentTaskEmployeeId, ct)
                    ?? await GetActiveEmployeeAsync(requester.ManagerId, ct);

                if (resubmitManager != null && !string.IsNullOrWhiteSpace(resubmitManager.Email))
                {
                    var items = notification.ProcurementItems?.ToList()
                        ?? await uow.ProcurementDetails.QueryNoTracking().Where(p => p.TicketId == ticket.Id).ToListAsync(ct);

                    var resubmitNote = !string.IsNullOrWhiteSpace(notification.Note)
                        ? notification.Note
                        : "Data tiket pengadaan telah diperbarui oleh pemohon dan diajukan kembali.";

                    var resubmitBody = EmailTemplates.Procurement(
                        recipientName: resubmitManager.FullName,
                        title: "Review Revisi Tiket Pengadaan",
                        message: $"Staf Anda, <strong>{WebUtility.HtmlEncode(requester.FullName)}</strong>, telah memperbarui detail pengadaan tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> dan mengajukannya kembali untuk ditinjau.",
                        ticketNumber: ticket.TicketNumber,
                        items: items,
                        note: resubmitNote
                    );

                    await emailService.SendEmailAsync(resubmitManager.Email, $"[ASMARA] Review Revisi Tiket Pengadaan: {ticket.TicketNumber}", resubmitBody, ct);
                }
                break;
            }

            case "send":
            {
                if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
                {
                    var sendStep = await uow.Tickets.GetCurrentStepAsync(ticket.Id, ct);
                    var detail = await GetMaintenanceDetailAsync(ticket.Id, ct);
                    var assetSummary = FormatAssetSummary(detail);

                    if (sendStep == StepCodes.HandlerReceiveItem)
                    {
                        var assignedTech = await GetActiveEmployeeAsync(ticket.AssignedToEmployeeId, ct);

                        if (assignedTech != null && !string.IsNullOrWhiteSpace(assignedTech.Email))
                        {
                            var complaint = WebUtility.HtmlEncode(detail?.Description ?? ticket.Description ?? "-");
                            var sendExtraInfo = $"<strong>Pemohon:</strong> {WebUtility.HtmlEncode(requester.FullName)}<br><strong>Perangkat:</strong> {assetSummary}<br><strong>Keluhan:</strong> {complaint}";
                            var sendBody = EmailTemplates.Notification(
                                recipientName: assignedTech.FullName,
                                title: "Unit Perangkat Diserahkan",
                                message: $"Pemohon <strong>{WebUtility.HtmlEncode(requester.FullName)}</strong> telah mengonfirmasi penyerahan unit perangkat untuk tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong>. Silakan periksa dan konfirmasi penerimaan unit di sistem ASMARA.",
                                ticketNumber: ticket.TicketNumber,
                                note: notification.Note,
                                extraInfo: sendExtraInfo
                            );
                            await emailService.SendEmailAsync(assignedTech.Email, $"[ASMARA] Penyerahan Unit: {ticket.TicketNumber}", sendBody, ct);
                        }
                    }
                    else if (sendStep == StepCodes.RequesterReceiveItem)
                    {
                        var isReplacement = detail?.ReplacementAssetId != null;
                        var replacementInfo = isReplacement
                            ? $"{WebUtility.HtmlEncode(detail?.ReplacementAsset?.AssetTag ?? "-")} ({WebUtility.HtmlEncode(detail?.ReplacementAsset?.Item?.ItemName ?? "Unit Pengganti")})"
                            : null;

                        var sendBackExtra = isReplacement
                            ? $"<strong>Petugas:</strong> {WebUtility.HtmlEncode(actor.FullName)}<br><strong>Unit Lama:</strong> {assetSummary}<br><strong>Unit Pengganti:</strong> {replacementInfo}"
                            : $"<strong>Petugas:</strong> {WebUtility.HtmlEncode(actor.FullName)}<br><strong>Perangkat:</strong> {assetSummary}";

                        var sendBackTitle = isReplacement ? "Unit Pengganti Siap Diterima" : "Perangkat Siap Diterima Kembali";
                        var sendBackMsg = isReplacement
                            ? $"Unit pengganti untuk tiket perawatan <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> telah dikirimkan oleh <strong>{WebUtility.HtmlEncode(actor.FullName)}</strong>. Silakan periksa fisik unit dan konfirmasi penerimaan di sistem ASMARA."
                            : $"Perangkat untuk tiket perawatan <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> telah dikirimkan kembali oleh <strong>{WebUtility.HtmlEncode(actor.FullName)}</strong>. Silakan periksa fisik unit dan konfirmasi penerimaan di sistem ASMARA.";

                        var sendBackBody = EmailTemplates.Notification(
                            recipientName: requester.FullName,
                            title: sendBackTitle,
                            message: sendBackMsg,
                            ticketNumber: ticket.TicketNumber,
                            note: notification.Note,
                            extraInfo: sendBackExtra
                        );
                        await emailService.SendEmailAsync(requester.Email, $"[ASMARA] {sendBackTitle}: {ticket.TicketNumber}", sendBackBody, ct);
                    }
                }
                break;
            }

            case "receive":
            {
                if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
                {
                    var receiveStep = await uow.Tickets.GetCurrentStepAsync(ticket.Id, ct);
                    var detail = await GetMaintenanceDetailAsync(ticket.Id, ct);
                    var assetSummary = FormatAssetSummary(detail);

                    if (receiveStep == StepCodes.RepairInProgress)
                    {
                        var fromStep = await uow.TicketWorkflowLogs.QueryNoTracking()
                            .Where(l => l.TicketId == ticket.Id && l.ToStep == StepCodes.RepairInProgress)
                            .OrderByDescending(l => l.CreatedAt)
                            .ThenByDescending(l => l.Id)
                            .Select(l => l.FromStep)
                            .FirstOrDefaultAsync(ct);

                        if (fromStep == StepCodes.HandlerReceivePart)
                        {
                            var partReceivedExtra = $"<strong>Teknisi:</strong> {WebUtility.HtmlEncode(actor.FullName)}<br><strong>Perangkat:</strong> {assetSummary}";
                            var partReceivedBody = EmailTemplates.Notification(
                                recipientName: requester.FullName,
                                title: "Suku Cadang Diterima - Perbaikan Dilanjutkan",
                                message: $"Teknisi <strong>{WebUtility.HtmlEncode(actor.FullName)}</strong> telah menerima suku cadang dari tim Inventory untuk tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> dan saat ini sedang melanjutkan proses perbaikan perangkat Anda.",
                                ticketNumber: ticket.TicketNumber,
                                note: notification.Note,
                                extraInfo: partReceivedExtra
                            );
                            await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Perbaikan Dilanjutkan: {ticket.TicketNumber}", partReceivedBody, ct);
                        }
                        else
                        {
                            var receiveExtra = $"<strong>Teknisi:</strong> {WebUtility.HtmlEncode(actor.FullName)}<br><strong>Perangkat:</strong> {assetSummary}";
                            var receiveBody = EmailTemplates.Notification(
                                recipientName: requester.FullName,
                                title: "Perangkat Diterima oleh Teknisi",
                                message: $"Perangkat Anda untuk tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> telah diterima oleh teknisi <strong>{WebUtility.HtmlEncode(actor.FullName)}</strong> dan saat ini sedang dalam proses pemeriksaan / perbaikan.",
                                ticketNumber: ticket.TicketNumber,
                                note: notification.Note,
                                extraInfo: receiveExtra
                            );
                            await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Perangkat Diterima Teknisi: {ticket.TicketNumber}", receiveBody, ct);
                        }
                    }
                    else if (receiveStep == StepCodes.RequesterClose)
                    {
                        var recCloseExtra = $"<strong>Perangkat:</strong> {assetSummary}";
                        var recCloseBody = EmailTemplates.Notification(
                            recipientName: requester.FullName,
                            title: "Penerimaan Unit Terkonfirmasi",
                            message: $"Anda telah mengonfirmasi penerimaan unit perangkat untuk tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong>. Silakan pastikan kondisi perangkat berfungsi dengan baik dan lakukan penutupan tiket (Close) di sistem ASMARA.",
                            ticketNumber: ticket.TicketNumber,
                            note: notification.Note,
                            extraInfo: recCloseExtra
                        );
                        await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Konfirmasi Penerimaan Unit: {ticket.TicketNumber}", recCloseBody, ct);
                    }
                }
                break;
            }

            case "needpart":
            {
                if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
                {
                    var detail = await GetMaintenanceDetailAsync(ticket.Id, ct);
                    var assetSummary = FormatAssetSummary(detail);
                    var parts = ParseSpareParts(detail?.PartRequirement);

                    var inventoryEmails = await GetEmailsByRoleAsync(Roles.Inventory, ct);
                    if (inventoryEmails.Count == 0) inventoryEmails = ["inventory.team@asmara.local"];

                    var partExtra = $"<strong>Teknisi:</strong> {WebUtility.HtmlEncode(actor.FullName)}<br><strong>Perangkat:</strong> {assetSummary}";
                    string partBody;

                    if (parts.Count > 0)
                    {
                        partBody = EmailTemplates.MaintenanceParts(
                            recipientName: "Tim Inventory",
                            title: "Permintaan Kebutuhan Suku Cadang",
                            message: $"Teknisi <strong>{WebUtility.HtmlEncode(actor.FullName)}</strong> membutuhkan suku cadang berikut untuk perbaikan perangkat pada tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong>. Silakan periksa ketersediaan stok di gudang inventaris.",
                            ticketNumber: ticket.TicketNumber,
                            parts: parts,
                            note: notification.Note,
                            extraInfo: partExtra
                        );
                    }
                    else
                    {
                        var legacyPartReq = WebUtility.HtmlEncode(!string.IsNullOrWhiteSpace(detail?.PartRequirement) ? detail.PartRequirement : "-");
                        partBody = EmailTemplates.Notification(
                            recipientName: "Tim Inventory",
                            title: "Permintaan Kebutuhan Suku Cadang",
                            message: $"Teknisi <strong>{WebUtility.HtmlEncode(actor.FullName)}</strong> membutuhkan suku cadang untuk perbaikan perangkat pada tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong>. Silakan periksa ketersediaan stok di gudang inventaris.",
                            ticketNumber: ticket.TicketNumber,
                            note: notification.Note,
                            extraInfo: $"{partExtra}<br><strong>Kebutuhan Part:</strong> {legacyPartReq}"
                        );
                    }

                    await emailService.SendEmailAsync(inventoryEmails, $"[ASMARA] Permintaan Suku Cadang: {ticket.TicketNumber}", partBody, ct);
                }
                break;
            }

            case "partavailable":
            {
                if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
                {
                    var assignedTech = await GetActiveEmployeeAsync(ticket.AssignedToEmployeeId, ct);
                    if (assignedTech != null && !string.IsNullOrWhiteSpace(assignedTech.Email))
                    {
                        var detail = await GetMaintenanceDetailAsync(ticket.Id, ct);
                        var assetSummary = FormatAssetSummary(detail);
                        var parts = ParseSpareParts(detail?.PartRequirement);

                        var partAvailExtra = $"<strong>Petugas Inventory:</strong> {WebUtility.HtmlEncode(actor.FullName)}<br><strong>Perangkat:</strong> {assetSummary}";
                        var partAvailBody = EmailTemplates.MaintenanceParts(
                            recipientName: assignedTech.FullName,
                            title: "Suku Cadang Siap Diterima",
                            message: $"Suku cadang yang Anda minta untuk tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> telah disiapkan dan dikirimkan oleh tim Inventory (<strong>{WebUtility.HtmlEncode(actor.FullName)}</strong>). Silakan ambil barang dan lakukan konfirmasi penerimaan suku cadang (<em>Receive Part</em>) di sistem ASMARA.",
                            ticketNumber: ticket.TicketNumber,
                            parts: parts,
                            note: notification.Note,
                            extraInfo: partAvailExtra,
                            headerColor: "#16a34a"
                        );
                        await emailService.SendEmailAsync(assignedTech.Email, $"[ASMARA] Suku Cadang Siap Diterima: {ticket.TicketNumber}", partAvailBody, ct);
                    }
                }
                break;
            }

            case "partunavailable":
            {
                if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
                {
                    var assignedTech = await GetActiveEmployeeAsync(ticket.AssignedToEmployeeId, ct);
                    if (assignedTech != null && !string.IsNullOrWhiteSpace(assignedTech.Email))
                    {
                        var detail = await GetMaintenanceDetailAsync(ticket.Id, ct);
                        var assetSummary = FormatAssetSummary(detail);
                        var parts = ParseSpareParts(detail?.PartRequirement);

                        var partUnavailExtra = $"<strong>Petugas Inventory:</strong> {WebUtility.HtmlEncode(actor.FullName)}<br><strong>Perangkat:</strong> {assetSummary}";
                        var partUnavailBody = EmailTemplates.MaintenanceParts(
                            recipientName: assignedTech.FullName,
                            title: "Stok Suku Cadang Tidak Tersedia",
                            message: $"Tim Inventory (<strong>{WebUtility.HtmlEncode(actor.FullName)}</strong>) menginformasikan bahwa stok suku cadang untuk tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> saat ini tidak tersedia di gudang. Silakan ajukan pembelian suku cadang melalui aksi <strong>Order Part</strong> di sistem ASMARA.",
                            ticketNumber: ticket.TicketNumber,
                            parts: parts,
                            note: notification.Note,
                            extraInfo: partUnavailExtra,
                            headerColor: "#d97706"
                        );
                        await emailService.SendEmailAsync(assignedTech.Email, $"[ASMARA] Stok Part Kosong - Perlu Order Part: {ticket.TicketNumber}", partUnavailBody, ct);
                    }
                }
                break;
            }

            case "orderpart":
            {
                if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
                {
                    var detail = await GetMaintenanceDetailAsync(ticket.Id, ct);
                    var assetSummary = FormatAssetSummary(detail);
                    var orderExtra = $"<strong>Teknisi:</strong> {WebUtility.HtmlEncode(actor.FullName)}<br><strong>Perangkat:</strong> {assetSummary}";

                    var orderBody = EmailTemplates.Notification(
                        recipientName: requester.FullName,
                        title: "Pengajuan Pengadaan Suku Cadang",
                        message: $"Teknisi <strong>{WebUtility.HtmlEncode(actor.FullName)}</strong> telah mengajukan pengadaan suku cadang untuk perbaikan perangkat pada tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> karena stok di gudang sedang kosong. Perbaikan akan dilanjutkan setelah suku cadang tiba.",
                        ticketNumber: ticket.TicketNumber,
                        note: notification.Note,
                        extraInfo: orderExtra
                    );
                    await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Menunggu Pengadaan Suku Cadang: {ticket.TicketNumber}", orderBody, ct);
                }
                break;
            }

            case "unrepairable":
            {
                if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
                {
                    var detail = await GetMaintenanceDetailAsync(ticket.Id, ct);
                    var reqWithDept = await GetActiveEmployeeAsync(requester.Id, ct);
                    var deptName = reqWithDept?.Department?.DepartmentName;
                    var requesterDisplay = string.IsNullOrWhiteSpace(deptName)
                        ? WebUtility.HtmlEncode(requester.FullName)
                        : $"{WebUtility.HtmlEncode(requester.FullName)} ({WebUtility.HtmlEncode(deptName)})";

                    var assetSummary = FormatAssetSummary(detail);
                    var initialProblem = WebUtility.HtmlEncode(detail?.Description ?? ticket.Description ?? "-");
                    var handlerNote = !string.IsNullOrWhiteSpace(detail?.HandlerNote)
                        ? detail.HandlerNote
                        : (notification.Note ?? "Perangkat mengalami kerusakan fatal dan tidak dapat diperbaiki.");

                    var unrepairExtra =
                        $"<strong>Pemohon:</strong> {requesterDisplay}<br>" +
                        $"<strong>Teknisi:</strong> {WebUtility.HtmlEncode(actor.FullName)}<br>" +
                        $"<strong>Perangkat:</strong> {assetSummary}<br>" +
                        $"<strong>Keluhan Awal:</strong> {initialProblem}";

                    // 1. Kirim permintaan keputusan (Dispose / Archive) ke Division Manager yang dituju
                    var targetDivManager = await GetActiveEmployeeAsync(ticket.CurrentTaskEmployeeId, ct);
                    if (targetDivManager != null && !string.IsNullOrWhiteSpace(targetDivManager.Email))
                    {
                        var divMgrBody = EmailTemplates.Notification(
                            recipientName: targetDivManager.FullName,
                            title: "Peninjauan Aset Rusak Total (Unrepairable)",
                            message: $"Perangkat pada tiket perawatan <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> dinyatakan tidak dapat diperbaiki oleh teknisi <strong>{WebUtility.HtmlEncode(actor.FullName)}</strong>. Menunggu keputusan Anda untuk pemusnahan (<strong>Dispose</strong>) atau pengarsipan (<strong>Archive</strong>).",
                            ticketNumber: ticket.TicketNumber,
                            note: handlerNote,
                            extraInfo: unrepairExtra,
                            headerColor: "#dc2626"
                        );
                        await emailService.SendEmailAsync(targetDivManager.Email, $"[ASMARA] Review Aset Rusak Total: {ticket.TicketNumber}", divMgrBody, ct);
                    }
                    else
                    {
                        var divMgrEmails = await GetEmailsByRoleAsync(Roles.DivisionManager, ct);
                        if (divMgrEmails.Count == 0) divMgrEmails = ["division.manager@asmara.local"];

                        var divMgrBody = EmailTemplates.Notification(
                            recipientName: "Division Manager",
                            title: "Peninjauan Aset Rusak Total (Unrepairable)",
                            message: $"Perangkat pada tiket perawatan <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> dinyatakan tidak dapat diperbaiki oleh teknisi <strong>{WebUtility.HtmlEncode(actor.FullName)}</strong>. Menunggu keputusan Anda untuk pemusnahan (<strong>Dispose</strong>) atau pengarsipan (<strong>Archive</strong>).",
                            ticketNumber: ticket.TicketNumber,
                            note: handlerNote,
                            extraInfo: unrepairExtra,
                            headerColor: "#dc2626"
                        );
                        await emailService.SendEmailAsync(divMgrEmails, $"[ASMARA] Review Aset Rusak Total: {ticket.TicketNumber}", divMgrBody, ct);
                    }

                    // 2. Kirim pemberitahuan (FYI saja) ke Manajer IT Support
                    var itSupportEmails = await GetEmailsByRoleAsync(Roles.ItSupport, ct);
                    if (itSupportEmails.Count == 0) itSupportEmails = ["itsupport.team@asmara.local"];

                    var divMgrName = targetDivManager?.FullName ?? "Division Manager";
                    var itFyiBody = EmailTemplates.Notification(
                        recipientName: "Manajer IT Support",
                        title: "Informasi Aset Tidak Dapat Diperbaiki (FYI)",
                        message: $"Sebagai pemberitahuan, perangkat pada tiket perawatan <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> dinyatakan tidak dapat diperbaiki (<em>Unrepairable</em>) oleh teknisi <strong>{WebUtility.HtmlEncode(actor.FullName)}</strong> dan telah diteruskan ke <strong>{WebUtility.HtmlEncode(divMgrName)}</strong> untuk keputusan Dispose/Archive. Tidak ada tindakan yang diperlukan dari Anda pada tahap ini.",
                        ticketNumber: ticket.TicketNumber,
                        note: handlerNote,
                        extraInfo: unrepairExtra,
                        headerColor: "#d97706"
                    );
                    await emailService.SendEmailAsync(itSupportEmails, $"[ASMARA] [FYI] Aset Tidak Dapat Diperbaiki: {ticket.TicketNumber}", itFyiBody, ct);
                }
                break;
            }

            case "dispose":
            case "archive":
            {
                if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
                {
                    var isDispose = action == "dispose";
                    var actionText = isDispose ? "Pemusnahan (Dispose)" : "Pengarsipan/Kanibalisasi (Archive)";
                    var detail = await GetMaintenanceDetailAsync(ticket.Id, ct);
                    var assetSummary = FormatAssetSummary(detail);
                    var invActionExtra = $"<strong>Perangkat:</strong> {assetSummary}<br><strong>Keputusan Oleh:</strong> {WebUtility.HtmlEncode(actor.FullName)}";

                    var inventoryEmails = await GetEmailsByRoleAsync(Roles.Inventory, ct);
                    if (inventoryEmails.Count == 0) inventoryEmails = ["inventory.team@asmara.local"];

                    var invActionBody = EmailTemplates.Notification(
                        recipientName: "Tim Inventory",
                        title: $"Instruksi {actionText}",
                        message: $"Manajer Divisi (<strong>{WebUtility.HtmlEncode(actor.FullName)}</strong>) telah menyetujui tindakan <strong>{actionText}</strong> untuk aset pada tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong>. Silakan tindak lanjuti pengelolaan fisik aset dan penyiapan unit pengganti di gudang inventaris.",
                        ticketNumber: ticket.TicketNumber,
                        note: notification.Note,
                        extraInfo: invActionExtra
                    );
                    await emailService.SendEmailAsync(inventoryEmails, $"[ASMARA] Instruksi {actionText}: {ticket.TicketNumber}", invActionBody, ct);
                }
                break;
            }

            case "replacementavailable":
            {
                if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
                {
                    var detail = await GetMaintenanceDetailAsync(ticket.Id, ct);
                    var oldAssetSummary = FormatAssetSummary(detail);
                    var replacementTag = detail?.ReplacementAsset?.AssetTag ?? "Unit Pengganti";
                    var replacementName = detail?.ReplacementAsset?.Item?.ItemName;
                    var replacementDisplay = string.IsNullOrWhiteSpace(replacementName)
                        ? WebUtility.HtmlEncode(replacementTag)
                        : $"{WebUtility.HtmlEncode(replacementTag)} - {WebUtility.HtmlEncode(replacementName)}";

                    var replaceExtra =
                        $"<strong>Petugas Inventory:</strong> {WebUtility.HtmlEncode(actor.FullName)}<br>" +
                        $"<strong>Unit Lama:</strong> {oldAssetSummary}<br>" +
                        $"<strong>Unit Pengganti:</strong> {replacementDisplay}";

                    var replaceBody = EmailTemplates.Notification(
                        recipientName: requester.FullName,
                        title: "Unit Pengganti Tersedia & Siap Diterima",
                        message: $"Kabar baik! Karena perangkat lama Anda tidak dapat diperbaiki, tim Inventory (<strong>{WebUtility.HtmlEncode(actor.FullName)}</strong>) telah menyiapkan unit pengganti dari stok gudang untuk tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong>. Silakan ambil unit pengganti dan konfirmasi penerimaan di sistem ASMARA.",
                        ticketNumber: ticket.TicketNumber,
                        note: notification.Note,
                        extraInfo: replaceExtra,
                        headerColor: "#16a34a"
                    );
                    await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Unit Pengganti Siap Diterima: {ticket.TicketNumber}", replaceBody, ct);
                }
                break;
            }

            case "replacementunavailable":
            {
                if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance)
                {
                    var detail = await GetMaintenanceDetailAsync(ticket.Id, ct);
                    var assetSummary = FormatAssetSummary(detail);
                    var replaceUnavailExtra = $"<strong>Petugas Inventory:</strong> {WebUtility.HtmlEncode(actor.FullName)}<br><strong>Unit Lama:</strong> {assetSummary}";

                    var replaceUnavailBody = EmailTemplates.Notification(
                        recipientName: requester.FullName,
                        title: "Pengajuan Pengadaan Unit Pengganti",
                        message: $"Stok unit pengganti untuk tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> saat ini tidak tersedia di gudang Inventory. Tim Inventory (<strong>{WebUtility.HtmlEncode(actor.FullName)}</strong>) telah mengajukan pengadaan unit pengganti baru. Anda akan mendapatkan informasi lebih lanjut setelah unit pengganti tersedia.",
                        ticketNumber: ticket.TicketNumber,
                        note: notification.Note,
                        extraInfo: replaceUnavailExtra
                    );
                    await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Pengadaan Unit Pengganti Diajukan: {ticket.TicketNumber}", replaceUnavailBody, ct);
                }
                break;
            }

            case "repaired":
            {
                var repairBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: "Perbaikan Perangkat Selesai",
                    message: $"Perangkat Anda pada tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> telah selesai diperbaiki oleh teknisi (<strong>{WebUtility.HtmlEncode(actor.FullName)}</strong>) dan sedang disiapkan untuk diserahkan kembali.",
                    ticketNumber: ticket.TicketNumber,
                    note: notification.Note,
                    headerColor: "#16a34a"
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Perbaikan Perangkat Selesai: {ticket.TicketNumber}", repairBody, ct);
                break;
            }

            case "completed":
            case "receivegoods":
            {
                if (ticket.TicketType == (byte)TicketTypeEnum.Procurement && ticket.ParentTicketId.HasValue)
                {
                    var parentTicket = await uow.Tickets.QueryNoTracking()
                        .Include(t => t.Employee)
                        .FirstOrDefaultAsync(t => t.Id == ticket.ParentTicketId.Value, ct);

                    if (parentTicket != null)
                    {
                        var items = notification.ProcurementItems?.ToList()
                            ?? await uow.ProcurementDetails.QueryNoTracking().Where(p => p.TicketId == ticket.Id).ToListAsync(ct);

                        var parentStep = await uow.Tickets.GetCurrentStepAsync(parentTicket.Id, ct);
                        var parentExtra = $"<strong>Tiket Perawatan Induk:</strong> {WebUtility.HtmlEncode(parentTicket.TicketNumber)}<br><strong>Pemohon Awal:</strong> {WebUtility.HtmlEncode(parentTicket.Employee.FullName)}";

                        if (parentStep == StepCodes.InventorySendReplacement)
                        {
                            var inventoryEmails = await GetEmailsByRoleAsync(Roles.Inventory, ct);
                            if (inventoryEmails.Count == 0 && !string.IsNullOrWhiteSpace(requester.Email))
                            {
                                inventoryEmails = [requester.Email];
                            }

                            var invReadyBody = EmailTemplates.Procurement(
                                recipientName: "Tim Inventory",
                                title: "Unit Pengganti Telah Tiba dari Purchasing",
                                message: $"Unit pengganti yang dipesan melalui pengadaan <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> telah tiba di gudang Inventory. Silakan lakukan pengiriman unit pengganti (<em>Send Replacement</em>) kepada pemohon pada tiket perawatan induk <strong>{WebUtility.HtmlEncode(parentTicket.TicketNumber)}</strong>.",
                                ticketNumber: ticket.TicketNumber,
                                items: items,
                                note: notification.Note,
                                extraInfo: parentExtra
                            );
                            await emailService.SendEmailAsync(inventoryEmails, $"[ASMARA] Unit Pengganti Tiba: {parentTicket.TicketNumber}", invReadyBody, ct);
                        }
                        else
                        {
                            var tech = await GetActiveEmployeeAsync(parentTicket.AssignedToEmployeeId, ct) ?? requester;
                            var techBody = EmailTemplates.Procurement(
                                recipientName: tech.FullName,
                                title: "Suku Cadang Pesanan Telah Tiba",
                                message: $"Suku cadang yang dipesan melalui pengadaan <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> telah tiba di gudang Inventory. Perbaikan pada tiket perawatan induk <strong>{WebUtility.HtmlEncode(parentTicket.TicketNumber)}</strong> kini dapat dilanjutkan kembali.",
                                ticketNumber: ticket.TicketNumber,
                                items: items,
                                note: notification.Note,
                                extraInfo: parentExtra
                            );
                            await emailService.SendEmailAsync(tech.Email, $"[ASMARA] Suku Cadang Tiba untuk {parentTicket.TicketNumber}", techBody, ct);
                        }

                        break;
                    }
                }

                var compTitle = ticket.TicketType == (byte)TicketTypeEnum.Procurement
                    ? "Barang Pengadaan Tiba di Inventory"
                    : "Penyelesaian Tiket";

                var compMessage = ticket.TicketType == (byte)TicketTypeEnum.Procurement
                    ? "Barang yang diajukan telah dibeli oleh Purchasing dan sudah tiba di gudang Inventory. Tiket pengadaan Anda kini telah selesai."
                    : $"Tiket perawatan <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> telah selesai diproses.";

                var compBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: compTitle,
                    message: compMessage,
                    ticketNumber: ticket.TicketNumber,
                    note: notification.Note,
                    headerColor: "#16a34a"
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] {compTitle}: {ticket.TicketNumber}", compBody, ct);
                break;
            }

            case "close":
            {
                var closeNote = !string.IsNullOrWhiteSpace(notification.Note)
                    ? notification.Note
                    : "Tiket telah selesai dan ditutup secara resmi.";

                var closeBody = EmailTemplates.Notification(
                    recipientName: requester.FullName,
                    title: "Tiket Telah Ditutup",
                    message: $"Tiket <strong>{WebUtility.HtmlEncode(ticket.TicketNumber)}</strong> telah resmi ditutup. Terima kasih telah menggunakan layanan sistem ASMARA.",
                    ticketNumber: ticket.TicketNumber,
                    note: closeNote,
                    headerColor: "#16a34a"
                );
                await emailService.SendEmailAsync(requester.Email, $"[ASMARA] Tiket Resmi Ditutup: {ticket.TicketNumber}", closeBody, ct);
                break;
            }

            default:
                logger.LogInformation("Action {Action} does not have an email trigger defined.", notification.Action);
                break;
        }
    }
}
