using System.Diagnostics;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Import;
using ProcurementSystem.Domain.Pricing;
using ProcurementSystem.Domain.Quoting;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Search;

namespace ProcurementSystem.Infrastructure.Seeding;

public record PriceListCorpusResult(
    int Vendors, int Products, int Offers, int Uploads,
    Guid SpecificationId, int SpecItems, int SpecUnmatched,
    int ElapsedMs);

public interface IPriceListCorpus
{
    Task<PriceListCorpusResult> SeedAsync(int lists = 120, int catalogSize = 0, CancellationToken ct = default);
}

/// <summary>
/// Корпус из 100+ прайсов для проверки ML на большом каталоге: пересекающееся ядро,
/// тысячи позиций-дистракторов, разные цены/сроки/остатки и кривая заявка.
/// Не парсим чужие сайты — публичных Excel на сотню поставщиков легально пачкой нет.
/// </summary>
public class PriceListCorpus(AppDbContext db, IProductIndexer? indexer = null) : IPriceListCorpus
{
    public const string VendorPrefix = "ПЛ-";
    public const string SpecTitle = "ML-стенд: кривая заявка";
    public const int DefaultCatalogSize = 4000;
    const int FileSampleRows = 100;

    public async Task<PriceListCorpusResult> SeedAsync(int lists = 120, int catalogSize = 0, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        lists = Math.Clamp(lists, 20, 160);
        if (catalogSize <= 0)
            catalogSize = lists >= 80 ? DefaultCatalogSize : Math.Max(220, lists * 12);
        catalogSize = Math.Clamp(catalogSize, 80, 8000);

        var catalog = BuildCatalog(catalogSize);
        var vendors = await EnsureVendorsAsync(lists, ct);
        var products = await EnsureProductsAsync(catalog, ct);
        var offers = await EnsureOffersAsync(vendors, products, catalog, ct);
        var uploads = await EnsureUploadsAsync(vendors, products, catalog, ct);
        var spec = await EnsureDirtySpecAsync(catalog, ct);
        await TryReindexAsync(ct);

        var unmatched = spec.Items.Count(i => i.ProductId == null);
        return new PriceListCorpusResult(
            vendors.Count, products.Count, offers, uploads,
            spec.Id, spec.Items.Count, unmatched, (int)sw.ElapsedMilliseconds);
    }

    async Task TryReindexAsync(CancellationToken ct)
    {
        if (indexer is null) return;
        try { await indexer.ReindexAllAsync(ct); }
        catch { /* Meilisearch может быть выключен — matcher учится с Postgres */ }
    }

    async Task<List<Vendor>> EnsureVendorsAsync(int lists, CancellationToken ct)
    {
        var existing = await db.Vendors.Where(v => v.Name.StartsWith(VendorPrefix)).OrderBy(v => v.Name).ToListAsync(ct);
        if (existing.Count >= lists) return existing.Take(lists).ToList();

        for (var i = existing.Count; i < lists; i++)
        {
            db.Vendors.Add(new Vendor
            {
                Name = $"{VendorPrefix}{i + 1:000} {VendorName(i)}",
                Inn = $"{7400000000L + i}",
                DefaultLeadTimeDays = 3 + i % 28,
            });
        }
        await db.SaveChangesAsync(ct);
        return await db.Vendors.Where(v => v.Name.StartsWith(VendorPrefix)).OrderBy(v => v.Name).Take(lists).ToListAsync(ct);
    }

    async Task<List<Product>> EnsureProductsAsync(IReadOnlyList<Canon> catalog, CancellationToken ct)
    {
        var skus = catalog.Select(c => c.Sku).ToList();
        var bySku = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < skus.Count; i += 400)
        {
            var chunk = skus.Skip(i).Take(400).ToList();
            foreach (var p in await db.Products.Where(p => chunk.Contains(p.Sku)).ToListAsync(ct))
                bySku[p.Sku] = p;
        }
        var added = 0;
        foreach (var c in catalog)
        {
            if (bySku.ContainsKey(c.Sku)) continue;
            var p = new Product { Sku = c.Sku, Name = c.Name, Manufacturer = c.Manufacturer, Category = c.Category };
            db.Products.Add(p);
            bySku[c.Sku] = p;
            added++;
            if (added % 1000 == 0) await db.SaveChangesAsync(ct);
        }
        if (added > 0) await db.SaveChangesAsync(ct);
        return catalog.Select(c => bySku[c.Sku]).ToList();
    }

    async Task<int> EnsureOffersAsync(List<Vendor> vendors, List<Product> products, IReadOnlyList<Canon> catalog, CancellationToken ct)
    {
        var vendorIds = vendors.Select(v => v.Id).ToList();
        var existing = (await db.PriceListItems.Where(o => vendorIds.Contains(o.VendorId)).Select(o => new { o.VendorId, o.ProductId }).ToListAsync(ct))
            .Select(x => (x.VendorId, x.ProductId)).ToHashSet();
        var rnd = new Random(7);
        var created = 0;
        var history = new List<PriceHistoryEntry>();

        for (var v = 0; v < vendors.Count; v++)
        {
            var vendor = vendors[v];
            var pick = PickForVendor(v, products.Count, rnd);
            foreach (var ix in pick)
            {
                var product = products[ix];
                if (!existing.Add((vendor.Id, product.Id))) continue;
                var canon = catalog[ix];
                var price = Math.Round(canon.Price * (0.86m + (decimal)(rnd.NextDouble() * 0.32)), 2);
                var lead = Math.Clamp(vendor.DefaultLeadTimeDays + rnd.Next(-4, 12), 1, 60);
                var stock = rnd.Next(0, 12) == 0 ? 0 : rnd.Next(2, 180);
                db.PriceListItems.Add(new PriceListItem
                {
                    VendorId = vendor.Id,
                    ProductId = product.Id,
                    Price = price,
                    Currency = "RUB",
                    LeadTimeDays = lead,
                    StockQuantity = stock,
                });
                history.Add(new PriceHistoryEntry
                {
                    ProductId = product.Id,
                    VendorId = vendor.Id,
                    Price = price,
                    LeadTimeDays = lead,
                    RecordedAtUtc = DateTime.UtcNow.AddDays(-rnd.Next(0, 40)),
                });
                created++;
            }
            if (created > 0 && created % 800 == 0)
            {
                db.PriceHistory.AddRange(history);
                history.Clear();
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }
        }
        if (history.Count > 0) db.PriceHistory.AddRange(history);
        if (created > 0) await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return await db.PriceListItems.CountAsync(o => vendorIds.Contains(o.VendorId), ct);
    }

    async Task<int> EnsureUploadsAsync(List<Vendor> vendors, List<Product> products, IReadOnlyList<Canon> catalog, CancellationToken ct)
    {
        var vendorIds = vendors.Select(v => v.Id).ToList();
        var have = (await db.PriceImportUploads.Where(u => vendorIds.Contains(u.VendorId)).Select(u => u.VendorId).ToListAsync(ct))
            .ToHashSet();
        var rnd = new Random(7);
        var added = 0;
        foreach (var (vendor, v) in vendors.Select((x, i) => (x, i)))
        {
            if (have.Contains(vendor.Id)) continue;
            var pick = PickForVendor(v, products.Count, rnd);
            var sample = pick.Take(FileSampleRows).ToList();
            var bytes = BuildXlsx(vendor.Name, sample.Select(i => (catalog[i], products[i])));
            db.PriceImportUploads.Add(new PriceImportUpload
            {
                VendorId = vendor.Id,
                VendorName = vendor.Name,
                FileName = $"{vendor.Name.Replace(' ', '_')}.xlsx",
                FileContent = bytes,
                UploadedBy = "corpus",
                RowsProcessed = sample.Count,
                ProductsCreated = 0,
                OffersUpserted = pick.Count,
            });
            added++;
            if (added % 20 == 0) await db.SaveChangesAsync(ct);
        }
        if (added > 0) await db.SaveChangesAsync(ct);
        return await db.PriceImportUploads.CountAsync(u => vendorIds.Contains(u.VendorId), ct);
    }

    async Task<Specification> EnsureDirtySpecAsync(IReadOnlyList<Canon> catalog, CancellationToken ct)
    {
        var want = catalog.Count >= 800 ? 180 : 64;
        var spec = await db.Specifications.Include(s => s.Items).FirstOrDefaultAsync(s => s.Title == SpecTitle, ct);
        if (spec is { Items.Count: >= 150 } || (spec is { Items.Count: > 0 } && want <= 64 && spec.Items.Count >= 40))
            return spec;

        if (spec is null)
        {
            spec = new Specification { Title = SpecTitle, Customer = "ЧелГУ, курсовая, стенд ML" };
            db.Specifications.Add(spec);
        }
        else if (spec.Items.Count > 0)
        {
            db.SpecificationItems.RemoveRange(spec.Items);
            spec.Items.Clear();
        }

        var rnd = new Random(3);
        var used = new HashSet<int>();
        void Add(string? sku, string name, int qty) =>
            spec.Items.Add(new SpecificationItem { RawSku = sku, RawName = name, Quantity = qty });

        var exactN = catalog.Count >= 800 ? 50 : 18;
        var messyN = catalog.Count >= 800 ? 110 : 40;
        for (var i = 0; i < exactN; i++)
        {
            var ix = i * 3 % catalog.Count;
            used.Add(ix);
            Add(catalog[ix].Sku, catalog[ix].Name, 1 + i % 6);
        }
        for (var i = 0; i < messyN; i++)
        {
            int ix;
            do { ix = rnd.Next(catalog.Count); } while (!used.Add(ix) && used.Count < catalog.Count);
            var c = catalog[ix];
            Add(i % 3 == 0 ? null : MessSku(c.Sku, i), MessName(c.Name, i), 1 + i % 8);
        }
        Add(null, "свитч циско каталист девять триста сорок восемь портов poe", 4);
        Add("C9300-XX", "коммутатор cisco catalyst 9300", 2);
        Add(null, "сервер proliant dl380 два ксеона 128 гигов", 1);
        Add(null, "ибп апк смарт апс 5000 ва стоечный", 3);
        Add("NO-SUCH-SKU", "кабель витая пара cat6a экран неизвестного бренда 305м", 10);
        Add(null, "что-то для щита модульное 16 ампер однополюсное", 24);
        Add(null, "ноут latitude i5 шестнадцать гигов", 2);
        Add("P82000-B21", "hpe dl380 gen11", 1);
        Add(null, "монитор dell 27 4k ultrasharp", 3);
        Add("VA47-16A-1P", "автомат iek 16а однополюсный", 12);

        await db.SaveChangesAsync(ct);
        return await db.Specifications.Include(s => s.Items).FirstAsync(s => s.Id == spec.Id, ct);
    }

    static List<int> PickForVendor(int vendorIndex, int productCount, Random rnd)
    {
        var set = new HashSet<int>();
        var core = Math.Min(productCount >= 800 ? 80 : 56, productCount);
        for (var i = 0; i < core; i++) set.Add(i);
        var extra = productCount >= 800 ? 200 + vendorIndex % 120 : 36 + vendorIndex % 40;
        var want = Math.Min(productCount, core + extra);
        while (set.Count < want) set.Add(rnd.Next(productCount));
        return set.OrderBy(x => x).ToList();
    }

    static byte[] BuildXlsx(string vendor, IEnumerable<(Canon Canon, Product Product)> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Прайс");
        string[] head = ["Артикул", "Наименование", "Производитель", "Категория", "Цена", "СрокДней", "Остаток"];
        for (var c = 0; c < head.Length; c++) ws.Cell(1, c + 1).Value = head[c];
        var r = 2;
        var n = 0;
        foreach (var (canon, _) in rows)
        {
            var jitter = 1 + (n % 17) * 0.01m;
            ws.Cell(r, 1).Value = canon.Sku;
            ws.Cell(r, 2).Value = canon.Name;
            ws.Cell(r, 3).Value = canon.Manufacturer;
            ws.Cell(r, 4).Value = canon.Category;
            ws.Cell(r, 5).Value = Math.Round(canon.Price * jitter, 2);
            ws.Cell(r, 6).Value = 5 + n % 21;
            ws.Cell(r, 7).Value = n % 11 == 0 ? 0 : 8 + n % 90;
            r++; n++;
        }
        ws.Cell(1, 9).Value = vendor;
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    static string MessName(string name, int i) => (i % 8) switch
    {
        0 => name,
        1 => SwapChars(name),
        2 => name.ToLowerInvariant(),
        3 => name.Replace("Коммутатор", "свитч", StringComparison.OrdinalIgnoreCase)
                 .Replace("Cisco", "циско", StringComparison.OrdinalIgnoreCase)
                 .Replace("Сервер", "server", StringComparison.OrdinalIgnoreCase),
        4 => name + "  новый оригинал",
        5 => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(4)),
        6 => name.Replace("HPE", "HP", StringComparison.OrdinalIgnoreCase).Replace("  ", " "),
        _ => "аналог " + name.ToLowerInvariant(),
    };

    static string MessSku(string sku, int i) => i % 4 == 0 ? sku : SwapChars(sku);

    static string SwapChars(string s)
    {
        if (s.Length < 4) return s;
        var a = s.ToCharArray();
        var i = Math.Min(3, a.Length - 2);
        (a[i], a[i + 1]) = (a[i + 1], a[i]);
        return new string(a);
    }

    static string VendorName(int i)
    {
        string[] stems = ["Электрокомплект", "СнабСервис", "КабельТорг", "АйТиДистриб", "СвязьСнаб", "РегионЭлектро", "УралКомплект", "СибСнаб", "ВолгаКабель", "ТехноПарк"];
        string[] cities = ["Челябинск", "Екатеринбург", "Уфа", "Пермь", "Тюмень", "Омск", "Казань", "Самара", "Новосибирск", "Красноярск", "Воронеж", "Ростов"];
        return $"{stems[i % stems.Length]} {cities[i % cities.Length]}";
    }

    sealed record Canon(string Sku, string Name, string Manufacturer, string Category, decimal Price);
    sealed record Line(string Manufacturer, string Model, string Category, decimal Price);

    static List<Canon> BuildCatalog(int target)
    {
        var list = new List<Canon>(target);
        void Add(string sku, string name, string mfr, string cat, decimal price) =>
            list.Add(new Canon(sku, name, mfr, cat, price));

        string[] cisco = ["C9300-48P", "C9300-24P", "C9200L-24P", "C9200L-48P", "ISR4451-X", "ISR4331", "FPR1140", "FPR2120", "C1000-16P", "C2960X-48"];
        foreach (var (sku, i) in cisco.Select((s, i) => (s, i)))
            Add(sku, sku == "C9300-48P"
                ? "Коммутатор Cisco Catalyst 9300 C9300-48P, 48 портов PoE+"
                : $"Коммутатор Cisco Catalyst {sku}",
                "Cisco", "Сетевое оборудование / Коммутаторы", 80_000 + i * 22_000);

        string[] hpe = ["DL380-G11", "DL360-G11", "ML350-G11", "DL325-G11", "MSA2062", "ALLETRA6010"];
        foreach (var (sku, i) in hpe.Select((s, i) => (s, i)))
            Add($"P8{2000 + i}-B21", $"Сервер HPE ProLiant {sku}", "HPE", "Серверы / Стоечные", 220_000 + i * 70_000);

        string[] dell = ["R760", "R660", "R750", "T550", "ME5024", "UNITY480"];
        foreach (var (sku, i) in dell.Select((s, i) => (s, i)))
            Add($"210-DE{sku}", $"Сервер Dell PowerEdge {sku}", "Dell", "Серверы / Стоечные", 190_000 + i * 65_000);

        string[] lenovo = ["SR650V3", "SR630V3", "ST650V2", "SE350"];
        foreach (var (sku, i) in lenovo.Select((s, i) => (s, i)))
            Add($"7D8{i}CTO1WW", $"Сервер Lenovo ThinkSystem {sku}", "Lenovo", "Серверы / Стоечные", 210_000 + i * 55_000);

        Add("LAT5440-i5", "Ноутбук Dell Latitude 5440 i5 16/512", "Dell", "ПК и ноутбуки", 92_000);
        Add("T14G4-i7", "Ноутбук Lenovo ThinkPad T14 Gen4 i7 32/1TB", "Lenovo", "ПК и ноутбуки", 128_000);
        Add("X1C-G11", "Ноутбук Lenovo ThinkPad X1 Carbon Gen11", "Lenovo", "ПК и ноутбуки", 186_000);
        Add("MATE-D16", "Ноутбук Huawei MateBook D16", "Huawei", "ПК и ноутбуки", 74_000);
        Add("U2723QE", "Монитор Dell UltraSharp U2723QE 27 4K", "Dell", "Периферия / Мониторы", 64_000);
        Add("P2422H", "Монитор Dell P2422H 24", "Dell", "Периферия / Мониторы", 18_400);
        Add("M428FDW", "МФУ HP LaserJet Pro M428fdw", "HP", "Периферия / Печать", 41_200);
        Add("SRT5KRMXLI", "ИБП APC Smart-UPS SRT 5000 ВА", "APC", "ИБП / Онлайн", 218_000);
        Add("9PX6KIRT", "ИБП Eaton 9PX 6000 ВА", "Eaton", "ИБП / Онлайн", 242_000);
        Add("S5732-H48", "Коммутатор Huawei CloudEngine S5732-H48", "Huawei", "Сетевое оборудование / Коммутаторы", 96_000);
        Add("AR6300", "Маршрутизатор Huawei NetEngine AR6300", "Huawei", "Сетевое оборудование / Маршрутизаторы", 154_000);

        for (var a = 6; a <= 63; a += 3)
        {
            Add($"VA47-{a}A-1P", $"Выключатель автоматический ВА47-29 {a}А 1P 4.5кА", "IEK", "Модульное оборудование / Автоматы", 180 + a * 12);
            Add($"VA47-{a}A-3P", $"Выключатель автоматический ВА47-29 {a}А 3P 4.5кА", "IEK", "Модульное оборудование / Автоматы", 520 + a * 18);
            Add($"EKF-{a}C", $"Автоматический выключатель EKF ВА47-63 {a}А C", "EKF", "Модульное оборудование / Автоматы", 210 + a * 11);
        }
        string[] cable = ["ВВГнг-LS 3х1.5", "ВВГнг-LS 3х2.5", "ВВГнг-LS 5х2.5", "ВВГнг-LS 5х4", "КГ 3х2.5", "FTP cat6 305м"];
        foreach (var (c, i) in cable.Select((s, i) => (s, i)))
            Add($"CAB-{i + 1:00}", $"Кабель {c}", "ДКС", "Кабель / Силовой", 28 + i * 17);

        Add("SSD-192-SAS", "Накопитель SSD Dell 1.92 ТБ SAS", "Dell", "Комплектующие / Накопители", 48_000);
        Add("RAM-32-D5", "Модуль памяти Lenovo 32 ГБ DDR5-4800 RDIMM", "Lenovo", "Комплектующие / Память", 19_800);
        Add("HDD-24-SAS", "Жёсткий диск HPE 2.4 ТБ SAS 10K", "HPE", "Комплектующие / Накопители", 27_400);
        Add("PSU-800", "Блок питания HPE 800 Вт Platinum", "HPE", "Комплектующие / БП", 14_200);
        Add("SFP-10G", "Оптический трансивер Huawei SFP+ 10G", "Huawei", "Сетевое оборудование / Оптика", 6_400);

        Line[] lines =
        [
            new("HPE", "Сервер HPE ProLiant DL380 Gen11", "Серверы / Стоечные", 420_000),
            new("HPE", "Сервер HPE ProLiant DL360 Gen11", "Серверы / Стоечные", 310_000),
            new("Dell", "Сервер Dell PowerEdge R760", "Серверы / Стоечные", 390_000),
            new("Dell", "Сервер Dell PowerEdge R660", "Серверы / Стоечные", 280_000),
            new("Lenovo", "Сервер Lenovo ThinkSystem SR650 V3", "Серверы / Стоечные", 360_000),
            new("Cisco", "Коммутатор Cisco Catalyst 9300", "Сетевое оборудование / Коммутаторы", 145_000),
            new("Cisco", "Коммутатор Cisco Catalyst 9200L", "Сетевое оборудование / Коммутаторы", 92_000),
            new("Huawei", "Коммутатор Huawei CloudEngine S5732", "Сетевое оборудование / Коммутаторы", 88_000),
            new("Dell", "Ноутбук Dell Latitude 5440", "ПК и ноутбуки", 95_000),
            new("Lenovo", "Ноутбук Lenovo ThinkPad T14 Gen4", "ПК и ноутбуки", 118_000),
            new("APC", "ИБП APC Smart-UPS SRT", "ИБП / Онлайн", 160_000),
            new("Eaton", "ИБП Eaton 9PX", "ИБП / Онлайн", 175_000),
            new("IEK", "Выключатель автоматический ВА47-29", "Модульное оборудование / Автоматы", 420),
            new("EKF", "Автоматический выключатель ВА47-63", "Модульное оборудование / Автоматы", 390),
            new("ДКС", "Кабель ВВГнг-LS", "Кабель / Силовой", 85),
            new("HP", "МФУ HP LaserJet Pro", "Периферия / Печать", 38_000),
            new("Dell", "Монитор Dell UltraSharp", "Периферия / Мониторы", 42_000),
            new("HPE", "Накопитель SSD SAS", "Комплектующие / Накопители", 36_000),
        ];
        string[] configs =
        [
            "2×Xeon Gold 6430, 128 ГБ", "1×Xeon Silver 4416+, 64 ГБ", "PoE+ 48 портов", "10G uplink",
            "Core i5-1345U, 16/512", "Core i7-1365U, 32/1TB", "стоечный 2U", "с байпасом",
            "16А 1P 4.5кА", "32А 3P 6кА", "3х2.5", "5х4", "1.92 ТБ", "27\" 4K", "OEM"
        ];

        var n = 0;
        while (list.Count < target)
        {
            var line = lines[n % lines.Length];
            var cfg = configs[n % configs.Length];
            Add($"STND-{n:00000}", $"{line.Model} — {cfg} #{n:00000}", line.Manufacturer, line.Category,
                Math.Round(line.Price * (0.85m + (n % 40) * 0.01m), 2));
            n++;
        }

        return list;
    }
}
