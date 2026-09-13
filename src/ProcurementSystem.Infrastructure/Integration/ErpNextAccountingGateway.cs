using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProcurementSystem.Domain.Integration;
using ProcurementSystem.Infrastructure.Observability;

namespace ProcurementSystem.Infrastructure.Integration;

public class ErpNextAccountingGateway : IAccountingGateway
{
    private readonly IConfiguration _config;
    private readonly ILogger<ErpNextAccountingGateway> _log;

    public ErpNextAccountingGateway(IConfiguration config, ILogger<ErpNextAccountingGateway> log)
    {
        _config = config;
        _log = log;
    }

    public string SystemName => "ERPNext NOC adapter";

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        using var client = CreateClient();
        try
        {
            using var res = await client.GetAsync("/api/method/frappe.auth.get_logged_user", ct);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "ERPNext availability check failed");
            return false;
        }
    }

    public async Task<AccountingPostResult> PostDocumentAsync(AccountingDocument doc, CancellationToken ct = default)
    {
        using var activity = Telemetry.Source.StartActivity("integration.erpnext.post");
        activity?.SetTag("doc.kind", doc.Kind);
        activity?.SetTag("doc.number", doc.Number);

        using var client = CreateClient();
        var doctype = ResolveDocType(doc);
        var payload = BuildDocumentPayload(doctype, doc);

        try
        {
            using var res = await client.PostAsJsonAsync($"/api/resource/{Uri.EscapeDataString(doctype)}", payload, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _log.LogWarning("ERPNext rejected {DocType} {Number}: {Status} {Body}", doctype, doc.Number, (int)res.StatusCode, body);
                return new AccountingPostResult(false, null, $"ERPNext {(int)res.StatusCode}: {body}");
            }

            var externalId = ExtractDocumentName(body) ?? doc.Number;
            _log.LogInformation("ERPNext: posted {DocType} {Number} -> {ExternalId}", doctype, doc.Number, externalId);
            return new AccountingPostResult(true, externalId, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ERPNext post failed for {Number}", doc.Number);
            return new AccountingPostResult(false, null, ex.Message);
        }
    }

    private HttpClient CreateClient()
    {
        var section = _config.GetSection("ExternalAccounting:ErpNext");
        var baseUrl = section["BaseUrl"] ?? "http://localhost:8080";
        var apiKey = section["ApiKey"];
        var apiSecret = section["ApiSecret"];

        var client = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/')) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", $"{apiKey}:{apiSecret}");
        return client;
    }

    private string ResolveDocType(AccountingDocument doc)
    {
        var kind = doc.Kind.ToLowerInvariant();
        if (kind.Contains("receipt") || kind.Contains("прием") || kind.Contains("приём"))
            return "Purchase Receipt";
        return "Purchase Invoice";
    }

    private object BuildDocumentPayload(string doctype, AccountingDocument doc)
    {
        var section = _config.GetSection("ExternalAccounting:ErpNext");
        var supplier = string.IsNullOrWhiteSpace(doc.Counterparty) || doc.Counterparty == "-"
            ? section["DefaultSupplier"] ?? "Temporary Supplier"
            : doc.Counterparty;
        var itemCode = section["DefaultItemCode"] ?? "PROCUREMENT-SERVICE";
        var warehouse = section["DefaultWarehouse"];
        var postingDate = doc.DateUtc.ToString("yyyy-MM-dd");

        var line = new Dictionary<string, object?>
        {
            ["item_code"] = itemCode,
            ["item_name"] = doc.Kind,
            ["description"] = $"Imported from Procurement System document {doc.Number}",
            ["qty"] = 1,
            ["rate"] = doc.Amount,
            ["amount"] = doc.Amount
        };
        if (!string.IsNullOrWhiteSpace(warehouse)) line["warehouse"] = warehouse;

        var payload = new Dictionary<string, object?>
        {
            ["supplier"] = supplier,
            ["posting_date"] = postingDate,
            ["set_posting_time"] = 1,
            ["remarks"] = $"Procurement System export: {doc.Kind} {doc.Number}",
            ["items"] = new[] { line }
        };

        if (doctype == "Purchase Invoice")
        {
            payload["bill_no"] = doc.Number;
            payload["bill_date"] = postingDate;
        }

        return payload;
    }

    private static string? ExtractDocumentName(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("name", out var name))
            return name.GetString();
        return null;
    }
}
