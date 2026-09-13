using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Import;

/// <summary>
/// Архив загруженного прайс-листа (ТЗ п.5.1: «система сохраняет оригинальный файл в архиве
/// загрузок», «поддерживается повторная загрузка — история предыдущих версий сохраняется»).
/// Хранит сам файл (для повторного скачивания/аудита) и итоги обработки.
/// </summary>
public class PriceImportUpload : AuditableEntity
{
    public Guid VendorId { get; set; }
    public Vendor? Vendor { get; set; }
    public string VendorName { get; set; } = default!;   // снимок на момент загрузки

    public string FileName { get; set; } = default!;
    public byte[] FileContent { get; set; } = [];
    public string? UploadedBy { get; set; }

    public int RowsProcessed { get; set; }
    public int ProductsCreated { get; set; }
    public int OffersUpserted { get; set; }
    public int ErrorsCount { get; set; }
    public string? ErrorsSummary { get; set; }            // первые несколько ошибок, склеенные через "; "
}
