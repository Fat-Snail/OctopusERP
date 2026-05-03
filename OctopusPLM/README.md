# OctopusPLM — 部署与测试指南

商品生命周期管理系统（PLM），包含以图搜商品（CLIP 视觉向量搜索）功能。

---

## 目录

- [环境要求](#环境要求)
- [快速启动（开发）](#快速启动开发)
- [以图搜商品：环境搭建](#以图搜商品环境搭建)
  - [1. 下载 CLIP ONNX 模型](#1-下载-clip-onnx-模型)
  - [2. 部署 Qdrant 向量数据库](#2-部署-qdrant-向量数据库)
- [配置说明](#配置说明)
- [构建与运行](#构建与运行)
- [测试以图搜商品](#测试以图搜商品)
- [生产部署注意事项](#生产部署注意事项)

---

## 环境要求

| 依赖 | 版本 | 用途 |
|---|---|---|
| .NET SDK | 10.0+ | 后端运行时 |
| Node.js | 18+ | 前端构建 |
| npm | 9+ | 前端包管理 |
| Docker | 任意 | 运行 Qdrant（可选，也可二进制启动） |

> **以图搜商品**额外需要：Docker 或 Qdrant 二进制（向量数据库）。
> 无需安装 Python、Ollama 或任何 AI 框架——CLIP 模型以 ONNX 格式直接在 .NET 运行时内推理。

---

## 快速启动（开发）

```bash
# 1. 克隆仓库（在父目录 test-cc-umc 下）
git clone <repo-url>

# 2. 进入 PLM 后端
cd OctopusPLM

# 3. 还原依赖并启动（数据库自动创建 + 种子数据自动填充）
cd src/OctopusPLM.Api
dotnet run
# API 默认监听 http://localhost:5003
# Swagger UI: http://localhost:5003/swagger

# 4. 启动前端（另开终端）
cd ../../web/OctopusPLM.Web
npm install
npm run dev
# 前端默认监听 http://localhost:5175

# 或通过 .NET Aspire 一键启动所有服务（推荐）
cd ../../aspire/OctopusAspire.AppHost
dotnet run
```

---

## 以图搜商品：环境搭建

以图搜商品基于 **CLIP ViT-B/32**（ONNX 格式）+ **Qdrant** 向量数据库。  
首次使用需完成以下两步：

### 1. 下载 CLIP ONNX 模型

模型文件需放置在 `src/OctopusPLM.Api/Models/clip_vision.onnx`。

**方式 A：命令行下载（推荐）**

```bash
cd OctopusPLM/src/OctopusPLM.Api
mkdir -p Models

# 量化版（约 59 MB，推理速度快）
curl -L \
  "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model_quantized.onnx" \
  -o Models/clip_vision.onnx

# 如果 HuggingFace 下载慢，可使用镜像站
curl -L \
  "https://hf-mirror.com/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model_quantized.onnx" \
  -o Models/clip_vision.onnx
```

**方式 B：浏览器下载**

访问以下地址手动下载，保存为 `Models/clip_vision.onnx`：
```
https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model_quantized.onnx
```

**验证下载**

```bash
ls -lh Models/clip_vision.onnx
# 预期输出：约 59M
```

> **关于模型版本选择：**
> - `vision_model_quantized.onnx`（59 MB）：int8 量化，速度最快，精度略低，**推荐用于开发和生产**
> - `vision_model.onnx`（约 170 MB）：fp32 全精度，精度最高，适合对精度要求高的场景
>
> 两个版本均支持 Apple Silicon CoreML 加速（M1/M2/M3/M4 系列芯片自动启用 Neural Engine）。

---

### 2. 部署 Qdrant 向量数据库

Qdrant 负责存储商品的视觉向量并提供高性能相似度搜索。

**方式 A：Docker（推荐）**

```bash
# 启动 Qdrant，数据持久化到本地 ./qdrant_data 目录
docker run -d \
  --name qdrant \
  --restart unless-stopped \
  -p 6333:6333 \
  -p 6334:6334 \
  -v "$(pwd)/qdrant_data:/qdrant/storage" \
  qdrant/qdrant:latest

# 验证启动
curl http://localhost:6333/healthz
# 预期输出：{"title":"qdrant - vector search engine","version":"..."}
```

**方式 B：Docker Compose**

在项目根目录创建或追加到 `docker-compose.yml`：

```yaml
services:
  qdrant:
    image: qdrant/qdrant:latest
    container_name: qdrant
    restart: unless-stopped
    ports:
      - "6333:6333"   # REST API / Dashboard
      - "6334:6334"   # gRPC（.NET 客户端使用）
    volumes:
      - qdrant_data:/qdrant/storage

volumes:
  qdrant_data:
```

```bash
docker compose up -d qdrant
```

**方式 C：二进制（无 Docker）**

```bash
# macOS Apple Silicon
curl -L https://github.com/qdrant/qdrant/releases/latest/download/qdrant-aarch64-apple-darwin.tar.gz \
  | tar -xz
./qdrant
```

**Qdrant Dashboard**

Qdrant 自带 Web UI，启动后访问：
```
http://localhost:6333/dashboard
```

可在此查看 collection（`plm_products`）中的向量数量和索引状态。

---

## 配置说明

`src/OctopusPLM.Api/appsettings.json`：

```json
{
  "Vector": {
    "QdrantHost": "localhost",
    "QdrantPort": "6334"
  }
}
```

| 配置项 | 默认值 | 说明 |
|---|---|---|
| `Vector:QdrantHost` | `localhost` | Qdrant 主机地址 |
| `Vector:QdrantPort` | `6334` | Qdrant gRPC 端口（非 REST 6333） |

生产环境通过环境变量覆盖：

```bash
export Vector__QdrantHost=qdrant.internal
export Vector__QdrantPort=6334
```

---

## 构建与运行

```bash
# 后端构建
cd OctopusPLM
dotnet build

# 运行测试
dotnet test

# 发布（生产构建）
dotnet publish src/OctopusPLM.Api/OctopusPLM.Api.csproj \
  -c Release -o ./publish
# 注意：Models/clip_vision.onnx 会自动复制到发布目录
```

---

## 测试以图搜商品

### 步骤一：为商品建立向量索引

首次运行或新增商品后，需要触发向量化（将商品主图转换为 CLIP 向量存入 Qdrant）。

**批量向量化所有商品：**

```bash
curl -X POST http://localhost:5003/api/product/vectorize-all
```

预期返回：
```json
{
  "code": 200,
  "msg": "批量向量化完成：12 成功 / 0 失败",
  "data": { "total": 12, "successCount": 12, "failCount": 0 }
}
```

**向量化单个商品：**

```bash
curl -X POST http://localhost:5003/api/product/{商品ID}/vectorize
```

> **说明：** 向量化需要商品有主图 URL（`mainImage` 字段不为空）。  
> API 会下载图片 → 运行 CLIP 推理（约 50–200ms）→ 存入 Qdrant。

---

### 步骤二：前端页面测试

1. 打开 PLM 前端：http://localhost:5175
2. 登录后进入「商品列表」
3. 点击右上角「以图搜商品」按钮
4. 上传任意商品图片（支持拖拽）
5. 点击「搜索相似商品」

搜索速度约 0.2–1 秒（含网络传输）。

---

### 步骤三：API 直接测试

```bash
# 准备一张测试图片
# 用 curl 调用以图搜商品接口
curl -X POST "http://localhost:5003/api/product/search/image?limit=5" \
  -F "image=@/path/to/your/image.jpg"
```

预期返回：
```json
{
  "code": 200,
  "msg": "ok",
  "data": {
    "queryDescription": "",
    "items": [
      {
        "score": 0.923,
        "imageDescription": "",
        "product": {
          "productId": 1,
          "productName": "iPhone 15 Pro",
          "categoryName": "手机",
          "minPrice": 7999,
          "maxPrice": 9999,
          "totalStock": 100
        }
      }
    ]
  }
}
```

**分数说明：**

| 分数范围 | 含义 | 颜色标记 |
|---|---|---|
| 0.6 以上 | 高度相似 | 绿色 |
| 0.35–0.60 | 较为相似 | 蓝色 |
| 0.20–0.35 | 低度相似 | 黄色 |
| 0.20 以下 | 已过滤，不返回 | — |

---

## 技术架构

```
用户上传图片
     │
     ▼
OctopusPLM.Api（.NET 10）
     │
     ├─ SixLabors.ImageSharp
     │   └─ Resize 到 224×224，归一化（CLIP 标准参数）
     │
     ├─ Microsoft.ML.OnnxRuntime
     │   ├─ 加载 clip_vision.onnx（CLIP ViT-B/32，量化版 59MB）
     │   ├─ CoreML EP（Apple Silicon 自动启用 Neural Engine 加速）
     │   └─ 推理输出：512 维 float32 向量
     │
     └─ Qdrant.Client（gRPC）
         ├─ Collection：plm_products（余弦相似度，512 维）
         └─ 搜索：最近邻查询，阈值 0.20
```

**与旧方案对比：**

| | 旧方案 | 新方案（当前）|
|---|---|---|
| 搜索耗时 | 10–30 秒 | 0.2–1 秒 |
| AI 模型 | gemma4:e4b（8B LLM）+ nomic-embed-text | CLIP ViT-B/32（视觉编码器）|
| 中间步骤 | 图片 → 文字描述 → 文字向量 | 图片 → 视觉向量（端到端）|
| 额外依赖 | Ollama（需常驻进程）| 无（纯 .NET 推理）|
| 模型大小 | ~5 GB（LLM）| 59 MB（ONNX 量化）|

---

## 生产部署注意事项

### 1. CLIP 模型文件

`clip_vision.onnx` 体积较大（59 MB），建议：
- 加入 `.gitignore`，不提交到 Git
- 通过 CI/CD 脚本在构建阶段自动下载
- 或存放在内网文件服务器，拉取脚本写入 `Makefile`

`.gitignore` 追加：
```
**/Models/clip_vision.onnx
```

CI/CD 下载脚本示例：
```bash
#!/bin/bash
# scripts/download-models.sh
set -e
MODEL_DIR="OctopusPLM/src/OctopusPLM.Api/Models"
mkdir -p "$MODEL_DIR"

if [ ! -f "$MODEL_DIR/clip_vision.onnx" ]; then
  echo "下载 CLIP ONNX 模型..."
  curl -L \
    "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model_quantized.onnx" \
    -o "$MODEL_DIR/clip_vision.onnx"
  echo "完成：$(du -h "$MODEL_DIR/clip_vision.onnx" | cut -f1)"
else
  echo "CLIP 模型已存在，跳过下载"
fi
```

### 2. Qdrant 持久化

生产环境必须挂载数据卷：

```bash
docker run -d \
  --name qdrant \
  -p 6334:6334 \
  -v /data/qdrant:/qdrant/storage \  # 宿主机持久化路径
  qdrant/qdrant:latest
```

数据存储路径包含向量索引，**不挂载则重启后向量数据丢失**，需重新向量化。

### 3. 重新向量化场景

以下情况需重新调用 `/api/product/vectorize-all`：
- 首次部署
- Qdrant 数据丢失（未持久化）
- 商品主图批量更换

### 4. Apple Silicon vs x86 服务器

| 平台 | ONNX 执行引擎 | 单图推理时间 |
|---|---|---|
| Apple Silicon（M系列）| CoreML（Neural Engine）| ~50–100 ms |
| Linux x86-64（无 GPU）| CPU（MLAS）| ~150–400 ms |
| Linux x86-64（NVIDIA GPU）| CUDA EP | ~10–30 ms |

Linux 服务器启用 CUDA 加速：安装 `Microsoft.ML.OnnxRuntime.Gpu` 替换 `Microsoft.ML.OnnxRuntime`，其余代码不变。
