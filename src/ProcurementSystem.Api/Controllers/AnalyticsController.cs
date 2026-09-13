using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Domain.Logistics;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Policy = "read")]
public class AnalyticsController(AppDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<AnalyticsSummaryDto>> Summary(CancellationToken ct)
    {
        var projects = await db.Projects.AsNoTracking().CountAsync(ct);
        var specifications = await db.Specifications.AsNoTracking().CountAsync(ct);
        var specificationItems = await db.SpecificationItems.AsNoTracking().CountAsync(ct);
        var matchedItems = await db.SpecificationItems.AsNoTracking().CountAsync(i => i.ProductId != null, ct);
        var approvals = await db.Set<Approval>().AsNoTracking().CountAsync(ct);
        var invoices = await db.Set<Invoice>().AsNoTracking().CountAsync(ct);
        var orders = await db.Orders.AsNoTracking().CountAsync(ct);
        var goodsReceipts = await db.Set<GoodsReceipt>().AsNoTracking().CountAsync(ct);

        var costTotal = await db.Set<Approval>().AsNoTracking().Select(a => (decimal?)a.CostPrice).SumAsync(ct) ?? 0m;
        var sellTotal = await db.Set<Approval>().AsNoTracking().Select(a => (decimal?)a.SellPrice).SumAsync(ct) ?? 0m;
        var averageMarginPercent = sellTotal > 0m
            ? Math.Round((sellTotal - costTotal) / sellTotal * 100m, 2)
            : 0m;

        // GroupBy по Year/Month в SQL Npgsql часто не транслируется — считаем в памяти.
        var vendorRows = await db.OrderLines.AsNoTracking()
            .Where(l => l.VendorName != null && l.VendorName != "")
            .Select(l => new { l.VendorName, l.LineTotal })
            .ToListAsync(ct);
        var vendors = vendorRows
            .GroupBy(l => l.VendorName!)
            .Select(g => new VendorAnalyticsDto(g.Key, g.Count(), g.Sum(x => x.LineTotal)))
            .OrderByDescending(v => v.TotalAmount)
            .Take(8)
            .ToList();

        var orderRows = await db.Orders.AsNoTracking()
            .Select(o => new { o.CreatedAtUtc, o.TotalCost })
            .ToListAsync(ct);
        var monthly = orderRows
            .GroupBy(o => (o.CreatedAtUtc.Year, o.CreatedAtUtc.Month))
            .Select(g => new MonthlyAnalyticsDto(g.Key.Year, g.Key.Month, g.Count(), g.Sum(x => x.TotalCost)))
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .Take(12)
            .ToList();

        var recentEvents = await db.AuditEntries.AsNoTracking()
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(10)
            .Select(a => new AnalyticsEventDto(a.OccurredAtUtc, a.UserName, a.Action, a.Path, a.StatusCode))
            .ToListAsync(ct);

        return new AnalyticsSummaryDto(
            projects,
            specifications,
            specificationItems,
            matchedItems,
            specificationItems - matchedItems,
            approvals,
            invoices,
            orders,
            goodsReceipts,
            costTotal,
            sellTotal,
            averageMarginPercent,
            vendors,
            monthly,
            recentEvents);
    }
}

public record AnalyticsSummaryDto(
    int Projects,
    int Specifications,
    int SpecificationItems,
    int MatchedItems,
    int UnmatchedItems,
    int Approvals,
    int Invoices,
    int Orders,
    int GoodsReceipts,
    decimal CostTotal,
    decimal SellTotal,
    decimal AverageMarginPercent,
    IReadOnlyList<VendorAnalyticsDto> TopVendors,
    IReadOnlyList<MonthlyAnalyticsDto> Monthly,
    IReadOnlyList<AnalyticsEventDto> RecentEvents);

public record VendorAnalyticsDto(string VendorName, int Lines, decimal TotalAmount);

public record MonthlyAnalyticsDto(int Year, int Month, int Orders, decimal TotalAmount);

public record AnalyticsEventDto(DateTime OccurredAtUtc, string? UserName, string Action, string Path, int StatusCode);
