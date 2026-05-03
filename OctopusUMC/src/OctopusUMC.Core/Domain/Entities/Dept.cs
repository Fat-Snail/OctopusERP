namespace OctopusUMC.Core.Domain.Entities;

public class Dept
{
    public long DeptId { get; set; }
    public long ParentId { get; set; }
    public string DeptName { get; set; } = string.Empty;
    public int OrderNum { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public List<Dept> Children { get; set; } = new();
}
