<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { CheckCircle2, XCircle } from 'lucide-vue-next'
import { toast } from '@/components/ui/toast'
import { h5GetOnboard, h5Submit } from '@/api/employee'
import type { H5OnboardResponse, H5SubmitRequest } from '@/api/employee/types'

const route = useRoute()
const token = route.params['token'] as string
const loading = ref(true)
const submitting = ref(false)
const error = ref('')
const info = ref<H5OnboardResponse | null>(null)
const submitted = ref(false)
const step = ref(0)

const STEPS = ['个人信息', '教育经历', '工作经历', '家庭&紧急', '银行卡']

const form = reactive<H5SubmitRequest>({
  idCardNo: '', politicalStatus: '', maritalStatus: '', nativePlace: '',
  currentAddress: '', householdAddress: '', bankName: '', bankAccount: '', bankBranch: '',
  educations: [{ school: '', education: '', major: '' }],
  workHistories: [],
  families: [{ name: '', relation: '' }],
  emergencyContacts: [{ name: '', relation: '', phone: '' }],
})

async function handleSubmit() {
  if (!form.emergencyContacts.some(ec => ec.name && ec.phone)) {
    toast('请至少填写一位紧急联系人', 'warning')
    return
  }
  submitting.value = true
  try {
    await h5Submit(token, form)
    submitted.value = true
  } catch (e: unknown) { toast((e as Error).message, 'error') } finally { submitting.value = false }
}

onMounted(async () => {
  try {
    info.value = await h5GetOnboard(token)
  } catch {
    error.value = '入职链接无效或已过期'
  } finally { loading.value = false }
})
</script>

<template>
  <div class="min-h-screen bg-subtle">
    <div class="max-w-[600px] mx-auto px-4 py-6">

      <!-- 加载中 -->
      <div v-if="loading" class="flex flex-col items-center justify-center min-h-[60vh] gap-3">
        <div class="w-8 h-8 border-2 border-primary/20 border-t-primary rounded-full animate-spin" />
        <p class="text-[13px] text-muted-foreground">加载中...</p>
      </div>

      <!-- 无效链接 -->
      <div v-else-if="error" class="flex flex-col items-center justify-center min-h-[60vh] gap-4 text-center">
        <XCircle :size="52" class="text-danger" />
        <h2 class="text-[18px] font-semibold text-foreground">链接无效</h2>
        <p class="text-[13px] text-muted-foreground">{{ error }}</p>
      </div>

      <!-- 已填写 -->
      <div v-else-if="info?.alreadyFilled" class="flex flex-col items-center justify-center min-h-[60vh] gap-4 text-center">
        <CheckCircle2 :size="52" class="text-success" />
        <h2 class="text-[18px] font-semibold text-foreground">信息已提交</h2>
        <p class="text-[13px] text-muted-foreground">请等待 HR 审核确认，您无需再次填写。</p>
      </div>

      <!-- 提交成功 -->
      <div v-else-if="submitted" class="flex flex-col items-center justify-center min-h-[60vh] gap-4 text-center">
        <CheckCircle2 :size="52" class="text-success" />
        <h2 class="text-[18px] font-semibold text-foreground">提交成功</h2>
        <p class="text-[13px] text-muted-foreground">您的入职信息已提交，请等待 HR 确认。</p>
      </div>

      <!-- 分步表单 -->
      <div v-else-if="info">
        <!-- Header -->
        <div class="text-center mb-5">
          <h2 class="text-[20px] font-semibold text-foreground">入职信息填写</h2>
          <p class="text-[13px] text-muted-foreground mt-1">{{ info.name }} | {{ info.applyPosition }} | {{ info.applyDeptName || '未分配部门' }}</p>
        </div>

        <!-- Step indicator -->
        <div class="flex items-center mb-6">
          <template v-for="(s, i) in STEPS" :key="i">
            <div class="flex flex-col items-center gap-1 flex-1">
              <div :class="['w-7 h-7 rounded-full flex items-center justify-center text-[11px] font-semibold transition-colors', i < step ? 'bg-success text-white' : i === step ? 'bg-primary text-primary-foreground' : 'bg-border text-muted-foreground']">
                <span v-if="i < step">✓</span>
                <span v-else>{{ i + 1 }}</span>
              </div>
              <span :class="['text-[10px] leading-none', i === step ? 'text-primary font-medium' : 'text-muted-foreground']">{{ s }}</span>
            </div>
            <div v-if="i < STEPS.length - 1" :class="['h-px flex-[0_0_16px] mb-4', i < step ? 'bg-success' : 'bg-border']" />
          </template>
        </div>

        <!-- Step 0: 个人信息 -->
        <div v-show="step === 0" class="space-y-3">
          <div>
            <label class="block text-[12px] font-medium mb-1"><span class="text-danger">*</span> 身份证号</label>
            <input v-model="form.idCardNo" type="text" placeholder="18位身份证号" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">政治面貌</label>
            <select v-model="form.politicalStatus" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring">
              <option value="">请选择</option>
              <option value="群众">群众</option>
              <option value="团员">团员</option>
              <option value="党员">党员</option>
              <option value="民主党派">民主党派</option>
            </select>
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">婚姻状况</label>
            <select v-model="form.maritalStatus" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring">
              <option value="">请选择</option>
              <option value="未婚">未婚</option>
              <option value="已婚">已婚</option>
              <option value="离异">离异</option>
            </select>
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">籍贯</label>
            <input v-model="form.nativePlace" type="text" placeholder="如：广东广州" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1"><span class="text-danger">*</span> 现居住地址</label>
            <input v-model="form.currentAddress" type="text" placeholder="详细地址" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">户籍地址</label>
            <input v-model="form.householdAddress" type="text" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
        </div>

        <!-- Step 1: 教育经历 -->
        <div v-show="step === 1" class="space-y-3">
          <div v-for="(edu, idx) in form.educations" :key="idx" class="bg-card border border-border rounded-[var(--radius)] p-3 space-y-3">
            <div class="flex justify-between items-center">
              <span class="text-[12px] font-semibold">教育经历 {{ idx + 1 }}</span>
              <button v-if="idx > 0" class="text-[11px] text-danger cursor-pointer border-0 bg-transparent" @click="form.educations.splice(idx, 1)">删除</button>
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1">学校</label>
              <input v-model="edu.school" type="text" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
            </div>
            <div class="grid grid-cols-2 gap-2">
              <div>
                <label class="block text-[12px] font-medium mb-1">学历</label>
                <select v-model="edu.education" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring">
                  <option value="">请选择</option>
                  <option value="高中">高中</option>
                  <option value="大专">大专</option>
                  <option value="本科">本科</option>
                  <option value="硕士">硕士</option>
                  <option value="博士">博士</option>
                </select>
              </div>
              <div>
                <label class="block text-[12px] font-medium mb-1">专业</label>
                <input v-model="edu.major" type="text" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
              </div>
            </div>
            <div class="grid grid-cols-2 gap-2">
              <div>
                <label class="block text-[12px] font-medium mb-1">开始</label>
                <input v-model="edu.startDate" type="text" placeholder="2018-09" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
              </div>
              <div>
                <label class="block text-[12px] font-medium mb-1">结束</label>
                <input v-model="edu.endDate" type="text" placeholder="2022-06" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
              </div>
            </div>
          </div>
          <button class="text-[12px] text-primary cursor-pointer border-0 bg-transparent" @click="form.educations.push({ school: '', education: '', major: '' })">+ 添加教育经历</button>
        </div>

        <!-- Step 2: 工作经历 -->
        <div v-show="step === 2" class="space-y-3">
          <div v-for="(wh, idx) in form.workHistories" :key="idx" class="bg-card border border-border rounded-[var(--radius)] p-3 space-y-3">
            <div class="flex justify-between items-center">
              <span class="text-[12px] font-semibold">工作经历 {{ idx + 1 }}</span>
              <button v-if="idx > 0" class="text-[11px] text-danger cursor-pointer border-0 bg-transparent" @click="form.workHistories.splice(idx, 1)">删除</button>
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1">公司</label>
              <input v-model="wh.company" type="text" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1">职位</label>
              <input v-model="wh.position" type="text" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
            </div>
            <div class="grid grid-cols-2 gap-2">
              <div>
                <label class="block text-[12px] font-medium mb-1">开始</label>
                <input v-model="wh.startDate" type="text" placeholder="2020-07" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
              </div>
              <div>
                <label class="block text-[12px] font-medium mb-1">结束</label>
                <input v-model="wh.endDate" type="text" placeholder="2025-12" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
              </div>
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1">离职原因</label>
              <input v-model="wh.leaveReason" type="text" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
            </div>
          </div>
          <button class="text-[12px] text-primary cursor-pointer border-0 bg-transparent" @click="form.workHistories.push({ company: '', position: '' })">+ 添加工作经历</button>
          <p class="text-[11px] text-muted-foreground mt-1">应届生可跳过此步</p>
        </div>

        <!-- Step 3: 家庭成员 + 紧急联系人 -->
        <div v-show="step === 3" class="space-y-3">
          <h4 class="text-[13px] font-semibold text-foreground">家庭成员</h4>
          <div v-for="(fam, idx) in form.families" :key="'f'+idx" class="bg-card border border-border rounded-[var(--radius)] p-3 space-y-3">
            <div class="flex justify-between items-center">
              <span class="text-[12px] font-semibold">家庭成员 {{ idx + 1 }}</span>
              <button v-if="idx > 0" class="text-[11px] text-danger cursor-pointer border-0 bg-transparent" @click="form.families.splice(idx, 1)">删除</button>
            </div>
            <div class="grid grid-cols-2 gap-2">
              <div>
                <label class="block text-[12px] font-medium mb-1">姓名</label>
                <input v-model="fam.name" type="text" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
              </div>
              <div>
                <label class="block text-[12px] font-medium mb-1">关系</label>
                <select v-model="fam.relation" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring">
                  <option value="">请选择</option>
                  <option value="父亲">父亲</option>
                  <option value="母亲">母亲</option>
                  <option value="配偶">配偶</option>
                  <option value="子女">子女</option>
                </select>
              </div>
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1">工作单位</label>
              <input v-model="fam.workplace" type="text" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1">联系电话</label>
              <input v-model="fam.phone" type="tel" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
            </div>
          </div>
          <button class="text-[12px] text-primary cursor-pointer border-0 bg-transparent" @click="form.families.push({ name: '', relation: '' })">+ 添加家庭成员</button>

          <div class="border-t border-border my-4" />

          <h4 class="text-[13px] font-semibold text-foreground">紧急联系人（至少 1 人）</h4>
          <div v-for="(ec, idx) in form.emergencyContacts" :key="'e'+idx" class="bg-card border border-border rounded-[var(--radius)] p-3 space-y-3">
            <div class="flex justify-between items-center">
              <span class="text-[12px] font-semibold">紧急联系人 {{ idx + 1 }}</span>
              <button v-if="idx > 0" class="text-[11px] text-danger cursor-pointer border-0 bg-transparent" @click="form.emergencyContacts.splice(idx, 1)">删除</button>
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1">姓名</label>
              <input v-model="ec.name" type="text" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
            </div>
            <div class="grid grid-cols-2 gap-2">
              <div>
                <label class="block text-[12px] font-medium mb-1">关系</label>
                <input v-model="ec.relation" type="text" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
              </div>
              <div>
                <label class="block text-[12px] font-medium mb-1">联系电话</label>
                <input v-model="ec.phone" type="tel" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
              </div>
            </div>
          </div>
          <button class="text-[12px] text-primary cursor-pointer border-0 bg-transparent" @click="form.emergencyContacts.push({ name: '', relation: '', phone: '' })">+ 添加紧急联系人</button>
        </div>

        <!-- Step 4: 银行卡 -->
        <div v-show="step === 4" class="space-y-3">
          <div>
            <label class="block text-[12px] font-medium mb-1"><span class="text-danger">*</span> 开户银行</label>
            <input v-model="form.bankName" type="text" placeholder="如：中国工商银行" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1"><span class="text-danger">*</span> 银行卡号</label>
            <input v-model="form.bankAccount" type="text" placeholder="银行卡号" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">开户支行</label>
            <input v-model="form.bankBranch" type="text" placeholder="如：广州天河支行" class="w-full h-10 px-3 border border-input rounded-[var(--radius)] bg-card text-[13px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
        </div>

        <!-- 底部导航按钮 -->
        <div class="flex justify-center gap-3 mt-6 pb-10">
          <button v-if="step > 0" class="h-10 px-6 text-[13px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="step--">上一步</button>
          <button v-if="step < 4" class="h-10 px-6 text-[13px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0" @click="step++">下一步</button>
          <button v-if="step === 4" :disabled="submitting" class="h-10 px-6 text-[13px] bg-success text-white rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 disabled:opacity-50 flex items-center gap-2" @click="handleSubmit">
            <span v-if="submitting" class="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
            {{ submitting ? '提交中...' : '提交' }}
          </button>
        </div>
      </div>

    </div>
  </div>
</template>
