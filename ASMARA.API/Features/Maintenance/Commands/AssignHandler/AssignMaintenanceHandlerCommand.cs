using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Maintenance.Commands.AssignHandler;

public record AssignMaintenanceHandlerCommand(Guid TicketId, AssignTechnicianDto Request) 
    : IRequest<TransitionResult>;

public class AssignMaintenanceHandlerCommandHandler(
    ITicketRepository tickets,
    IEmployeeRepository employees,
    ITicketFlowService ticketFlow,
    IUnitOfWork uow,
    IMediator mediator,
    ICurrentUser currentUser)
    : IRequestHandler<AssignMaintenanceHandlerCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(AssignMaintenanceHandlerCommand command, CancellationToken ct)
    {
        var type = (byte)TicketTypeEnum.Maintenance;
        var ticket = await tickets.Query()
            .Include(t => t.Employee)
            .Include(t => t.MaintenanceDetail)
                .ThenInclude(m => m!.Asset)
            .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.TicketType == type, ct);

        if (ticket is null)
            throw new NotFoundException($"Maintenance ticket {command.TicketId} was not found.");

        var technician = await employees.GetByIdAsync(command.Request.TechnicianId, ct);
        if (technician is null || technician.EmployeeStatus != 1)
            throw new BusinessRuleException($"Teknisi dengan ID {command.Request.TechnicianId} tidak ditemukan atau tidak aktif.");

        // Simpan data teknisi yang ditugaskan (assignedTo)
        ticket.AssignedToEmployeeId = technician.Id;
        ticket.AssignedTo = technician.FullName;

        // Eksekusi transisi status alur tiket (IT_SUPPORT_REVIEW -> REQUESTER_SEND_ITEM)
        var result = await ticketFlow.ExecuteAsync(command.TicketId, TicketActionEnum.AssignHandler, command.Request.InstructionNote, ct);

        await uow.SaveChangesAsync(ct);

        var actor = await employees.GetByIdAsync(currentUser.EmployeeId, ct) ?? ticket.Employee;

        // Kirim event status changed agar notifikasi email ke teknisi & pemohon terkirim
        await mediator.Publish(new TicketStatusChangedEvent(
            ticket,
            ticket.Employee,
            actor,
            TicketActionEnum.AssignHandler.ToString(),
            command.Request.InstructionNote,
            maintenance: ticket.MaintenanceDetail), ct);

        return result;
    }
}
