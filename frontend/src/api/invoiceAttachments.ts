// Контракты прикреплённых документов счёта NOC (ТЗ п.6.1). camelCase — как сериализует ASP.NET Core.
// Метаданные файла — без самих байтов; оригинал отдаётся отдельным эндпоинтом .../download.

export interface InvoiceAttachmentDto {
  id: string
  invoiceId: string
  fileName: string
  contentType: string
  uploadedBy?: string
  uploadedAtUtc: string
}
