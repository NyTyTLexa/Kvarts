// Контракты бэкенда модуля «Коммерческий контур» (ТРЕК A): согласование КБ + счёт NOC.
// camelCase — как сериализует ASP.NET Core; enum-ы идут строками (JsonStringEnumConverter).

export type ApprovalStatus = 'НаСогласованииРП' | 'ВКоммерческомБлоке' | 'Согласовано' | 'Отклонено'

export type QuoteStrategy = 'MinCost' | 'MinLeadTime' | 'Balanced' | 'MlRelevance'

export interface ApprovalDto {
  id: string
  specificationId: string
  strategy: QuoteStrategy
  title: string
  customer?: string
  costPrice: number
  markupPercent: number
  sellPrice: number
  marginPercent: number
  status: ApprovalStatus
  createdBy?: string
  createdAtUtc: string
  decidedAtUtc?: string
  comment?: string
}

export interface CreateApprovalRequest {
  specificationId: string
  strategy: QuoteStrategy
  markupPercent: number
}
export interface SetMarginRequest { markupPercent: number }
export interface DecisionRequest { approved: boolean; comment?: string }

export type InvoiceStatus =
  | 'Создан' | 'Согласован' | 'ОжиданиеОплаты' | 'ЧастичнаяОплата' | 'Оплачено'
  | 'ОжиданиеПоставки' | 'ПришёлНаСклад' | 'ОтраженоВ1С' | 'Отменён'

export interface InvoiceDto {
  id: string
  number: string
  approvalId: string
  customer?: string
  contract?: string
  costPrice: number
  markupPercent: number
  sellPrice: number
  status: InvoiceStatus
  dueDateUtc?: string
  createdBy?: string
  createdAtUtc: string
  linesCount: number
}

// Строка счёта — снимок позиции согласованного варианта КП (ТЗ п.6.1).
export interface InvoiceLineDto {
  id: string
  productId?: string
  sku?: string
  name: string
  quantity: number
  vendorName?: string
  manufacturer?: string
  unitCost: number
  unitPrice: number
  lineTotal: number
}

// Детальный счёт (GET /api/invoices/{id}) — шапка + позиции.
export interface InvoiceDetailDto extends Omit<InvoiceDto, 'linesCount'> {
  lines: InvoiceLineDto[]
}

export interface CreateInvoiceRequest {
  approvalId: string
  contract?: string | null
  dueDateUtc?: string | null
}
export interface SetInvoiceStatusRequest { status: InvoiceStatus }
