import type { FlowGraph, FlowGraphNode } from '@/api/flow/types'

export const NODE_W = 150
export const NODE_H = 54
export const LAYER_GAP = 92
export const NODE_GAP = 44
export const PADDING = 60

export interface LayoutNode extends FlowGraphNode {
  x: number
  y: number
}

export interface LayoutResult {
  nodes: LayoutNode[]
  width: number
  height: number
}

/**
 * 分层布局：按拓扑深度自上而下分层，同层横向均分。
 * 设计器与详情页预览共用，保证两边画法一致。
 */
export function layoutGraph(graph: FlowGraph): LayoutResult {
  const nodes = graph.nodes ?? []
  const edges = graph.edges ?? []
  if (!nodes.length) return { nodes: [], width: 1200, height: 700 }

  const idSet = new Set(nodes.map(n => n.id))
  const out = new Map<string, string[]>()
  const inDeg = new Map<string, number>()
  for (const n of nodes) { out.set(n.id, []); inDeg.set(n.id, 0) }
  for (const e of edges) {
    if (!idSet.has(e.from) || !idSet.has(e.to)) continue
    out.get(e.from)!.push(e.to)
    inDeg.set(e.to, (inDeg.get(e.to) ?? 0) + 1)
  }

  const depth = new Map<string, number>()
  const starts = nodes.filter(n => (inDeg.get(n.id) ?? 0) === 0)
  const queue: string[] = []
  for (const s of (starts.length ? starts : [nodes[0]])) { depth.set(s.id, 0); queue.push(s.id) }
  let guard = 0
  const cap = nodes.length * nodes.length
  while (queue.length && guard++ < cap) {
    const u = queue.shift()!
    const du = depth.get(u) ?? 0
    for (const v of out.get(u) ?? []) {
      const cur = depth.get(v)
      if (cur === undefined || du + 1 > cur) {
        depth.set(v, du + 1)
        queue.push(v)
      }
    }
  }
  for (const n of nodes) if (!depth.has(n.id)) depth.set(n.id, 0)

  const layers = new Map<number, string[]>()
  for (const n of nodes) {
    const d = depth.get(n.id) ?? 0
    if (!layers.has(d)) layers.set(d, [])
    layers.get(d)!.push(n.id)
  }

  let maxCount = 1
  let maxDepth = 0
  for (const [d, ids] of layers) {
    maxCount = Math.max(maxCount, ids.length)
    maxDepth = Math.max(maxDepth, d)
  }

  const layerWidth = maxCount * NODE_W + (maxCount - 1) * NODE_GAP
  const centerX = PADDING + layerWidth / 2
  const byId = new Map(nodes.map(n => [n.id, n]))
  const positioned: LayoutNode[] = []

  for (const d of [...layers.keys()].sort((a, b) => a - b)) {
    const ids = layers.get(d)!
    const total = ids.length * NODE_W + (ids.length - 1) * NODE_GAP
    const startX = centerX - total / 2
    ids.forEach((id, i) => {
      const n = byId.get(id)!
      positioned.push({
        ...n,
        x: Math.round(startX + i * (NODE_W + NODE_GAP)),
        y: Math.round(PADDING + d * (NODE_H + LAYER_GAP)),
      })
    })
  }

  return {
    nodes: positioned,
    width: Math.max(1200, Math.ceil(PADDING * 2 + layerWidth)),
    height: Math.max(700, Math.ceil(PADDING * 2 + (maxDepth + 1) * (NODE_H + LAYER_GAP))),
  }
}

/** 自下而上（源节点底部 → 目标节点顶部）的贝塞尔连线；跨层长边向右绕行 */
export function edgePath(s: { x: number; y: number }, t: { x: number; y: number }): string {
  const sx = s.x + NODE_W / 2
  const sy = s.y + NODE_H
  const tx = t.x + NODE_W / 2
  const ty = t.y
  const layerSpan = Math.round((ty - sy) / (NODE_H + LAYER_GAP))
  if (layerSpan > 1) {
    const off = 150
    return `M ${sx} ${sy} C ${sx + off} ${sy + 40}, ${tx + off} ${ty - 40}, ${tx} ${ty}`
  }
  const dy = Math.max(24, Math.abs(ty - sy) / 2)
  return `M ${sx} ${sy} C ${sx} ${sy + dy}, ${tx} ${ty - dy}, ${tx} ${ty}`
}
