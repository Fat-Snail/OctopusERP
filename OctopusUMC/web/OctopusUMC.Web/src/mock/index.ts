import { setupWorker } from 'msw/browser'
import { accountHandlers } from './handlers/account'
import { userHandlers } from './handlers/system/user'
import { deptHandlers } from './handlers/system/dept'
import { roleHandlers } from './handlers/system/role'
import { menuHandlers } from './handlers/system/menu'
import { postHandlers } from './handlers/system/post'
import { dictHandlers } from './handlers/system/dict'
import { monitorHandlers } from './handlers/monitor'
import { toolHandlers } from './handlers/tool'

export const worker = setupWorker(
  ...accountHandlers,
  ...userHandlers,
  ...deptHandlers,
  ...roleHandlers,
  ...menuHandlers,
  ...postHandlers,
  ...dictHandlers,
  ...monitorHandlers,
  ...toolHandlers,
)
