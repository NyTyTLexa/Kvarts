using Bogus;
using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Search;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Search;

namespace ProcurementSystem.Infrastructure.Seeding;

public record SeedResult(int Vendors, int Products, int Offers);

public interface IDataSeeder
{
    Task<SeedResult> SeedAsync(int vendors, int products, int maxOffersPerProduct, CancellationToken ct = default);

    /// <summary>Полностью очистить каталог (офферы, товары, поставщики).</summary>
    Task ResetAsync(CancellationToken ct = default);
}

/// <summary>
/// Генератор синтетических данных. Нужен, чтобы наполнить каталог тысячами позиций для
/// демонстрации поиска и генерации КП — руками такой объём не набрать. Названия строятся из
/// доменной номенклатуры (реальные модельные линейки оборудования, согласованные с производителем
/// и категорией), а цены офферов варьируются вокруг базовой цены товара — чтобы у одного товара
/// были разные предложения поставщиков и три стратегии КП (цена/срок/баланс) различались осмысленно.
/// </summary>
public class DataSeeder(AppDbContext db, ISearchEngine search) : IDataSeeder
{
    private sealed record Line(string Manufacturer, string Model);

    private sealed record CategoryDef(string Category, Line[] Lines, string[] Specs, decimal MinPrice, decimal MaxPrice);

    // Доменный «каталог» номенклатуры: для каждой категории — реалистичные модели (привязанные к
    // производителю), варианты конфигурации и характерный для категории диапазон цен.
    private static readonly CategoryDef[] Catalog =
    [
        new("Серверы",
            [
                new("HPE", "Сервер HPE ProLiant DL380 Gen11"),
                new("HPE", "Сервер HPE ProLiant DL360 Gen11"),
                new("HPE", "Сервер HPE ProLiant ML350 Gen11"),
                new("Dell", "Сервер Dell PowerEdge R760"),
                new("Dell", "Сервер Dell PowerEdge R660"),
                new("Lenovo", "Сервер Lenovo ThinkSystem SR650 V3"),
                new("Lenovo", "Сервер Lenovo ThinkSystem SR630 V3"),
                new("Huawei", "Сервер Huawei FusionServer 2288H V6"),
                new("Supermicro", "Сервер Supermicro SuperServer SYS-621P-TR")
            ],
            [
                "2×Xeon Gold 6430, 128 ГБ RAM", "2×Xeon Silver 4416+, 64 ГБ RAM",
                "1×Xeon Bronze 3408U, 32 ГБ RAM", "2×Xeon Platinum 8462Y+, 256 ГБ RAM",
                "1×EPYC 9354, 128 ГБ RAM", "2×Xeon Gold 5418Y, 192 ГБ RAM"
            ],
            180_000, 1_400_000),

        new("Сетевое оборудование",
            [
                new("Cisco", "Коммутатор Cisco Catalyst 9300-48P"),
                new("Cisco", "Коммутатор Cisco Catalyst 9200L-24P"),
                new("Cisco", "Маршрутизатор Cisco ISR 4451"),
                new("Cisco", "Межсетевой экран Cisco Firepower 1140"),
                new("Huawei", "Коммутатор Huawei CloudEngine S5732-H48"),
                new("Huawei", "Маршрутизатор Huawei NetEngine AR6300"),
                new("Huawei", "Точка доступа Huawei AirEngine 6760-X1")
            ],
            ["", "PoE+", "10G uplink", "стек 2 шт.", "с лицензией DNA Essentials"],
            8_000, 600_000),

        new("СХД",
            [
                new("Dell", "СХД Dell PowerVault ME5024"),
                new("Dell", "СХД Dell Unity XT 480"),
                new("HPE", "СХД HPE MSA 2062"),
                new("HPE", "СХД HPE Alletra 6010"),
                new("Huawei", "СХД Huawei OceanStor Dorado 3000 V6"),
                new("Lenovo", "СХД Lenovo ThinkSystem DE4000H")
            ],
            ["12×2.4 ТБ SAS", "24×1.92 ТБ SSD", "8×8 ТБ NL-SAS", "16×3.84 ТБ SSD NVMe"],
            250_000, 3_000_000),

        new("ПК и ноутбуки",
            [
                new("Dell", "Ноутбук Dell Latitude 5440"),
                new("Dell", "Моноблок Dell OptiPlex 7410 AIO"),
                new("Lenovo", "Ноутбук Lenovo ThinkPad T14 Gen4"),
                new("Lenovo", "Ноутбук Lenovo ThinkPad X1 Carbon Gen11"),
                new("Lenovo", "ПК Lenovo ThinkCentre M70q Gen4"),
                new("Huawei", "Ноутбук Huawei MateBook D16")
            ],
            [
                "Core i5-1345U, 16 ГБ, 512 ГБ SSD", "Core i7-1365U, 32 ГБ, 1 ТБ SSD",
                "Core i5-13500, 16 ГБ, 512 ГБ SSD", "Ryzen 7 7840U, 16 ГБ, 1 ТБ SSD"
            ],
            35_000, 280_000),

        new("Периферия",
            [
                new("Dell", "Монитор Dell UltraSharp U2723QE 27\" 4K"),
                new("Dell", "Монитор Dell P2422H 24\""),
                new("HP", "МФУ HP LaserJet Pro M428fdw"),
                new("HP", "Принтер HP LaserJet Enterprise M611dn"),
                new("Lenovo", "Док-станция Lenovo ThinkPad Universal USB-C"),
                new("Huawei", "Монитор Huawei MateView 28\" 4K+")
            ],
            ["", "комплект", "с гарантией 3 года"],
            2_000, 120_000),

        new("ИБП",
            [
                new("APC", "ИБП APC Smart-UPS SRT 5000 ВА"),
                new("APC", "ИБП APC Smart-UPS SC 1500 ВА"),
                new("APC", "ИБП APC Easy-UPS On-Line 3000 ВА"),
                new("Eaton", "ИБП Eaton 9PX 6000 ВА"),
                new("Eaton", "ИБП Eaton 5P 1550 ВА"),
                new("Eaton", "ИБП Eaton 93PS 8 кВА")
            ],
            ["", "с байпасом", "стоечный 2U", "доп. батарейный модуль"],
            6_000, 800_000),

        new("Комплектующие",
            [
                new("Dell", "Накопитель SSD Dell 1.92 ТБ SAS"),
                new("Lenovo", "Модуль памяти Lenovo 32 ГБ DDR5-4800 RDIMM"),
                new("HPE", "Жёсткий диск HPE 2.4 ТБ SAS 10K"),
                new("HPE", "Блок питания HPE 800 Вт Platinum"),
                new("Supermicro", "Материнская плата Supermicro X13SAE"),
                new("Huawei", "Оптический трансивер Huawei SFP+ 10G")
            ],
            ["", "OEM", "новый, запечатанный"],
            1_500, 180_000)
    ];

    // Производители, встречающиеся в Catalog выше — справочник (ТЗ п.5.2), не просто строка товара.
    private static readonly (string Name, string Country)[] ManufacturerSeed =
    [
        ("HPE", "США"), ("Dell", "США"), ("Lenovo", "Китай"), ("Huawei", "Китай"),
        ("Supermicro", "США"), ("Cisco", "США"), ("HP", "США"), ("APC", "Ирландия"), ("Eaton", "Ирландия"),
    ];

    public async Task<SeedResult> SeedAsync(int vendors, int products, int maxOffersPerProduct, CancellationToken ct = default)
    {
        var rnd = new Random(42);

        var existingMfrs = (await db.Set<Manufacturer>().Select(m => m.Name).ToListAsync(ct)).ToHashSet();
        db.AddRange(ManufacturerSeed.Where(m => !existingMfrs.Contains(m.Name))
            .Select(m => new Manufacturer { Name = m.Name, Country = m.Country }));

        var vendorFaker = new Faker<Vendor>("ru")
            .RuleFor(v => v.Id, _ => Guid.NewGuid())
            .RuleFor(v => v.Name, f => f.Company.CompanyName())
            .RuleFor(v => v.Inn, f => f.Random.ReplaceNumbers("##########"))
            .RuleFor(v => v.DefaultLeadTimeDays, f => f.Random.Int(1, 30))
            .RuleFor(v => v.CreatedAtUtc, _ => DateTime.UtcNow);

        var vendorList = vendorFaker.Generate(vendors);
        db.Vendors.AddRange(vendorList);
        await db.SaveChangesAsync(ct);

        const int batch = 2000;
        var productBuffer = new List<Product>(batch);
        var offerBuffer = new List<PriceListItem>(batch * maxOffersPerProduct);
        int offerCount = 0;

        for (int i = 0; i < products; i++)
        {
            ct.ThrowIfCancellationRequested();

            var cat = Catalog[rnd.Next(Catalog.Length)];
            var line = cat.Lines[rnd.Next(cat.Lines.Length)];
            var spec = cat.Specs[rnd.Next(cat.Specs.Length)];

            var p = new Product
            {
                Id = Guid.NewGuid(),
                Sku = MakeSku(line.Manufacturer, rnd),
                Name = spec.Length == 0 ? line.Model : $"{line.Model} — {spec}",
                Manufacturer = line.Manufacturer,
                Category = cat.Category,
                CreatedAtUtc = DateTime.UtcNow
            };
            productBuffer.Add(p);

            // Базовая цена товара (по диапазону категории) — офферы поставщиков пляшут вокруг неё.
            var basePrice = cat.MinPrice + (decimal)(rnd.NextDouble() * (double)(cat.MaxPrice - cat.MinPrice));
            basePrice = Math.Round(basePrice / 100m, 0) * 100m;

            int offers = rnd.Next(1, maxOffersPerProduct + 1);
            for (int j = 0; j < offers; j++)
            {
                var vendor = vendorList[rnd.Next(vendorList.Count)];
                var markup = 0.90 + rnd.NextDouble() * 0.25;                 // −10% … +15% к базовой
                var lead = Math.Clamp(vendor.DefaultLeadTimeDays + rnd.Next(-3, 8), 1, 60);
                offerBuffer.Add(new PriceListItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = p.Id,
                    VendorId = vendor.Id,
                    Price = Math.Round(basePrice * (decimal)markup, 2),
                    Currency = "RUB",
                    LeadTimeDays = lead,
                    StockQuantity = rnd.Next(0, 500),
                    CreatedAtUtc = DateTime.UtcNow
                });
                offerCount++;
            }

            if (productBuffer.Count >= batch)
                await FlushAsync(productBuffer, offerBuffer, ct);
        }

        await FlushAsync(productBuffer, offerBuffer, ct);
        return new SeedResult(vendors, products, offerCount);
    }

    public async Task ResetAsync(CancellationToken ct = default)
    {
        await db.PriceListItems.ExecuteDeleteAsync(ct);
        await db.Products.ExecuteDeleteAsync(ct);
        await db.Vendors.ExecuteDeleteAsync(ct);
        await db.Set<Manufacturer>().ExecuteDeleteAsync(ct);
        try { await search.ClearAsync(ProductIndexer.Index, ct); }
        catch { /* Meilisearch лёг — каталог в БД уже очищен */ }
    }

    private async Task FlushAsync(List<Product> products, List<PriceListItem> offers, CancellationToken ct)
    {
        if (products.Count == 0) return;
        db.Products.AddRange(products);
        db.PriceListItems.AddRange(offers);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        products.Clear();
        offers.Clear();
    }

    // Артикул в стиле, характерном для производителя (для убедительности демо).
    private static string MakeSku(string mfr, Random rnd) => mfr switch
    {
        "HPE" or "HP" => $"P{rnd.Next(10000, 99999)}-B21",
        "Dell"        => $"210-{Letters(rnd, 4)}",
        "Cisco"       => $"C9{rnd.Next(200, 600)}-{rnd.Next(24, 48)}P-{Letters(rnd, 2)}",
        "Lenovo"      => $"7D{rnd.Next(10, 99)}{Letters(rnd, 1)}{rnd.Next(100, 999)}EA",
        "Huawei"      => $"02{rnd.Next(100000, 999999)}",
        "APC"         => $"SRT{rnd.Next(1000, 9000)}RMXLI",
        "Eaton"       => $"9PX{rnd.Next(1000, 9000)}IRT",
        "Supermicro"  => $"MBD-{Letters(rnd, 4)}-{rnd.Next(10, 99)}",
        _             => rnd.Next(100000, 999999).ToString()
    };

    private static string Letters(Random rnd, int n) =>
        string.Create(n, rnd, (span, r) =>
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            for (int i = 0; i < span.Length; i++) span[i] = alphabet[r.Next(alphabet.Length)];
        });
}
