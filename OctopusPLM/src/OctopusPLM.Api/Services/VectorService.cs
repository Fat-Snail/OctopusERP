using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using OnnxSessionOptions = Microsoft.ML.OnnxRuntime.SessionOptions;
using SharpImage = SixLabors.ImageSharp.Image;

namespace OctopusPLM.Api.Services;

/// <summary>以图搜商品：CLIP ONNX（图片 → 512维向量）→ Qdrant 向量搜索</summary>
public class VectorService : IDisposable
{
    private readonly QdrantClient _qdrant;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<VectorService> _log;
    private readonly InferenceSession _session;
    private readonly string _embedOutputName;

    private const string Collection = "plm_products";
    private const uint VectorDim = 512; // CLIP ViT-B/32

    // CLIP 标准归一化参数
    private static readonly float[] Mean = [0.48145466f, 0.4578275f, 0.40821073f];
    private static readonly float[] Std  = [0.26862954f, 0.26130258f, 0.27577711f];

    public VectorService(IConfiguration config, IHttpClientFactory http, ILogger<VectorService> log)
    {
        _http = http;
        _log = log;
        _qdrant = new QdrantClient(
            host: config["Vector:QdrantHost"] ?? "localhost",
            port: int.Parse(config["Vector:QdrantPort"] ?? "6334"));

        var modelPath = Path.Combine(AppContext.BaseDirectory, "Models", "clip_vision.onnx");
        var options = new OnnxSessionOptions();
        try
        {
            options.AppendExecutionProvider_CoreML();
            _log.LogInformation("CLIP：CoreML EP 已启用（Apple Silicon 加速）");
        }
        catch
        {
            _log.LogInformation("CLIP：CoreML 不可用，使用 CPU 推理");
        }

        _session = new InferenceSession(modelPath, options);

        // 找正确的输出节点名（优先 image_embeds，兜底取第一个）
        _embedOutputName = _session.OutputMetadata.Keys
            .FirstOrDefault(k => k.Contains("embed", StringComparison.OrdinalIgnoreCase))
            ?? _session.OutputMetadata.Keys.First();

        _log.LogInformation("CLIP 模型加载完成，嵌入输出节点：{Name}，维度：{Dim}", _embedOutputName, VectorDim);
    }

    /// <summary>确保 Qdrant collection 存在且维度正确（自动重建）</summary>
    public async Task EnsureCollectionAsync()
    {
        try
        {
            if (await _qdrant.CollectionExistsAsync(Collection))
            {
                var info = await _qdrant.GetCollectionInfoAsync(Collection);
                var existingSize = info.Config?.Params?.VectorsConfig?.Params?.Size ?? 0;
                if (existingSize == VectorDim)
                {
                    _log.LogInformation("Qdrant collection '{Col}' 已存在（维度 {Dim}），跳过创建", Collection, VectorDim);
                    return;
                }
                _log.LogInformation("Qdrant collection 维度不匹配（{Old} → {New}），重建中...", existingSize, VectorDim);
                await _qdrant.DeleteCollectionAsync(Collection);
            }

            await _qdrant.CreateCollectionAsync(Collection, new VectorParams
            {
                Size = VectorDim,
                Distance = Distance.Cosine
            });
            _log.LogInformation("Qdrant collection '{Col}' 创建完成（维度 {Dim}）", Collection, VectorDim);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Qdrant collection 初始化失败，向量搜索不可用");
        }
    }

    /// <summary>下载商品主图 → CLIP 推理 → 存入 Qdrant</summary>
    public async Task<(bool ok, string description)> IndexProductAsync(
        long productId, string imageUrl, string productName)
    {
        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var imageBytes = await client.GetByteArrayAsync(imageUrl);

            var vector = GetImageVector(imageBytes);
            if (vector is null) return (false, "");

            var point = new PointStruct
            {
                Id = (ulong)productId,
                Vectors = vector,
                Payload =
                {
                    ["product_id"]   = (long)productId,
                    ["product_name"] = productName,
                }
            };
            await _qdrant.UpsertAsync(Collection, [point]);
            return (true, "");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "商品 {Id} 向量化失败", productId);
            return (false, "");
        }
    }

    /// <summary>上传图片字节 → CLIP 推理 → Qdrant 搜索</summary>
    public async Task<(List<ImageSearchHit> Hits, string QueryDescription)> SearchByImageAsync(
        byte[] imageBytes, int limit = 10)
    {
        try
        {
            var vector = GetImageVector(imageBytes);
            if (vector is null) return ([], "");

            var hits = await _qdrant.SearchAsync(
                collectionName: Collection,
                vector: vector,
                limit: (ulong)limit,
                payloadSelector: new WithPayloadSelector { Enable = true },
                scoreThreshold: 0.20f); // CLIP 余弦相似度阈值

            var result = hits
                .Select(h => new ImageSearchHit
                {
                    ProductId = h.Id.HasNum ? (long)h.Id.Num : 0,
                    Score = h.Score,
                    ImageDescription = "",
                })
                .Where(h => h.ProductId > 0)
                .ToList();

            return (result, "");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "图片搜索失败");
            return ([], "");
        }
    }

    // ── 推理核心 ──────────────────────────────────────────────────────────────

    private float[]? GetImageVector(byte[] imageBytes)
    {
        try
        {
            var tensor = PreprocessImage(imageBytes);
            var inputs = new[] { NamedOnnxValue.CreateFromTensor("pixel_values", tensor) };
            using var outputs = _session.Run(inputs);

            var embedOutput = outputs.FirstOrDefault(o => o.Name == _embedOutputName)
                ?? outputs.First();

            var raw = embedOutput.AsEnumerable<float>().ToArray();

            // L2 归一化，保证余弦相似度计算正确
            var norm = MathF.Sqrt(raw.Sum(x => x * x));
            if (norm > 0f)
                for (int i = 0; i < raw.Length; i++)
                    raw[i] /= norm;

            return raw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "CLIP 推理失败");
            return null;
        }
    }

    private static DenseTensor<float> PreprocessImage(byte[] imageBytes)
    {
        using var image = SharpImage.Load<Rgb24>(imageBytes);
        image.Mutate(x => x.Resize(224, 224));

        var data = new float[1 * 3 * 224 * 224];
        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 224; x++)
            {
                var p = image[x, y];
                data[0 * 224 * 224 + y * 224 + x] = (p.R / 255f - Mean[0]) / Std[0];
                data[1 * 224 * 224 + y * 224 + x] = (p.G / 255f - Mean[1]) / Std[1];
                data[2 * 224 * 224 + y * 224 + x] = (p.B / 255f - Mean[2]) / Std[2];
            }
        }
        return new DenseTensor<float>(data, [1, 3, 224, 224]);
    }

    public void Dispose() => _session.Dispose();
}

public class ImageSearchHit
{
    public long ProductId { get; set; }
    public float Score { get; set; }
    public string ImageDescription { get; set; } = "";
}
