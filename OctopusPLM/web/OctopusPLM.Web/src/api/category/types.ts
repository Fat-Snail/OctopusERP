/** 类目树节点 */
export interface CategoryTreeNode {
  categoryId: number
  parentId: number | null
  name: string
  level: number
  orderNum: number
  children: CategoryTreeNode[]
}

// ── Category CRUD requests ──

export interface CreateCategoryRequest {
  name: string
  parentId?: number | null
  orderNum: number
}

export interface UpdateCategoryRequest {
  name: string
  orderNum: number
}

// ── Attribute types ──

export interface AttributeValueItem {
  valueId: number
  label: string
  value: string
}

export interface PlmAttribute {
  attributeId: number
  name: string
  code: string | null
  attributeType: string
  inputType: string
  unit: string | null
  valueScope: string
  status: number
  values: AttributeValueItem[]
}

export interface CreateAttributeRequest {
  name: string
  code?: string
  attributeType: string
  inputType: string
  unit?: string
  valueScope: string
}

export interface UpdateAttributeRequest {
  name: string
  unit?: string
}

export interface AddAttributeValueRequest {
  label: string
  value: string
  orderNum: number
}

export interface BindAttributeRequest {
  attributeId: number
  isRequired: boolean
  isSaleAxis: boolean
  groupType: string
  groupName?: string
  orderNum: number
}

export interface CreateModelVersionRequest {
  changeSummary?: string
}

// ── Category attribute bindings ──

/** 类目下绑定的属性 */
export interface CategoryAttributeResponse {
  id: number
  attributeId: number
  code?: string | null
  name: string
  attributeType: string
  inputType: string
  unit: string | null
  isRequired: boolean
  orderNum: number
  groupName: string | null
  groupType: string
  isSaleAxis: boolean
  extRulesJson?: string | null
  values: AttributeValueItem[]
}

/** 按 GroupName 分组后的属性 */
export interface CategoryAttributeGrouped {
  groupName: string | null
  groupLabel: string
  attributes: CategoryAttributeResponse[]
}

export interface CategoryModelVersionSummary {
  modelVersionId: number
  versionNo: string
  status: string
  effectiveFrom: string
  publishedAt: string | null
  publishedBy: number | null
  changeSummary: string | null
}

export interface CategoryRuleResponse {
  ruleId: number
  ruleType: string
  ruleCode: string
  ruleName: string
  triggerExpr: string | null
  actionExpr: string
  priority: number
  status: number
  message: string | null
}

export interface DetailComponentItem {
  bindId: number
  componentId: number
  componentCode: string
  componentName: string
  componentType: string
  orderNum: number
  isRequired: boolean
  displayRuleJson: string | null
  defaultContentJson: string | null
}

export interface DetailTemplateResponse {
  templateId: number
  templateName: string
  status: string
  components: DetailComponentItem[]
}

export interface ChannelAttributeMappingResponse {
  mappingId: number
  attributeId: number
  attributeName: string
  attributeCode: string | null
  externalAttributeId: string
  externalAttributeName: string
  isRequiredOutbound: boolean
  transformRuleJson: string | null
}

export interface ChannelCategoryMappingResponse {
  mappingId: number
  channelCode: string
  channelName: string
  externalCategoryId: string
  externalCategoryName: string
  mappingVersion: string
  attributeMappings: ChannelAttributeMappingResponse[]
}

export interface CategoryModelDetailResponse {
  categoryId: number
  categoryName: string
  activeVersion: CategoryModelVersionSummary | null
  versions: CategoryModelVersionSummary[]
  attributeGroups: CategoryAttributeGrouped[]
  rules: CategoryRuleResponse[]
  detailTemplates: DetailTemplateResponse[]
  channelMappings: ChannelCategoryMappingResponse[]
}
