import DemoProductPicker from '@/views/flow/components/DemoProductPicker.vue'
import { registerFormComponent } from './formComponentRegistry'

// 业务方 / 子系统在此登记自定义表单组件（L3）。
// 表单 Schema 里写 { "componentId": "demo:product-picker" } 即可引用。
registerFormComponent('demo:product-picker', DemoProductPicker)
