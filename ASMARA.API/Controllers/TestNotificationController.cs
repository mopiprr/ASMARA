using ASMARA.API.Entities;
using ASMARA.API.Features.Tickets.Events;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[AllowAnonymous] // SEMENTARA
[Route("api/test-notification")]
public class TestNotificationController(IMediator mediator) : ControllerBase
{
    // 1. Test Email: Pengajuan Tiket Pengadaan (Multi-Item)
    [HttpPost("procurement-created")]
    public async Task<IActionResult> TestProcurementCreated(CancellationToken ct)
    {
        var requester = new Employee
        {
            Id = Guid.CreateVersion7(),
            FullName = "Kambing Requester",
            Email = "kambing@asmara.local"
        };

        var manager = new Employee
        {
            Id = Guid.CreateVersion7(),
            FullName = "Pak Gajah (Dept Manager)",
            Email = "gajah.manager@asmara.local"
        };

        var ticket = new ASMARA.API.Entities.Ticket
        {
            Id = Guid.CreateVersion7(),
            TicketNumber = "TCK-PROC-2026-0001",
            TicketType = 2, // Procurement
            Subject = "Pengadaan Laptop & Perlengkapan Tim Dev",
            Description = "Pengadaan perangkat kerja baru untuk batch engineer baru."
        };

        var items = new List<ProcurementDetail>
        {
            new() { ItemName = "ThinkPad T14 Gen 4 Core i7 32GB", Quantity = 2, Price = 18500000 },
            new() { ItemName = "Dell 24 Inch Ultrasharp U2422H", Quantity = 2, Price = 3200000 },
            new() { ItemName = "Logitech MX Master 3S Wireless Mouse", Quantity = 2, Price = 1450000 }
        };

        await mediator.Publish(new TicketCreatedEvent(ticket, requester, manager, items), ct);

        return Ok(new
        {
            Status = "Success",
            Message = "Email pengajuan tiket pengadaan berhasil dikirim ke Papercut!",
            Recipient = manager.Email,
            TicketNumber = ticket.TicketNumber
        });
    }

    // 2. Test Email: Pengajuan Tiket Perawatan (Maintenance)
    [HttpPost("maintenance-created")]
    public async Task<IActionResult> TestMaintenanceCreated(CancellationToken ct)
    {
        var requester = new Employee
        {
            Id = Guid.CreateVersion7(),
            FullName = "Babi Requester",
            Email = "babi@asmara.local"
        };

        var manager = new Employee
        {
            Id = Guid.CreateVersion7(),
            FullName = "Pak Siput (Dept Manager)",
            Email = "siput.manager@asmara.local"
        };

        var ticket = new ASMARA.API.Entities.Ticket
        {
            Id = Guid.CreateVersion7(),
            TicketNumber = "TCK-MAINT-2026-0001",
            TicketType = 1, // Maintenance
            Subject = "Laptop Mati Total & Blue Screen Berulang",
            Description = "Perangkat mendadak restart dan muncul layar biru IRQL_NOT_LESS_OR_EQUAL."
        };

        var maintenance = new MaintenanceDetail
        {
            TicketId = ticket.Id,
            Description = "Kipas berbunyi bising dan mati tiba-tiba saat compile code.",
            Asset = new Asset { AssetTag = "AST-IT-2024-009" }
        };

        await mediator.Publish(new TicketCreatedEvent(ticket, requester, manager, null, maintenance), ct);

        return Ok(new
        {
            Status = "Success",
            Message = "Email pengajuan tiket perawatan berhasil dikirim ke Papercut!",
            Recipient = manager.Email,
            TicketNumber = ticket.TicketNumber
        });
    }

    // 3. Test Email: Aksi Workflow (Approve / Reject / Revise / Completed)
    [HttpPost("status-changed")]
    public async Task<IActionResult> TestStatusChanged(
        [FromQuery] string action = "Approve",
        [FromQuery] byte ticketType = 2,
        [FromQuery] string? note = "Disetujui untuk diproses ke tahap selanjutnya.",
        CancellationToken ct = default)
    {
        var requester = new Employee
        {
            Id = Guid.CreateVersion7(),
            FullName = "Kambing Requester",
            Email = "kambing@asmara.local"
        };

        var actor = new Employee
        {
            Id = Guid.CreateVersion7(),
            FullName = "Pak Gajah (Dept Manager)",
            Email = "gajah.manager@asmara.local"
        };

        var ticket = new ASMARA.API.Entities.Ticket
        {
            Id = Guid.CreateVersion7(),
            TicketNumber = ticketType == 2 ? "TCK-PROC-2026-0001" : "TCK-MAINT-2026-0001",
            TicketType = ticketType,
            Subject = ticketType == 2 ? "Pengadaan Laptop & Perlengkapan Tim Dev" : "Laptop Mati Total"
        };

        var items = ticketType == 2 ? new List<ProcurementDetail>
        {
            new() { ItemName = "ThinkPad T14 Gen 4", Quantity = 2, Price = 18500000 },
            new() { ItemName = "Dell 24 Inch Ultrasharp", Quantity = 2, Price = 3200000 }
        } : null;

        var maintenance = ticketType == 1 ? new MaintenanceDetail
        {
            TicketId = ticket.Id,
            Description = "Masalah RAM & Kipas",
            Asset = new Asset { AssetTag = "AST-IT-2024-009" }
        } : null;

        await mediator.Publish(new TicketStatusChangedEvent(ticket, requester, actor, action, note, items, maintenance), ct);

        return Ok(new
        {
            Status = "Success",
            Action = action,
            TicketType = ticketType == 2 ? "Procurement" : "Maintenance",
            Message = $"Email notifikasi aksi '{action}' berhasil dikirim ke Papercut!"
        });
    }
}
