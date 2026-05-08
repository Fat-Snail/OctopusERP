import * as signalR from '@microsoft/signalr'

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5001'

let connection: signalR.HubConnection | null = null

export function getOnlineUserHub(): signalR.HubConnection {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(`${BASE_URL}/hubs/online`, {
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connection.onclose(() => {
      connection = null
    })
  }
  return connection
}

export async function startOnlineUserHub(): Promise<signalR.HubConnection> {
  const hub = getOnlineUserHub()
  if (hub.state === signalR.HubConnectionState.Disconnected) {
    await hub.start()
  }
  return hub
}

export async function stopOnlineUserHub(): Promise<void> {
  if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
    await connection.stop()
  }
  connection = null
}
