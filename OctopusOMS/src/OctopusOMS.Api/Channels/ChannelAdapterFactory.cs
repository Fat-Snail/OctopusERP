namespace OctopusOMS.Api.Channels;

/// <summary>渠道不支持（OmsChannel.ChannelCode 没有对应的适配器实现）</summary>
public class ChannelNotSupportedException(string channelCode)
    : Exception($"渠道 '{channelCode}' 尚无适配器实现（开放平台凭证到位后注册对应 IChannelAdapter）")
{
    public string ChannelCode { get; } = channelCode;
}

/// <summary>按 OmsChannel.ChannelCode 解析适配器。真实渠道凭证到位后，新渠道只需注册一个 IChannelAdapter 实现。</summary>
public class ChannelAdapterFactory(IEnumerable<IChannelAdapter> adapters)
{
    private readonly Dictionary<string, IChannelAdapter> _map =
        adapters.ToDictionary(a => a.ChannelCode, StringComparer.OrdinalIgnoreCase);

    public IChannelAdapter GetAdapter(string channelCode)
    {
        if (_map.TryGetValue(channelCode, out var adapter)) return adapter;
        throw new ChannelNotSupportedException(channelCode);
    }

    public bool IsSupported(string channelCode) =>
        _map.ContainsKey(channelCode);

    /// <summary>全部已注册适配器（诊断/Mock 沙箱查询用）</summary>
    public IReadOnlyCollection<IChannelAdapter> GetAll() => _map.Values.ToList();
}
