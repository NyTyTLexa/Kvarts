// Контракты Трека B (Логистика + интеграции). camelCase — как сериализует ASP.NET Core.

export type ReceiptStatus = 'Черновик' | 'Проведено' | 'Отменена'

export interface ReceiptLineDto {
  id: string; productId?: string; sku?: string; name: string
  orderedQty: number; receivedQty: number; discrepancy: number; unitPrice: number
}
export interface ReceiptDto {
  id: string; orderId: string; orderNumber: string; customer?: string; status: ReceiptStatus
  linesCount: number; discrepancyCount: number; createdBy?: string; createdAtUtc: string
  completedAtUtc?: string; warehouseRef?: string; accountingRef?: string
}
export interface ReceiptDetailDto {
  id: string; orderId: string; orderNumber: string; customer?: string; status: ReceiptStatus
  createdBy?: string; createdAtUtc: string; completedAtUtc?: string
  warehouseRef?: string; accountingRef?: string; lines: ReceiptLineDto[]
}

export interface IntegrationStatusDto { system: string; kind: string; available: boolean }
