using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Retail;

/// <summary>
/// Магазин-витрина: либо из справочника известных хостов, либо найденный поиском
/// (страница отдала schema.org/Product с ценой).
/// </summary>
public class RetailShop : Entity
{
    public string Host { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    /// <summary>Known — из справочника; Discovered — сам нашёлся по выдаче.</summary>
    public string Kind { get; set; } = "Discovered";
    public string? SearchUrlTemplate { get; set; }
    public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSuccessUtc { get; set; }
    public string? LastError { get; set; }
    public int HitCount { get; set; }
}
