import { ref, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'
import { oidcService } from '@/services/oidcService'

export interface HubNotification {
  type: string
  message: string
  at: string
}

const OA_API_BASE = 'http://localhost:5002'

export function useNotificationHub(onNotification?: (n: HubNotification) => void) {
  const connected = ref(false)
  let connection: signalR.HubConnection | null = null

  async function start() {
    const token = oidcService.getAccessToken()
    if (!token) return

    connection = new signalR.HubConnectionBuilder()
      .withUrl(`${OA_API_BASE}/hubs/notification`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connection.on('Notification', (payload: HubNotification) => {
      onNotification?.(payload)
    })

    connection.onclose(() => { connected.value = false })
    connection.onreconnected(() => { connected.value = true })

    try {
      await connection.start()
      connected.value = true
    } catch {
      // Hub unavailable (e.g. OA API not running) — fail silently
    }
  }

  async function stop() {
    if (connection) {
      await connection.stop()
      connection = null
      connected.value = false
    }
  }

  onUnmounted(() => { stop() })

  return { connected, start, stop }
}
