namespace OctopusUMC.Api.DTOs;

/// <summary>统一 API 响应包装</summary>
public class ApiResponse<T>
{
    /// <summary>状态码，200=成功</summary>
    public int Code { get; set; }
    /// <summary>提示信息</summary>
    public string Msg { get; set; } = string.Empty;
    /// <summary>响应数据</summary>
    public T? Data { get; set; }

    public static ApiResponse<T> Success(T data, string msg = "操作成功") =>
        new() { Code = 200, Msg = msg, Data = data };

    public static ApiResponse<T> Fail(string msg, int code = 500) =>
        new() { Code = code, Msg = msg, Data = default };
}

/// <summary>分页结果</summary>
public class PagedResult<T>
{
    /// <summary>当前页数据行</summary>
    public List<T> Rows { get; set; } = new();
    /// <summary>总记录数</summary>
    public int Total { get; set; }
}
