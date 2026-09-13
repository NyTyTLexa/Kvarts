// Контракты бэкенда (зеркало DTO из Infrastructure). camelCase — как сериализует ASP.NET Core.
import type { OrderStatus, QuoteStrategy } from '../data/orders'

export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number }

export interface ProductDto { id: string; sku: string; name: string; manufacturer?: string; category?: string }
export interface CategoryDto { path: string; productsCount: number }

export interface VendorDto { id: string; name: string; inn?: string; defaultLeadTimeDays: number }

export interface OfferDto {
  id: string; productId: string; vendorId: string; vendorName: string
  price: number; currency: string; leadTimeDays: number; stockQuantity: number
  sourceUrl?: string
}
export interface PriceHistoryDto { vendorId: string; price: number; leadTimeDays: number; recordedAtUtc: string }
export interface PriceImportResult { rowsProcessed: number; productsCreated: number; offersUpserted: number; errors: string[] }

export interface AuditChangeDto {
  entity: string; entityId: string; property: string; oldValue?: string | null; newValue?: string | null
}
export interface AuditEntryDto {
  userName?: string; action: string; path: string; statusCode: number; occurredAtUtc: string
  changes?: AuditChangeDto[]
}

export interface SpecificationDto {
  id: string; title: string; customer?: string
  itemsCount: number; matchedCount: number; createdAtUtc: string
}
export interface SpecificationItemDto {
  id: string; productId?: string; sku?: string; name: string; quantity: number; matched: boolean
}
export interface SpecificationDetailDto {
  id: string; title: string; customer?: string; createdAtUtc: string; items: SpecificationItemDto[]
}
export interface SpecImportResult {
  specificationId: string; rows: number; matched: number; unmatched: number; errors: string[]
}

export interface QuoteLine {
  specificationItemId: string; productId?: string; sku?: string; name: string; quantity: number
  matched: boolean; vendorId?: string; vendorName?: string
  unitPrice: number; lineTotal: number; leadTimeDays: number; stockQuantity: number; candidateOffers: number
  // Скидки и НДС (ТЗ п.5.4)
  discountPercent: number; unitPriceDiscounted: number
  unitPriceWithVat: number; unitPriceWithVatDiscounted: number; lineTotalWithVat: number
  manufacturer?: string; selectionReason?: string
  matchKind?: string; matchScore?: number
}
export interface Quote {
  specificationId: string; specificationTitle: string; strategy: QuoteStrategy
  weightPrice: number; weightLeadTime: number; lines: QuoteLine[]
  totalCost: number; maxLeadTimeDays: number; vendorsUsed: number
  matchedPositions: number; unmatchedPositions: number
  vatRate: number; totalCostWithVat: number
}

export interface DiscountDto {
  id: string; target: 'Vendor' | 'Manufacturer'; vendorId?: string; vendorName?: string; vendorInn?: string
  manufacturer?: string; percent: number; validFromUtc?: string; validToUtc?: string
  createdBy?: string; createdAtUtc: string; updatedAtUtc?: string; isActive: boolean
}

export interface OrderDto {
  id: string; number: string; specificationId?: string; title: string; customer?: string; strategy: string
  status: OrderStatus; totalCost: number; maxLeadTimeDays: number
  linesCount: number; createdBy?: string; createdAtUtc: string
}
export interface OrderLineDto {
  id: string; productId?: string; sku?: string; name: string; quantity: number
  vendorId?: string; vendorName?: string; unitPrice: number; lineTotal: number; leadTimeDays: number
}
export interface OrderDetailDto {
  id: string; number: string; title: string; customer?: string; strategy: string
  status: OrderStatus; totalCost: number; maxLeadTimeDays: number
  createdBy?: string; createdAtUtc: string; lines: OrderLineDto[]
}
