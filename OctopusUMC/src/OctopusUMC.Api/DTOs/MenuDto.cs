namespace OctopusUMC.Api.DTOs;

/// <summary>菜单信息响应</summary>
public class MenuResponse
{
    public long MenuId { get; set; }
    public long ParentId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public string MenuType { get; set; } = "C";
    public string Path { get; set; } = string.Empty;
    public string? Component { get; set; }
    public string? Permission { get; set; }
    public string? Icon { get; set; }
    public int OrderNum { get; set; }
    public int Status { get; set; }
    public bool IsCache { get; set; }
    public bool IsFrame { get; set; }
    public bool Visible { get; set; }
    public List<MenuResponse> Children { get; set; } = new();
}

/// <summary>创建菜单请求</summary>
public class CreateMenuRequest
{
    public long ParentId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public string MenuType { get; set; } = "C";
    public string Path { get; set; } = string.Empty;
    public string? Component { get; set; }
    public string? Permission { get; set; }
    public string? Icon { get; set; }
    public int OrderNum { get; set; }
    public int Status { get; set; } = 1;
    public bool IsCache { get; set; }
    public bool IsFrame { get; set; }
    public bool Visible { get; set; } = true;
}

/// <summary>修改菜单请求</summary>
public class UpdateMenuRequest
{
    public long MenuId { get; set; }
    public long ParentId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public string MenuType { get; set; } = "C";
    public string Path { get; set; } = string.Empty;
    public string? Component { get; set; }
    public string? Permission { get; set; }
    public string? Icon { get; set; }
    public int OrderNum { get; set; }
    public int Status { get; set; } = 1;
    public bool IsCache { get; set; }
    public bool IsFrame { get; set; }
    public bool Visible { get; set; } = true;
}
