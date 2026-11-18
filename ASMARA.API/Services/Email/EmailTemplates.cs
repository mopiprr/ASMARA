using System.Net;
using ASMARA.API.Entities;
using ASMARA.API.Models.Ticket;

namespace ASMARA.API.Services.Email;

public static class EmailTemplates
{
    private static string WrapLayout(
        string recipientName,
        string title,
        string message,
        string ticketNumber,
        string? note,
        string? extraInfo,
        string bodySection,
        string headerColor = "#2563eb",
        int maxWidthPx = 600)
    {
        var safeRecipient = WebUtility.HtmlEncode(recipientName);
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeTicketNumber = WebUtility.HtmlEncode(ticketNumber);
        var safeNote = string.IsNullOrWhiteSpace(note)
            ? null
            : WebUtility.HtmlEncode(note).Replace("\n", "<br>");

        return $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background-color: #f8fafc; padding: 20px; color: #1e293b; margin: 0;">
            <div style="max-width: {maxWidthPx}px; margin: 0 auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05);">
                <div style="background: {headerColor}; color: #ffffff; padding: 16px 20px;">
                    <h2 style="margin: 0; font-size: 18px;">ASMARA - {safeTitle}</h2>
                </div>
                <div style="padding: 20px;">
                    <p style="margin-top: 0; font-size: 15px;">Halo <strong>{safeRecipient}</strong>,</p>
                    <p style="color: #475569; line-height: 1.5; font-size: 14px;">{message}</p>
                    <div style="background: #f1f5f9; padding: 12px 16px; border-radius: 6px; margin: 15px 0; font-size: 13px; line-height: 1.6;">
                        <p style="margin: 4px 0;"><strong>Nomor Tiket:</strong> {safeTicketNumber}</p>
                        {(string.IsNullOrWhiteSpace(extraInfo) ? "" : $"<div style='margin-top: 6px;'>{extraInfo}</div>")}
                    </div>
                    {(string.IsNullOrWhiteSpace(safeNote) ? "" : $"""
                    <div style="background: #eff6ff; border-left: 4px solid {headerColor}; padding: 10px 14px; margin: 12px 0; border-radius: 4px; font-size: 13px; line-height: 1.5;">
                        <strong style="color: #1e40af;">Catatan / Alasan:</strong>
                        <p style="margin: 4px 0 0 0; color: #334155;">{safeNote}</p>
                    </div>
                    """)}
                    {bodySection}
                </div>
                <div style="background: #f8fafc; padding: 12px; text-align: center; font-size: 12px; color: #94a3b8; border-top: 1px solid #e2e8f0;">
                    Email otomatis dari Sistem Ticketing ASMARA.
                </div>
            </div>
        </body>
        </html>
        """;
    }

    // 1. Template Universal untuk Notifikasi Status (Approve, Reject, Revise, Maintenance, Completed, dll)
    public static string Notification(
        string recipientName,
        string title,
        string message,
        string ticketNumber,
        string? note = null,
        string? extraInfo = null,
        string headerColor = "#2563eb")
    {
        return WrapLayout(
            recipientName,
            title,
            message,
            ticketNumber,
            note,
            extraInfo,
            bodySection: string.Empty,
            headerColor: headerColor,
            maxWidthPx: 600);
    }

    // 2. Template Khusus Pengadaan (Dengan Tabel Rincian Barang & Grand Total)
    public static string Procurement(
        string recipientName,
        string title,
        string message,
        string ticketNumber,
        IEnumerable<ProcurementDetail> items,
        string? note = null,
        string? extraInfo = null)
    {
        var itemList = items.ToList();
        decimal total = itemList.Sum(i => i.Quantity * i.Price);

        var rows = string.Join("", itemList.Select((item, idx) => $"""
            <tr style="border-bottom: 1px solid #e2e8f0; {(idx % 2 == 1 ? "background-color: #f8fafc;" : "")}">
                <td style="padding: 8px; text-align: center; color: #64748b;">{idx + 1}</td>
                <td style="padding: 8px; font-weight: 500;">{WebUtility.HtmlEncode(item.ItemName)}</td>
                <td style="padding: 8px; text-align: center;">{item.Quantity}</td>
                <td style="padding: 8px; text-align: right;">Rp {item.Price:N0}</td>
                <td style="padding: 8px; text-align: right; font-weight: bold; color: #0f172a;">Rp {(item.Quantity * item.Price):N0}</td>
            </tr>
        """));

        var tableHtml = $"""
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
        """;

        return WrapLayout(
            recipientName,
            title,
            message,
            ticketNumber,
            note,
            extraInfo,
            bodySection: tableHtml,
            headerColor: "#2563eb",
            maxWidthPx: 650);
    }

    // 3. Template Khusus Daftar Suku Cadang Maintenance (NeedPart & Pengiriman Part Inventory -> Handler)
    public static string MaintenanceParts(
        string recipientName,
        string title,
        string message,
        string ticketNumber,
        IEnumerable<MaintenancePartItemRequestDto> parts,
        string? note = null,
        string? extraInfo = null,
        string headerColor = "#2563eb")
    {
        var partList = parts.ToList();
        var tableHtml = string.Empty;

        if (partList.Count > 0)
        {
            var rows = string.Join("", partList.Select((part, idx) => $"""
                <tr style="border-bottom: 1px solid #e2e8f0; {(idx % 2 == 1 ? "background-color: #f8fafc;" : "")}">
                    <td style="padding: 8px; text-align: center; color: #64748b;">{idx + 1}</td>
                    <td style="padding: 8px; font-weight: 500;">{WebUtility.HtmlEncode(part.ItemName)}</td>
                    <td style="padding: 8px; text-align: center; font-weight: bold; color: #0f172a;">{part.Quantity}</td>
                </tr>
            """));

            tableHtml = $"""
                <table style="width: 100%; border-collapse: collapse; font-size: 13px; margin: 15px 0;">
                    <thead>
                        <tr style="background: #f1f5f9; border-bottom: 2px solid #cbd5e1; color: #475569;">
                            <th style="padding: 8px; text-align: center; width: 40px;">No</th>
                            <th style="padding: 8px; text-align: left;">Nama Suku Cadang / Spare Part</th>
                            <th style="padding: 8px; text-align: center; width: 80px;">Qty</th>
                        </tr>
                    </thead>
                    <tbody>{rows}</tbody>
                </table>
            """;
        }

        return WrapLayout(
            recipientName,
            title,
            message,
            ticketNumber,
            note,
            extraInfo,
            bodySection: tableHtml,
            headerColor: headerColor,
            maxWidthPx: 620);
    }
}
