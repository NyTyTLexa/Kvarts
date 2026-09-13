// Контракты справочника производителей (ТЗ п.5.2). camelCase — как сериализует ASP.NET Core.

export interface ManufacturerDto {
  id: string; name: string; country?: string; isActive: boolean; productsCount: number
}
export interface CreateManufacturerRequest { name: string; country?: string | null }
export interface UpdateManufacturerRequest { name: string; country?: string | null; isActive: boolean }
