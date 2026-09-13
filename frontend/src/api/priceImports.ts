// Контракты архива загрузок прайсов (ТЗ п.5.1). camelCase — как сериализует ASP.NET Core.

export interface PriceImportUploadDto {
  id: string; vendorId: string; vendorName: string; fileName: string
  uploadedAtUtc: string; uploadedBy?: string
  rowsProcessed: number; productsCreated: number; offersUpserted: number; errorsCount: number
}
