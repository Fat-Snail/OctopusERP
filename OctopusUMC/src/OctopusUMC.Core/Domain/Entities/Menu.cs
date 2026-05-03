namespace OctopusUMC.Core.Domain.Entities;

public class Menu
{
    public long MenuId { get; set; }
    public long ParentId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public string MenuType { get; set; } = "C"; // M=目录 C=菜单 F=按钮
    public string Path { get; set; } = string.Empty;
    public string? Component { get; set; }
    public string? Permission { get; set; }
    public string? Icon { get; set; }
    public int OrderNum { get; set; }
    public int Status { get; set; } = 1;
    public bool IsCache { get; set; }
    public bool IsFrame { get; set; }
    public bool Visible { get; set; } = true;
    public List<Menu> Children { get; set; } = new();
}
