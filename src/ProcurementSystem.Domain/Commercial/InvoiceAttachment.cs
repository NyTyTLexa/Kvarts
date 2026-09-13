using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Commercial;

/// <summary>
/// Прикреплённый документ счёта NOC (ТЗ п.6.1: «прикладывание договора и документации» к счёту —
/// УК-08, загружает Бухгалтерия). Файл хранится в БД (bytea), как и архив прайсов (PriceImportUpload):
/// оригинал нужен для повторного скачивания/аудита, отдельное файловое хранилище не заводим.
/// </summary>
public class InvoiceAttachment : AuditableEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public string FileName { get; set; } = default!;       // оригинальное имя файла
    public string ContentType { get; set; } = default!;    // MIME-тип для отдачи при скачивании
    public byte[] FileContent { get; set; } = [];          // сам файл (bytea)
    public string? UploadedBy { get; set; }                // пользователь, загрузивший документ
}
