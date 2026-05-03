<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useUserStore } from '@/store/modules/user'
import { put } from '@/utils/http'
import Badge from '@/components/ui/badge.vue'

const userStore = useUserStore()
const activeTab = ref<'basic' | 'password'>('basic')
const savingBasic = ref(false)
const savingPwd = ref(false)
const basicError = ref('')
const basicSuccess = ref('')
const pwdError = ref('')
const pwdSuccess = ref('')

const basicForm = reactive({
  nickName: userStore.userInfo?.nickName ?? '',
  phoneNumber: userStore.userInfo?.phoneNumber ?? '',
  email: userStore.userInfo?.email ?? '',
  sex: userStore.userInfo?.sex ?? '1',
})

const pwdForm = reactive({ oldPassword: '', newPassword: '', confirmPassword: '' })

async function handleSaveBasic() {
  basicError.value = ''
  basicSuccess.value = ''
  if (!basicForm.nickName) { basicError.value = '请输入昵称'; return }
  savingBasic.value = true
  try {
    await put('/account/me', basicForm)
    if (userStore.userInfo) {
      userStore.userInfo.nickName = basicForm.nickName
      userStore.userInfo.phoneNumber = basicForm.phoneNumber
      userStore.userInfo.email = basicForm.email
      userStore.userInfo.sex = basicForm.sex
    }
    basicSuccess.value = '保存成功'
  } catch {
    basicError.value = '保存失败，请稍后重试'
  } finally {
    savingBasic.value = false
  }
}

async function handleSavePwd() {
  pwdError.value = ''
  pwdSuccess.value = ''
  if (!pwdForm.oldPassword) { pwdError.value = '请输入旧密码'; return }
  if (!pwdForm.newPassword || pwdForm.newPassword.length < 6) { pwdError.value = '新密码至少6位'; return }
  if (pwdForm.newPassword !== pwdForm.confirmPassword) { pwdError.value = '两次输入的密码不一致'; return }
  savingPwd.value = true
  try {
    await put('/system/user/resetPwd', {
      userId: userStore.userInfo?.userId,
      password: pwdForm.newPassword,
    })
    pwdSuccess.value = '密码修改成功，请重新登录'
    resetPwdForm()
  } catch {
    pwdError.value = '密码修改失败'
  } finally {
    savingPwd.value = false
  }
}

function resetPwdForm() {
  pwdForm.oldPassword = ''
  pwdForm.newPassword = ''
  pwdForm.confirmPassword = ''
}

onMounted(() => {
  if (userStore.userInfo) {
    basicForm.nickName = userStore.userInfo.nickName
    basicForm.phoneNumber = userStore.userInfo.phoneNumber ?? ''
    basicForm.email = userStore.userInfo.email ?? ''
    basicForm.sex = userStore.userInfo.sex ?? '1'
  }
})
</script>

<template>
  <div class="grid grid-cols-[280px_1fr] gap-4">
    <!-- Left: user summary -->
    <div class="bg-card border border-border rounded-lg p-6 flex flex-col items-center">
      <div class="w-20 h-20 rounded-full bg-primary-soft flex items-center justify-center mb-3">
        <span class="text-primary-soft-fg font-semibold text-[26px]">
          {{ (userStore.userInfo?.nickName || userStore.userInfo?.userName || 'A').slice(0, 1).toUpperCase() }}
        </span>
      </div>
      <h3 class="text-[16px] font-semibold text-foreground mt-1">
        {{ userStore.userInfo?.nickName || userStore.userInfo?.userName || 'Admin' }}
      </h3>
      <p class="text-[12px] text-muted-foreground">{{ userStore.userInfo?.deptName || '未知部门' }}</p>
      <div class="w-full border-t border-border my-4" />
      <div class="w-full space-y-3">
        <div class="flex gap-2 text-[13px]">
          <span class="text-muted-foreground">职位：</span>
          <span class="text-foreground">{{ userStore.userInfo?.postName || '无' }}</span>
        </div>
        <div class="flex items-start gap-2 text-[13px]">
          <span class="text-muted-foreground shrink-0">角色：</span>
          <div class="flex flex-wrap gap-1">
            <Badge v-for="r in userStore.userInfo?.roles" :key="r" variant="secondary">{{ r }}</Badge>
            <span v-if="!userStore.userInfo?.roles?.length" class="text-muted-foreground">无角色</span>
          </div>
        </div>
        <div class="flex gap-2 text-[13px]">
          <span class="text-muted-foreground">邮箱：</span>
          <span class="text-foreground truncate">{{ userStore.userInfo?.email || '-' }}</span>
        </div>
      </div>
    </div>

    <!-- Right: tabs -->
    <div class="bg-card border border-border rounded-lg overflow-hidden">
      <!-- Tab headers -->
      <div class="flex border-b border-border px-4">
        <button
          v-for="tab in [{ key: 'basic', label: '基本资料' }, { key: 'password', label: '修改密码' }]"
          :key="tab.key"
          :class="[
            'px-4 py-3 text-[13px] font-medium border-b-2 -mb-px transition-colors cursor-pointer border-0 bg-transparent',
            activeTab === tab.key
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          ]"
          @click="activeTab = tab.key as 'basic' | 'password'"
        >
          {{ tab.label }}
        </button>
      </div>

      <div class="p-6">
        <!-- Basic info tab -->
        <div v-if="activeTab === 'basic'" class="max-w-[480px] space-y-4">
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">用户名</label>
            <input
              :value="userStore.userInfo?.userName"
              disabled
              class="w-full h-9 px-3 rounded-[var(--radius)] border border-input bg-muted text-[13px] text-muted-foreground"
            />
          </div>
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">昵称 <span class="text-danger">*</span></label>
            <input
              v-model="basicForm.nickName"
              placeholder="请输入昵称"
              class="w-full h-9 px-3 rounded-[var(--radius)] border border-input bg-background text-[13px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">手机号</label>
            <input
              v-model="basicForm.phoneNumber"
              placeholder="请输入手机号"
              class="w-full h-9 px-3 rounded-[var(--radius)] border border-input bg-background text-[13px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">邮箱</label>
            <input
              v-model="basicForm.email"
              placeholder="请输入邮箱"
              class="w-full h-9 px-3 rounded-[var(--radius)] border border-input bg-background text-[13px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">性别</label>
            <div class="flex gap-4">
              <label v-for="opt in [{ v: '1', l: '男' }, { v: '0', l: '女' }, { v: '2', l: '未知' }]" :key="opt.v" class="flex items-center gap-1.5 cursor-pointer">
                <input v-model="basicForm.sex" type="radio" :value="opt.v" class="w-3.5 h-3.5" />
                <span class="text-[13px] text-foreground">{{ opt.l }}</span>
              </label>
            </div>
          </div>
          <p v-if="basicError" class="text-[12px] text-danger">{{ basicError }}</p>
          <p v-if="basicSuccess" class="text-[12px] text-success">{{ basicSuccess }}</p>
          <button
            :disabled="savingBasic"
            class="h-9 px-4 bg-primary text-primary-foreground text-[13px] font-medium rounded-[var(--radius)] hover:opacity-90 transition-opacity disabled:opacity-50 cursor-pointer border-0"
            @click="handleSaveBasic"
          >
            {{ savingBasic ? '保存中...' : '保存修改' }}
          </button>
        </div>

        <!-- Change password tab -->
        <div v-if="activeTab === 'password'" class="max-w-[480px] space-y-4">
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">旧密码 <span class="text-danger">*</span></label>
            <input
              v-model="pwdForm.oldPassword"
              type="password"
              placeholder="请输入旧密码"
              class="w-full h-9 px-3 rounded-[var(--radius)] border border-input bg-background text-[13px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">新密码 <span class="text-danger">*</span></label>
            <input
              v-model="pwdForm.newPassword"
              type="password"
              placeholder="请输入新密码（至少6位）"
              class="w-full h-9 px-3 rounded-[var(--radius)] border border-input bg-background text-[13px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">确认密码 <span class="text-danger">*</span></label>
            <input
              v-model="pwdForm.confirmPassword"
              type="password"
              placeholder="请再次输入新密码"
              class="w-full h-9 px-3 rounded-[var(--radius)] border border-input bg-background text-[13px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>
          <p v-if="pwdError" class="text-[12px] text-danger">{{ pwdError }}</p>
          <p v-if="pwdSuccess" class="text-[12px] text-success">{{ pwdSuccess }}</p>
          <div class="flex gap-2">
            <button
              :disabled="savingPwd"
              class="h-9 px-4 bg-primary text-primary-foreground text-[13px] font-medium rounded-[var(--radius)] hover:opacity-90 transition-opacity disabled:opacity-50 cursor-pointer border-0"
              @click="handleSavePwd"
            >
              {{ savingPwd ? '修改中...' : '修改密码' }}
            </button>
            <button
              class="h-9 px-4 bg-muted text-foreground text-[13px] font-medium rounded-[var(--radius)] hover:bg-muted/80 transition-colors cursor-pointer border border-border"
              @click="resetPwdForm"
            >
              重置
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
