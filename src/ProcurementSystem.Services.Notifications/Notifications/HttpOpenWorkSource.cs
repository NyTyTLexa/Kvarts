using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Domain.Logistics;
using ProcurementSystem.Domain.Ordering;

namespace ProcurementSystem.Services.Notifications.Notifications;

/// <summary>
/// Открытые документы соседей по HTTP. Outbox у Commercial/Ordering нет, а Logistics
/// публикует только проведённую приёмку — колокольчик смотрит черновики.
/// </summary>
public sealed class HttpOpenWorkSource(IHttpClientFactory httpFactory, ILogger<HttpOpenWorkSource> log) : IOpenWorkSource
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    static readonly ApprovalStatus[] OpenApproval =
        [ApprovalStatus.НаСогласованииРП, ApprovalStatus.ВКоммерческомБлоке];

    public async Task<IReadOnlyList<OpenWorkItem>> GetOpenWorkAsync(CancellationToken ct = default)
    {
        var approvalsTask = FetchAsync<ApprovalRow>("commercial", "api/approvals", "Commercial", ct);
        var invoicesTask = FetchAsync<InvoiceRow>("commercial", "api/invoices", "Commercial", ct);
        var ordersTask = FetchAsync<OrderRow>("ordering", "api/orders", "Ordering", ct);
        var receiptsTask = FetchAsync<ReceiptRow>("logistics", "api/receipts", "Logistics", ct);
        await Task.WhenAll(approvalsTask, invoicesTask, ordersTask, receiptsTask);

        var items = new List<OpenWorkItem>();

        foreach (var a in (await approvalsTask)
                     .Where(x => OpenApproval.Contains(x.Status))
                     .OrderByDescending(x => x.CreatedAtUtc)
                     .Take(20))
        {
            items.Add(new OpenWorkItem(
                "approval", "commercial", "КП ожидает согласования",
                $"{a.Title}: требуется решение коммерческого блока.",
                "Approval", a.Id, a.CreatedAtUtc));
        }

        foreach (var i in (await invoicesTask)
                     .Where(x => x.Status != InvoiceStatus.ОтраженоВ1С && x.Status != InvoiceStatus.Отменён)
                     .OrderByDescending(x => x.CreatedAtUtc)
                     .Take(20))
        {
            items.Add(new OpenWorkItem(
                "invoice", "accounting", "Счет NOC требует внимания",
                $"{i.Number}: текущий статус {i.Status}.",
                "Invoice", i.Id, i.CreatedAtUtc));
        }

        foreach (var o in (await ordersTask)
                     .Where(x => x.Status != OrderStatus.Completed && x.Status != OrderStatus.Cancelled)
                     .OrderByDescending(x => x.CreatedAtUtc)
                     .Take(20))
        {
            items.Add(new OpenWorkItem(
                "order", "manager", "Заказ в работе",
                $"{o.Number}: {o.Title}, статус {o.Status}.",
                "Order", o.Id, o.CreatedAtUtc));
        }

        foreach (var r in (await receiptsTask)
                     .Where(x => x.Status == ReceiptStatus.Черновик)
                     .OrderByDescending(x => x.CreatedAtUtc)
                     .Take(20))
        {
            items.Add(new OpenWorkItem(
                "warehouse", "warehouse", "Приемка ожидает проведения",
                $"Заказ {r.OrderNumber}: нужно сверить складскую приемку.",
                "GoodsReceipt", r.Id, r.CreatedAtUtc));
        }

        return items;
    }

    async Task<IReadOnlyList<T>> FetchAsync<T>(string clientName, string path, string neighbor, CancellationToken ct)
    {
        try
        {
            var http = httpFactory.CreateClient(clientName);
            using var response = await http.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("{Neighbor} ответил {Status} на GET {Path}, источник пропущен",
                    neighbor, (int)response.StatusCode, path);
                return [];
            }

            var items = await response.Content.ReadFromJsonAsync<List<T>>(Json, ct);
            return items ?? [];
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "{Neighbor} недоступен, GET {Path} пропущен", neighbor, path);
            return [];
        }
    }

    sealed record ApprovalRow(Guid Id, string? Title, ApprovalStatus Status, DateTime CreatedAtUtc);
    sealed record InvoiceRow(Guid Id, string? Number, InvoiceStatus Status, DateTime CreatedAtUtc);
    sealed record OrderRow(Guid Id, string? Number, string? Title, OrderStatus Status, DateTime CreatedAtUtc);
    sealed record ReceiptRow(Guid Id, string? OrderNumber, ReceiptStatus Status, DateTime CreatedAtUtc);
}
