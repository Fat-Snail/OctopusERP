// ── Request types ──

export interface CreateSkuRequest {
  skuCode: string
  barcode?: string
  saleAttributes?: Record<string, string>
  price: number
  costPrice?: number
  stock: number
}

export interface CreateProductRequest {
  categoryId: number
  productName: string
  description?: string
  mainImage?: string
  attributes?: Record<string, string>
  skus?: CreateSkuRequest[]
  images?: string[]
  vectorizeAfterSave?: boolean
}

export interface UpdateProductRequest {
  categoryId: number
  productName: string
  description?: string
  mainImage?: string
  attributes?: Record<string, string>
  skus?: CreateSkuRequest[]
  images?: string[]
  vectorizeAfterSave?: boolean
}

export interface ReviewActionRequest {
  comment?: string
}

// ── Response types ──

export interface SkuResponse {
  skuId: number
  skuCode: string
  barcode: string | null
  saleAttributes: Record<string, string>
  price: number
  costPrice: number | null
  stock: number
  status: number
}

export interface ProductResponse {
  productId: number
  categoryId: number
  categoryName: string
  productName: string
  description: string | null
  mainImage: string | null
  images: string[]
  attributes: Record<string, string>
  status: string
  statusLabel: string
  createdBy: number
  createdByName: string
  createdAt: string
  updatedAt: string
  skus: SkuResponse[]
}

export interface ProductListRow {
  productId: number
  categoryId: number
  categoryName: string
  productName: string
  mainImage: string | null
  status: string
  statusLabel: string
  statusInt: number
  minPrice: number
  maxPrice: number
  totalStock: number
  createdAt: string
  updatedAt: string
}

export interface ProductListResult {
  rows: ProductListRow[]
  total: number
}

export interface ReviewHistoryItem {
  reviewId: number
  action: string
  actionLabel: string
  reviewerId: number
  reviewerName: string
  comment: string | null
  createdAt: string
}

export interface ProductStatsResponse {
  total: number
  draft: number
  pendingReview: number
  approved: number
  rejected: number
  active: number
  discontinued: number
}

export interface ImageSearchItem {
  score: number
  imageDescription: string
  product: ProductListRow
}

export interface ImageSearchResult {
  queryDescription: string
  items: ImageSearchItem[]
}

// ── 1688 批量导入 ──

export interface Import1688ItemResult {
  sourceId: number
  subject: string
  status: 'imported' | 'skipped' | 'failed'
  productId: number | null
  error: string | null
}

export interface Import1688Result {
  total: number
  imported: number
  skipped: number
  failed: number
  items: Import1688ItemResult[]
}
