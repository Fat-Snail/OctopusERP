export interface ChannelDef {
  channelId: number
  channelCode: string
  channelName: string
  status: number
  createdAt: string
}

export interface ChannelAttrMappingItem {
  mappingId: number
  attributeId: number
  attributeName: string
  attributeCode: string | null
  externalAttributeId: string
  externalAttributeName: string
  isRequiredOutbound: boolean
  transformRuleJson: string | null
  status: number
}

export interface ChannelCategoryMappingItem {
  mappingId: number
  channelId: number
  categoryId: number
  categoryName: string
  externalCategoryId: string
  externalCategoryName: string
  mappingVersion: string
  status: number
  createdAt: string
  attributeMappings: ChannelAttrMappingItem[]
}

export interface CreateChannelRequest {
  channelCode: string
  channelName: string
}

export interface UpdateChannelRequest {
  channelName: string
  status: number
}

export interface CreateChannelCategoryMappingRequest {
  categoryId: number
  externalCategoryId: string
  externalCategoryName: string
  mappingVersion: string
}

export interface UpdateChannelCategoryMappingRequest {
  externalCategoryId: string
  externalCategoryName: string
  status: number
}

export interface CreateChannelAttributeMappingRequest {
  attributeId: number
  externalAttributeId: string
  externalAttributeName: string
  isRequiredOutbound: boolean
  transformRuleJson?: string
}

export interface UpdateChannelAttributeMappingRequest {
  externalAttributeId: string
  externalAttributeName: string
  isRequiredOutbound: boolean
  transformRuleJson?: string
}
