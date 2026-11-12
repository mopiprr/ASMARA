namespace ASMARA.API.Enums
{
    public enum BudgetChangeTypeEnum : byte
    {
        TicketCommitment = 1, // Ticket diapprove finance
        TicketRealization = 2, // Pembelian purchasing selesai
        TicketRelease = 3, // Ticket cancel
        AllocationIncrease = 4, // Admin menambah budget
        AllocationDecrease = 5, // Admin mengurangi budget
        TransferIn = 6, // Admin memasukkan budget dari department lain
        TransferOut = 7 // Admin mengeluarkan budget ke department lain
    }
}
