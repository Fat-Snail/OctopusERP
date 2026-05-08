#!/bin/bash
# 下载 OctopusPLM 所需的 ONNX 模型文件
# 用法：bash scripts/download-models.sh [--mirror]
#
# --mirror  使用 hf-mirror.com 镜像（国内网络推荐）

set -e

MODEL_DIR="$(cd "$(dirname "$0")/.." && pwd)/src/OctopusPLM.Api/Models"
MODEL_FILE="$MODEL_DIR/clip_vision.onnx"
MODEL_SIZE_MB=82

ORIGIN_URL="https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model.onnx"
MIRROR_URL="https://hf-mirror.com/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model.onnx"

# 解析参数
USE_MIRROR=false
for arg in "$@"; do
  [[ "$arg" == "--mirror" ]] && USE_MIRROR=true
done

DOWNLOAD_URL=$ORIGIN_URL
$USE_MIRROR && DOWNLOAD_URL=$MIRROR_URL

mkdir -p "$MODEL_DIR"

if [ -f "$MODEL_FILE" ]; then
  actual_size=$(du -m "$MODEL_FILE" | cut -f1)
  if [ "$actual_size" -ge "$((MODEL_SIZE_MB - 5))" ]; then
    echo "✓ CLIP ONNX 模型已存在（$(du -h "$MODEL_FILE" | cut -f1)），跳过下载"
    exit 0
  else
    echo "⚠ 模型文件不完整（${actual_size}MB），重新下载..."
    rm -f "$MODEL_FILE"
  fi
fi

echo "下载 CLIP ViT-B/32 ONNX 模型（约 ${MODEL_SIZE_MB}MB）..."
echo "来源：$DOWNLOAD_URL"
echo ""

if command -v curl &>/dev/null; then
  curl -L --progress-bar "$DOWNLOAD_URL" -o "$MODEL_FILE"
elif command -v wget &>/dev/null; then
  wget -q --show-progress "$DOWNLOAD_URL" -O "$MODEL_FILE"
else
  echo "错误：系统中未找到 curl 或 wget，请手动下载："
  echo "  $DOWNLOAD_URL"
  echo "  保存到：$MODEL_FILE"
  exit 1
fi

echo ""
echo "✓ 下载完成：$(du -h "$MODEL_FILE" | cut -f1)"
