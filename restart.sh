#!/usr/bin/env bash
# OctopusERP — 一键重启脚本
# 用法：bash restart.sh

set -uo pipefail

kill_port() {
  local port=$1
  local pids
  pids=$(lsof -ti ":$port" 2>/dev/null || true)
  if [ -n "$pids" ]; then
    echo "    kill :$port → PID $(echo $pids | tr '\n' ' ')"
    echo "$pids" | xargs kill -9 2>/dev/null || true
  fi
}

# ── 第一步：先杀 Aspire 编排进程，阻止它重启子进程 ───────────────
echo "🛑  停止 Aspire 编排层..."
pkill -9 -f "OctopusAspire.AppHost" 2>/dev/null || true
pkill -9 -f "\.dcp/dcp "            2>/dev/null || true
pkill -9 -f "/dcp "                 2>/dev/null || true

sleep 2   # 等 DCP 完全退出，不再拉起新进程

# ── 第二步：清理所有子进程 ─────────────────────────────────────
echo "🛑  停止所有子服务..."
pkill -9 -f "OctopusUMC.Api"  2>/dev/null || true
pkill -9 -f "OctopusOA.Api"   2>/dev/null || true
pkill -9 -f "OctopusPLM.Api"  2>/dev/null || true
pkill -9 -f "OctopusCRM.Api"  2>/dev/null || true
pkill -9 -f "vite"            2>/dev/null || true

sleep 1

# ── 第三步：按端口强清（兜底） ────────────────────────────────
echo "🛑  释放端口..."
for port in 5001 5002 5003 5004 5173 5174 5175 5176 15256 20228 19240 19241; do
  kill_port "$port"
done

sleep 2   # 等内核释放端口

echo ""
echo "✅  清理完毕"

# ── 第四步：检查 PLM 图搜模型（缺失时提示，不阻塞启动）──────────
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
MODEL_FILE="$SCRIPT_DIR/OctopusPLM/src/OctopusPLM.Api/Models/clip_vision.onnx"
if [ ! -f "$MODEL_FILE" ]; then
  echo ""
  echo "⚠️   CLIP 模型未下载，「以图搜商品」功能暂不可用。"
  echo "    启动后可在 PLM 前端 → 系统 → 模型管理 页面一键下载，"
  echo "    或手动执行（国内推荐镜像源）："
  echo "    bash OctopusPLM/scripts/download-models.sh --mirror"
fi

echo ""
echo "🚀  正在启动 Aspire..."
echo ""

cd "$SCRIPT_DIR/aspire/OctopusAspire.AppHost"
dotnet run
