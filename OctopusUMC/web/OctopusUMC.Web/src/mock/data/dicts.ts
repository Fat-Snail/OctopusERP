import type { DictTypeResponse, DictDataResponse } from '@/api/system/types'

export const dictTypesData: DictTypeResponse[] = [
  {
    dictId: 1,
    dictName: '系统开关',
    dictType: 'sys_normal_disable',
    status: 1,
    createTime: '2024-01-01 00:00:00',
    remark: '系统开关列表'
  },
  {
    dictId: 2,
    dictName: '系统是否',
    dictType: 'sys_yes_no',
    status: 1,
    createTime: '2024-01-01 00:00:00',
    remark: '系统是否列表'
  },
  {
    dictId: 3,
    dictName: '用户性别',
    dictType: 'sys_user_sex',
    status: 1,
    createTime: '2024-01-01 00:00:00',
    remark: '用户性别列表'
  }
]

export const dictDataItems: DictDataResponse[] = [
  {
    dictCode: 1,
    dictType: 'sys_normal_disable',
    dictLabel: '正常',
    dictValue: '0',
    dictSort: 1,
    status: 1,
    isDefault: true,
    createTime: '2024-01-01 00:00:00',
    remark: null
  },
  {
    dictCode: 2,
    dictType: 'sys_normal_disable',
    dictLabel: '停用',
    dictValue: '1',
    dictSort: 2,
    status: 1,
    isDefault: false,
    createTime: '2024-01-01 00:00:00',
    remark: null
  },
  {
    dictCode: 3,
    dictType: 'sys_yes_no',
    dictLabel: '是',
    dictValue: 'Y',
    dictSort: 1,
    status: 1,
    isDefault: true,
    createTime: '2024-01-01 00:00:00',
    remark: null
  },
  {
    dictCode: 4,
    dictType: 'sys_yes_no',
    dictLabel: '否',
    dictValue: 'N',
    dictSort: 2,
    status: 1,
    isDefault: false,
    createTime: '2024-01-01 00:00:00',
    remark: null
  },
  {
    dictCode: 5,
    dictType: 'sys_user_sex',
    dictLabel: '男',
    dictValue: '1',
    dictSort: 1,
    status: 1,
    isDefault: true,
    createTime: '2024-01-01 00:00:00',
    remark: null
  },
  {
    dictCode: 6,
    dictType: 'sys_user_sex',
    dictLabel: '女',
    dictValue: '0',
    dictSort: 2,
    status: 1,
    isDefault: false,
    createTime: '2024-01-01 00:00:00',
    remark: null
  },
  {
    dictCode: 7,
    dictType: 'sys_user_sex',
    dictLabel: '未知',
    dictValue: '2',
    dictSort: 3,
    status: 1,
    isDefault: false,
    createTime: '2024-01-01 00:00:00',
    remark: null
  }
]
