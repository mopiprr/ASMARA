using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Controllers;

#region Request DTOs
public class CreateProcurementTestRequest
{
    public Guid? RequesterId { get; set; } = null;
    public string? RequesterName { get; set; } = "Budi Hartono";
    public string? RequesterEmail { get; set; } = "budi.hartono@asmara.local";
    public string Subject { get; set; } = "Pengadaan Laptop & Perlengkapan Tim Dev";
    public string? Description { get; set; } = "Pengadaan perangkat kerja baru untuk batch engineer baru.";
    public List<TestProcurementItemRequest>? Items { get; set; } = new()
    {
        new() { ItemName = "ThinkPad T14 Gen 4 Core i7 32GB", Quantity = 2, Price = 18500000 },
        new() { ItemName = "Dell 24 Inch Ultrasharp U2422H", Quantity = 2, Price = 3200000 },
        new() { ItemName = "Logitech MX Master 3S Wireless Mouse", Quantity = 2, Price = 1450000 }
    };
}

public class TestProcurementItemRequest
{
    public string ItemName { get; set; } = "ThinkPad T14 Gen 4 Core i7 32GB";
    public int Quantity { get; set; } = 2;
    public decimal Price { get; set; } = 18500000;
}

public class CreateMaintenanceTestRequest
{
    public Guid? RequesterId { get; set; } = null;
    public string? RequesterName { get; set; } = "Budi Hartono";
    public string? RequesterEmail { get; set; } = "budi.hartono@asmara.local";
    public string Subject { get; set; } = "Laptop Mati Total & Blue Screen Berulang";
    public string? Description { get; set; } = "Perangkat mendadak restart dan muncul layar biru IRQL_NOT_LESS_OR_EQUAL.";
    public string? AssetTag { get; set; } = "DEV-ASM-0001";
    public string? ProblemDescription { get; set; } = "Kipas berbunyi bising dan mati tiba-tiba saat compile code.";
}

public class StatusChangedTestRequest
{
    public Guid? TicketId { get; set; }
    public string? TicketNumber { get; set; } = "PRC-261104-1001";
    public byte TicketType { get; set; } = 2; // 1 = Maintenance, 2 = Procurement
    public string Subject { get; set; } = "Pengadaan Laptop & Perlengkapan Tim Dev";
    public string Action { get; set; } = "Approve"; // Approve, Revise, Reject, Completed
    public string? Note { get; set; } = "Disetujui untuk diproses ke tahap selanjutnya.";
    public string? RequesterName { get; set; } = "Budi Hartono";
    public string? RequesterEmail { get; set; } = "budi.hartono@asmara.local";
    public string? ActorName { get; set; } = "Pak Andi Wijaya (Dept Manager)";
}
#endregion

[ApiController]
[AllowAnonymous] // SEMENTARA untuk keperluan testing
[Route("api/test-notification")]
public class TestNotificationController(
    IMediator mediator,
    ITicketFlowService ticketFlowService,
    IUnitOfWork uow,
    AsmaraDbContext db,
    ICurrentUser currentUser
) : ControllerBase
{
    // 1. Test Email: Pengajuan Tiket Pengadaan (Multi-Item)
    [HttpPost("procurement-created")]
    public async Task<IActionResult> TestProcurementCreated(
        [FromBody] CreateProcurementTestRequest request,
        CancellationToken ct)
    {
        // 1. Ambil Requester dari User Login (jika login) atau dari Database
        Employee? requester = null;
        if (currentUser.IsAuthenticated)
        {
            requester = await db.Employees
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.Id == currentUser.EmployeeId, ct);
        }

        if (requester == null && request.RequesterId.HasValue && request.RequesterId != Guid.Empty)
        {
            requester = await db.Employees
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.Id == request.RequesterId.Value, ct);
        }

        if (requester == null && !string.IsNullOrWhiteSpace(request.RequesterEmail))
        {
            requester = await db.Employees
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.Email == request.RequesterEmail, ct);
        }

        if (requester == null)
        {
            requester = await db.Employees
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.ManagerId != null, ct)
                ?? await db.Employees.FirstOrDefaultAsync(ct);
        }

        if (requester == null)
        {
            requester = new Employee
            {
                Id = Guid.CreateVersion7(),
                FullName = request.RequesterName ?? "Budi Hartono",
                Email = request.RequesterEmail ?? "budi.hartono@asmara.local"
            };
        }

        // 2. Ambil Direct Manager
        var manager = requester.Manager;
        if (manager == null && requester.ManagerId.HasValue)
        {
            manager = await db.Employees.FirstOrDefaultAsync(e => e.Id == requester.ManagerId.Value, ct);
        }

        if (manager == null)
        {
            // Ambil karyawan lain sebagai manager atau buat fallback
            manager = await db.Employees.FirstOrDefaultAsync(e => e.Id != requester.Id, ct)
                ?? new Employee
                {
                    Id = Guid.CreateVersion7(),
                    FullName = "Andi Wijaya (Manager)",
                    Email = "andi.wijaya@asmara.local"
                };
        }

        // 3. Buat Entitas Tiket & Simpan ke DB agar Guid bisa dipakai di Test 4
        var ticket = new ASMARA.API.Entities.Ticket
        {
            Id = Guid.CreateVersion7(),
            TicketNumber = $"PRC-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(1000, 9999)}",
            TicketType = (byte)TicketTypeEnum.Procurement,
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? "Pengadaan Perangkat Kerja" : request.Subject,
            Description = request.Description,
            EmployeeId = requester.Id,
            CurrentTaskEmployeeId = manager.Id,
            CreatedAt = DateTime.UtcNow
        };

        // 4. Kategori Item
        var defaultCategory = await db.ItemCategories.FirstOrDefaultAsync(ct);
        int categoryId = defaultCategory?.Id ?? 1;

        var items = new List<ProcurementDetail>();
        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var item in request.Items)
            {
                items.Add(new ProcurementDetail
                {
                    TicketId = ticket.Id,
                    ItemCategoryId = categoryId,
                    ItemName = item.ItemName,
                    Quantity = item.Quantity > 0 ? item.Quantity : 1,
                    Price = item.Price > 0 ? item.Price : 100000,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        else
        {
            items.Add(new ProcurementDetail
            {
                TicketId = ticket.Id,
                ItemCategoryId = categoryId,
                ItemName = "Standard Laptop Dev",
                Quantity = 1,
                Price = 15000000,
                CreatedAt = DateTime.UtcNow
            });
        }

        ticket.ProcurementDetails = items;

        // Simpan tiket ke DB jika requester ada di DB
        var requesterExistsInDb = await db.Employees.AnyAsync(e => e.Id == requester.Id, ct);
        if (requesterExistsInDb)
        {
            db.Tickets.Add(ticket);
            db.TicketWorkflowLogs.Add(new TicketWorkflowLog
            {
                TicketId = ticket.Id,
                FromStep = StepCodes.Created,
                ToStep = StepCodes.DepartmentApproval,
                EmployeeId = requester.Id,
                EmployeeName = requester.FullName,
                Note = "Pengajuan tiket baru oleh user",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        // 5. Kirim Notifikasi Email via MediatR
        await mediator.Publish(new TicketCreatedEvent(ticket, requester, manager, items), ct);

        return Ok(new
        {
            Status = "Success",
            Message = "Email pengajuan tiket pengadaan berhasil dikirim ke Papercut!",
            TicketId = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            Subject = ticket.Subject,
            Description = ticket.Description,
            CurrentStep = StepCodes.DepartmentApproval,
            IsSavedToDatabase = requesterExistsInDb,
            Requester = new
            {
                requester.Id,
                requester.FullName,
                requester.Email
            },
            RecipientManager = new
            {
                manager.Id,
                manager.FullName,
                manager.Email
            },
            ItemsCount = items.Count,
            TotalEstimatedCost = items.Sum(i => i.Quantity * i.Price),
            Items = items.Select(i => new
            {
                i.ItemName,
                i.Quantity,
                i.Price,
                Subtotal = i.Quantity * i.Price
            }),
            NextStepHint = requesterExistsInDb
                ? $"Salin TicketId '{ticket.Id}' untuk diuji pada Test 4 [POST /api/test-notification/execution-flow(procurement)]."
                : "Tiket diuji secara in-memory (karena RequesterId tidak ada di DB)."
        });
    }

    // 2. Test Email: Pengajuan Tiket Perawatan (Maintenance)
    [HttpPost("maintenance-created")]
    public async Task<IActionResult> TestMaintenanceCreated(
        [FromBody] CreateMaintenanceTestRequest request,
        CancellationToken ct)
    {
        // 1. Ambil Requester dari User Login (jika login) atau dari Database
        Employee? requester = null;
        if (currentUser.IsAuthenticated)
        {
            requester = await db.Employees
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.Id == currentUser.EmployeeId, ct);
        }

        if (requester == null && request.RequesterId.HasValue && request.RequesterId != Guid.Empty)
        {
            requester = await db.Employees
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.Id == request.RequesterId.Value, ct);
        }

        if (requester == null && !string.IsNullOrWhiteSpace(request.RequesterEmail))
        {
            requester = await db.Employees
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.Email == request.RequesterEmail, ct);
        }

        if (requester == null)
        {
            requester = await db.Employees
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.ManagerId != null, ct)
                ?? await db.Employees.FirstOrDefaultAsync(ct);
        }

        if (requester == null)
        {
            requester = new Employee
            {
                Id = Guid.CreateVersion7(),
                FullName = request.RequesterName ?? "Budi Hartono",
                Email = request.RequesterEmail ?? "budi.hartono@asmara.local"
            };
        }

        // 2. Ambil Direct Manager
        var manager = requester.Manager;
        if (manager == null && requester.ManagerId.HasValue)
        {
            manager = await db.Employees.FirstOrDefaultAsync(e => e.Id == requester.ManagerId.Value, ct);
        }

        if (manager == null)
        {
            manager = await db.Employees.FirstOrDefaultAsync(e => e.Id != requester.Id, ct)
                ?? new Employee
                {
                    Id = Guid.CreateVersion7(),
                    FullName = "Andi Wijaya (Manager)",
                    Email = "andi.wijaya@asmara.local"
                };
        }

        // 3. Ambil Asset dari DB
        Asset? asset = null;
        if (!string.IsNullOrWhiteSpace(request.AssetTag))
        {
            asset = await db.Assets.FirstOrDefaultAsync(a => a.AssetTag == request.AssetTag, ct);
        }
        asset ??= await db.Assets.FirstOrDefaultAsync(ct)
            ?? new Asset { Id = 1, AssetTag = request.AssetTag ?? "DEV-ASM-0001" };

        // 4. Buat Tiket
        var ticket = new ASMARA.API.Entities.Ticket
        {
            Id = Guid.CreateVersion7(),
            TicketNumber = $"MNT-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(1000, 9999)}",
            TicketType = (byte)TicketTypeEnum.Maintenance,
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? "Perawatan Perangkat" : request.Subject,
            Description = request.Description,
            EmployeeId = requester.Id,
            CurrentTaskEmployeeId = manager.Id,
            CreatedAt = DateTime.UtcNow
        };

        var maintenance = new MaintenanceDetail
        {
            TicketId = ticket.Id,
            AssetId = asset.Id,
            Description = string.IsNullOrWhiteSpace(request.ProblemDescription)
                ? (request.Description ?? "Kendala operasional perangkat")
                : request.ProblemDescription,
            CreatedAt = DateTime.UtcNow,
            Asset = asset
        };

        ticket.MaintenanceDetail = maintenance;

        // Simpan ke DB jika data valid
        var requesterExistsInDb = await db.Employees.AnyAsync(e => e.Id == requester.Id, ct);
        var assetExistsInDb = await db.Assets.AnyAsync(a => a.Id == asset.Id, ct);

        if (requesterExistsInDb && assetExistsInDb)
        {
            db.Tickets.Add(ticket);
            db.MaintenanceDetails.Add(maintenance);
            db.TicketWorkflowLogs.Add(new TicketWorkflowLog
            {
                TicketId = ticket.Id,
                FromStep = StepCodes.Created,
                ToStep = StepCodes.DepartmentApproval,
                EmployeeId = requester.Id,
                EmployeeName = requester.FullName,
                Note = "Pengajuan tiket perawatan oleh user",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        // 5. Kirim Notifikasi Email
        await mediator.Publish(new TicketCreatedEvent(ticket, requester, manager, null, maintenance), ct);

        return Ok(new
        {
            Status = "Success",
            Message = "Email pengajuan tiket perawatan berhasil dikirim ke Papercut!",
            TicketId = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            Subject = ticket.Subject,
            Description = ticket.Description,
            CurrentStep = StepCodes.DepartmentApproval,
            IsSavedToDatabase = requesterExistsInDb && assetExistsInDb,
            Requester = new
            {
                requester.Id,
                requester.FullName,
                requester.Email
            },
            RecipientManager = new
            {
                manager.Id,
                manager.FullName,
                manager.Email
            },
            Maintenance = new
            {
                AssetTag = asset.AssetTag,
                ProblemDescription = maintenance.Description
            },
            NextStepHint = (requesterExistsInDb && assetExistsInDb)
                ? $"Salin TicketId '{ticket.Id}' untuk diuji alur approval-nya."
                : "Tiket diuji secara in-memory (karena data ID belum tersedia di DB)."
        });
    }

    // 3. Test Email: Aksi Workflow (Approve / Reject / Revise / Completed)
    [HttpPost("status-changed")]
    public async Task<IActionResult> TestStatusChanged(
        [FromBody] StatusChangedTestRequest request,
        CancellationToken ct = default)
    {
        var ticketId = request.TicketId ?? Guid.CreateVersion7();
        var requester = new Employee
        {
            Id = Guid.CreateVersion7(),
            FullName = request.RequesterName ?? "Budi Hartono",
            Email = request.RequesterEmail ?? "budi.hartono@asmara.local"
        };

        var actor = new Employee
        {
            Id = Guid.CreateVersion7(),
            FullName = request.ActorName ?? "Andi Wijaya (Dept Manager)",
            Email = "andi.wijaya@asmara.local"
        };

        var ticket = new ASMARA.API.Entities.Ticket
        {
            Id = ticketId,
            TicketNumber = string.IsNullOrWhiteSpace(request.TicketNumber) ? "PRC-261104-1001" : request.TicketNumber,
            TicketType = request.TicketType,
            Subject = request.Subject
        };

        var items = request.TicketType == 2 ? new List<ProcurementDetail>
        {
            new() { ItemName = "ThinkPad T14 Gen 4", Quantity = 2, Price = 18500000 },
            new() { ItemName = "Dell 24 Inch Ultrasharp", Quantity = 2, Price = 3200000 }
        } : null;

        var maintenance = request.TicketType == 1 ? new MaintenanceDetail
        {
            TicketId = ticket.Id,
            Description = "Masalah RAM & Kipas",
            Asset = new Asset { AssetTag = "DEV-ASM-0001" }
        } : null;

        await mediator.Publish(new TicketStatusChangedEvent(
            ticket,
            requester,
            actor,
            request.Action,
            request.Note,
            items,
            maintenance), ct);

        return Ok(new
        {
            Status = "Success",
            Action = request.Action,
            TicketId = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            TicketType = request.TicketType == 2 ? "Procurement" : "Maintenance",
            RecipientRequester = requester.Email,
            Message = $"Email notifikasi aksi '{request.Action}' berhasil dikirim ke Papercut!"
        });
    }

    // 4. Test eksekusi alur asli via TicketFlowService
    [HttpPost("execution-flow(procurement)")]
    public async Task<IActionResult> TestExecutionFlow(
        [FromQuery] Guid ticketId,
        [FromQuery] TicketActionEnum action,
        [FromQuery] string? note,
        CancellationToken ct)
    {
        try
        {
            var result = await ticketFlowService.ExecuteAsync(ticketId, action, note, ct);
            await uow.SaveChangesAsync(ct);

            return Ok(new
            {
                Status = "Success",
                Message = $"Aksi {action} berhasil dieksekusi",
                Transition = result,
                CheckPapercut = "Silahkan periksa aplikasi Papercut untuk email notifikasi hasil aksi ini."
            });
        }
        catch (UnauthorizedException ex)
        {
            return Unauthorized(new
            {
                Status = "Unauthorized",
                Message = ex.Message,
                Hint = "TicketFlowService memerlukan JWT Token user yang sedang login. Silakan login terlebih dahulu via POST /api/auth/login menggunakan akun Manager/Approver, lalu klik Authorize di Swagger."
            });
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(403, new
            {
                Status = "Forbidden",
                Message = ex.Message,
                Hint = "User yang sedang login tidak berhak mengeksekusi aksi pada tiket ini. Pastikan akun yang digunakan adalah Manager yang ditugaskan pada tiket tersebut."
            });
        }
    }
}

