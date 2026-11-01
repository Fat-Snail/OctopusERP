namespace OctopusRetail.Api.Dtos;

/// <summary>统一响应：{ code, msg, data }</summary>
public class ApiResponse<T>
{
    public int Code { get; set; }
    public string Msg { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ApiResponse<T> Success(T data, string msg = "操作成功") =>
        new() { Code = 200, Msg = msg, Data = data };

    public static ApiResponse<T> Fail(string msg, int code = 500) =>
        new() { Code = code, Msg = msg };
}
