export type MatchKind = 'None' | 'ExactSku' | 'FuzzyName' | 'Analog'

export interface MatchSuggestion {
  productId: string
  sku: string
  name: string
  manufacturer?: string
  category?: string
  kind: MatchKind
  probability: number
  nameCosine: number
  reason: string
}

export interface ItemMatchDto {
  itemId: string
  sku?: string
  name: string
  quantity: number
  currentProductId?: string
  matched: boolean
  suggestions: MatchSuggestion[]
}

export interface SpecMatchDto {
  specificationId: string
  title: string
  items: number
  matched: number
  unmatched: number
  modelReady: boolean
  catalogSize: number
  itemsDetail: ItemMatchDto[]
  elapsedMs: number
  trainedOn: number
  exactHits: number
  fuzzyHits: number
  analogHits: number
}
