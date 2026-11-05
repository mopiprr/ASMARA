using FluentValidation;

namespace ASMARA.API.Features.Procurement.Commands.ResubmitProcurementTicket;

public class ResubmitProcurementTicketValidator : AbstractValidator<ResubmitProcurementTicketCommand>
{
    public ResubmitProcurementTicketValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("ID tiket pengadaan wajib diisi.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request revisi tidak boleh kosong.");

        RuleFor(x => x.Request.Subject)
            .NotEmpty().WithMessage("Subjek pengajuan wajib diisi.")
            .MaximumLength(100).WithMessage("Subjek pengajuan maksimal 100 karakter.");

        RuleFor(x => x.Request.Description)
            .MaximumLength(500).WithMessage("Deskripsi pengajuan maksimal 500 karakter.");

        RuleFor(x => x.Request.Items)
            .NotEmpty().WithMessage("Daftar barang pengadaan tidak boleh kosong.")
            .Must(items => items != null && items.Count > 0).WithMessage("Minimal harus ada 1 barang dalam pengajuan pengadaan.");

        RuleForEach(x => x.Request.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0).WithMessage("Jumlah barang harus lebih besar dari 0.");

                item.RuleFor(i => i.Price)
                    .GreaterThanOrEqualTo(0).WithMessage("Harga estimasi barang tidak boleh negatif.");

                item.When(i => !i.ItemId.HasValue, () =>
                {
                    item.RuleFor(i => i.ItemName)
                        .NotEmpty().WithMessage("Nama barang wajib diisi untuk barang baru.");

                    item.RuleFor(i => i.ItemCategoryId)
                        .GreaterThan(0).WithMessage("Kategori barang wajib dipilih untuk barang baru.");
                });
            });
    }
}
