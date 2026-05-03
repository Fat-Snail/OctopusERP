import { setupWorker } from 'msw/browser'

// OA has minimal mock handlers - auth is handled by oidcService
export const worker = setupWorker()
