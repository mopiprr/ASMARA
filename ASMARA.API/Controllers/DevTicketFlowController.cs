using ASMARA.API.Common.Security;
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

/// KHUSUS TESTING TicketFlow. Hanya aktif di Development. Hapus sebelum rilis.
[ApiController]
[Route("api/dev/ticketflow")]
[Authorize]
public class DevTicketFlowController(
    IWebHostEnvironment env,
    ITicketRepository tickets,
    IEmployeeRepository employees,
    ITicketWorkflowLogRepository logs,
    ITicketFlowService ticketFlow,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IMediator mediator) : ControllerBase
{
    // 1. Buat tiket dummy atas nama user yang login, lalu jalankan aksi Create
    [HttpPost("{type}")]
    public async Task<IActionResult> Create(TicketTypeEnum type, [FromQuery] Guid? parentId, CancellationToken ct)
    {
        if (!env.IsDevelopment()) return NotFound();

        var now = DateTime.UtcNow;
        var ticket = new Ticket
        {
            Id = Guid.CreateVersion7(),
            EmployeeId = currentUser.EmployeeId,
            TicketNumber = $"TST-{now:yyMMddHHmmssff}",
            TicketType = (byte)type,
            Subject = $"Dummy {type} ticket",
            ParentTicketId = parentId,
            CreatedAt = now
        };
        tickets.Add(ticket);

        var result = await ticketFlow.ExecuteAsync(ticket.Id, TicketActionEnum.Create, "Dummy create", ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Opsi 2: Kirim email setelah executeAsync dan SaveChangesAsync sukses
        var requester = await employees.GetByIdAsync(ticket.EmployeeId, ct);
        Employee? manager = null;
        if (result.NextTaskEmployeeId.HasValue)
        {
            manager = await employees.GetByIdAsync(result.NextTaskEmployeeId.Value, ct);
        }
        else if (requester?.ManagerId is not null)
        {
            manager = await employees.GetByIdAsync(requester.ManagerId.Value, ct);
        }

        if (requester is not null)
        {
            await mediator.Publish(new TicketCreatedEvent(ticket, requester, manager), ct);
        }

        return Ok(result);
    }

    [HttpPost("{ticketId:guid}/{ticketAction}")]
    public async Task<IActionResult> Execute(Guid ticketId, TicketActionEnum ticketAction, [FromQuery] string? note, [FromQuery] Guid? assignTo, CancellationToken ct)
    {
        if (!env.IsDevelopment()) return NotFound();

        if (assignTo is not null)
        {
            var ticket = await tickets.GetByIdAsync(ticketId, ct);
            if (ticket is null) return NotFound();
            ticket.AssignedToEmployeeId = assignTo;   // engine memakai objek tiket yang sama dari tracker
        }

        var result = await ticketFlow.ExecuteAsync(ticketId, ticketAction, note, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Opsi 2: Kirim email aksi setelah executeAsync dan SaveChangesAsync sukses
        var updatedTicket = await tickets.GetByIdAsync(ticketId, ct);
        if (updatedTicket is not null)
        {
            var requester = await employees.GetByIdAsync(updatedTicket.EmployeeId, ct);
            var actor = await employees.GetByIdAsync(currentUser.EmployeeId, ct);
            if (requester is not null && actor is not null)
            {
                await mediator.Publish(new TicketStatusChangedEvent(
                    updatedTicket, requester, actor, ticketAction.ToString(), note), ct);
            }
        }

        return Ok(result);
    }

    // 3. Lihat posisi tiket: step sekarang, siapa yang pegang, aksi yang boleh, dan riwayat log
    [HttpGet("{ticketId:guid}")]
    public async Task<IActionResult> Inspect(Guid ticketId, CancellationToken ct)
    {
        if (!env.IsDevelopment()) return NotFound();

        var ticket = await tickets.QueryNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null) return NotFound();

        var currentStep = await tickets.GetCurrentStepAsync(ticketId, ct) ?? StepCodes.Created;
        var type = (TicketTypeEnum)ticket.TicketType;

        var history = await logs.QueryNoTracking()
            .Where(l => l.TicketId == ticketId)
            .OrderBy(l => l.CreatedAt).ThenBy(l => l.Id)
            .Select(l => new { l.FromStep, l.ToStep, l.EmployeeName, l.Note, l.CreatedAt })
            .ToListAsync(ct);

        return Ok(new
        {
            ticket.TicketNumber,
            Type = type.ToString(),
            CurrentStep = currentStep,
            ticket.CurrentTaskEmployeeId,
            IsTerminal = TransitionMap.IsTerminal(type, currentStep),
            AllowedActions = TransitionMap.AllowedActions(type, currentStep).Select(a => a.ToString()),
            History = history,
            ticket.ParentTicketId,
            ticket.AssignedToEmployeeId,
        });
    }
}