namespace OctopusPLM.Core.Entities;

/// <summary>商品类目（N 级树）</summary>
public class Category
{
    public long CategoryId { get; set; }
    public long? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Path { get; set; }
    public int Level { get; set; }
    public int OrderNum { get; set; }
    public string? Icon { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    public Category? Parent { get; set; }
    public List<Category> Children { get; set; } = new();
    public List<CategoryAttribute> CategoryAttributes { get; set; } = new();
}
