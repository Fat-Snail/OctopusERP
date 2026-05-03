namespace OctopusPLM.Core.Entities;

/// <summary>详情模板与组件绑定关系</summary>
public class DetailComponentBind
{
    public long BindId { get; set; }
    public long TemplateId { get; set; }
    public long ComponentId { get; set; }
    public int OrderNum { get; set; }
    public bool IsRequired { get; set; }
    public string? DisplayRuleJson { get; set; }
    public string? DefaultContentJson { get; set; }

    public DetailTemplate? Template { get; set; }
    public DetailComponentDef? Component { get; set; }
}
