using ASMARA.API.Entities;

namespace ASMARA.API.Services.Email;

public static class EmailTemplates
{
    // 1. Template Universal untuk Notifikasi Status (Approve, Reject, Revise, Maintenance, Completed, dll)
    public static string Notification(
        string recipientName,
        string title,
        string message,
        string ticketNumber,
        string? note = null,
        string? extraInfo = null)
    {
        return $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background-color: #f8fafc; padding: 20px; color: #1e293b; margin: 0;">
            <div style="max-width: 600px; margin: 0 auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05);">
                <div style="background: #2563eb; color: #ffffff; padding: 16px 20px;">
                    <h2 style="margin: 0; font-size: 18px;">ASMARA - {title}</h2>
                </div>
                <div style="padding: 20px;">
                    <p style="margin-top: 0; font-size: 15px;">Halo <strong>{recipientName}</strong>,</p>
                    <p style="color: #475569; line-height: 1.5; font-size: 14px;">{message}</p>
                    <div style="background: #f1f5f9; padding: 12px 16px; border-radius: 6px; margin: 15px 0; font-size: 13px; line-height: 1.6;">
                        <p style="margin: 4px 0;"><strong>Nomor Tiket:</strong> {ticketNumber}</p>
                        // {(string.IsNullOrWhiteSpace(note) ? "" : $"<p style='margin: 4px 0;'><strong>Catatan:</strong> {note}</p>")}
                        {(string.IsNullOrWhiteSpace(extraInfo) ? "" : $"<div style='margin-top: 6px;'>{extraInfo}</div>")}
                    </div>
                    {(string.IsNullOrWhiteSpace(note) ? "" : $"""
                    <div style="background: #eff6ff; border-left: 4px solid #2563eb; padding: 10px 14px; margin: 12px 0; border-radius: 4px; font-size: 13px; line-height: 1.5;">
                        <strong style="color: #1e40af;">Catatan / Alasan:</strong>
                        <p style="margin: 4px 0 0 0; color: #334155;">{note}</p>
                    </div>
                    """)}
                </div>
                <div style="background: #f8fafc; padding: 12px; text-align: center; font-size: 12px; color: #94a3b8; border-top: 1px solid #e2e8f0;">
                    Email otomatis dari Sistem Ticketing ASMARA.
                </div>
            </div>
        </body>
        </html>
        """;
    }

    // 2. Template Khusus Pengadaan (Dengan Tabel Rincian Barang & Grand Total)
    public static string Procurement(
        string recipientName,
        string title,
        string message,
        string ticketNumber,
        IEnumerable<ProcurementDetail> items,
        string? note = null)
    {
        var itemList = items.ToList();
        decimal total = itemList.Sum(i => i.Quantity * i.Price);

        var rows = string.Join("", itemList.Select((item, idx) => $"""
            <tr style="border-bottom: 1px solid #e2e8f0; {(idx % 2 == 1 ? "background-color: #f8fafc;" : "")}">
                <td style="padding: 8px; text-align: center; color: #64748b;">{idx + 1}</td>
                <td style="padding: 8px; font-weight: 500;">{item.ItemName}</td>
                <td style="padding: 8px; text-align: center;">{item.Quantity}</td>
                <td style="padding: 8px; text-align: right;">Rp {item.Price:N0}</td>
                <td style="padding: 8px; text-align: right; font-weight: bold; color: #0f172a;">Rp {(item.Quantity * item.Price):N0}</td>
            </tr>
        """));

        return $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background-color: #f8fafc; padding: 20px; color: #1e293b; margin: 0;">
            <div style="max-width: 650px; margin: 0 auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05);">
                <div style="background: #2563eb; color: #ffffff; padding: 16px 20px;">
                    <h2 style="margin: 0; font-size: 18px;">ASMARA - {title}</h2>
                </div>
                <div style="padding: 20px;">
                    <p style="margin-top: 0; font-size: 15px;">Halo <strong>{recipientName}</strong>,</p>
                    <p style="color: #475569; line-height: 1.5; font-size: 14px;">{message}</p>
                    <div style="background: #f1f5f9; padding: 12px 16px; border-radius: 6px; margin: 15px 0; font-size: 13px;">
                        <p style="margin: 4px 0;"><strong>Nomor Tiket:</strong> {ticketNumber}</p>
                        // {(string.IsNullOrWhiteSpace(note) ? "" : $"<p style='margin: 4px 0;'><strong>Catatan:</strong> {note}</p>")}
                    </div>
                    {(string.IsNullOrWhiteSpace(note) ? "" : $"""
                    <div style="background: #eff6ff; border-left: 4px solid #2563eb; padding: 10px 14px; margin: 12px 0; border-radius: 4px; font-size: 13px; line-height: 1.5;">
                        <strong style="color: #1e40af;">Catatan / Alasan:</strong>
                        <p style="margin: 4px 0 0 0; color: #334155;">{note}</p>
                    </div>
                    """)}

                    <table style="width: 100%; border-collapse: collapse; font-size: 13px; margin: 15px 0;">
                        <thead>
                            <tr style="background: #f1f5f9; border-bottom: 2px solid #cbd5e1; color: #475569;">
                                <th style="padding: 8px; text-align: center; width: 35px;">No</th>
                                <th style="padding: 8px; text-align: left;">Nama Barang</th>
                                <th style="padding: 8px; text-align: center; width: 50px;">Qty</th>
                                <th style="padding: 8px; text-align: right; width: 120px;">Harga Satuan</th>
                                <th style="padding: 8px; text-align: right; width: 130px;">Subtotal</th>
                            </tr>
                        </thead>
                        <tbody>{rows}</tbody>
                        <tfoot>
                            <tr style="background: #e2e8f0; font-weight: bold;">
                                <td colspan="4" style="padding: 10px; text-align: right;">Total Estimasi:</td>
                                <td style="padding: 10px; text-align: right; color: #1d4ed8; font-size: 14px;">Rp {total:N0}</td>
                            </tr>
                        </tfoot>
                    </table>
                </div>
                <div style="background: #f8fafc; padding: 12px; text-align: center; font-size: 12px; color: #94a3b8; border-top: 1px solid #e2e8f0;">
                    Email otomatis dari Sistem Ticketing ASMARA.
                </div>
            </div>
        </body>
        </html>
        """;
    }
}
