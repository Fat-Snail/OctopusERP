using System.Text.Json;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services.PlmSync;

public interface IPlmSyncAlertSink
{
    void Notify(MesPlmProductSyncRecord record, int failedAttempts);
}

public sealed class PlmSyncAlertSink(WebhookSender webhook, IConfiguration configuration) : IPlmSyncAlertSink
{
    public void Notify(MesPlmProductSyncRecord record, int failedAttempts)
    {
        var userId = configuration.GetValue<long?>("Plm:AlertUmcUserId") ?? 1;
        webhook.PostToUmcNotify(userId, "mes", "plm_sync_alert", "PLM 制造数据同步异常",
            $"商品 {record.PlmProductId} / 制造版本 {record.PlmManufacturingVersionId}：{record.ErrorMessage}",
            "/plm-sync",
            JsonSerializer.Serialize(new
            {
                record.SyncRecordId,
                record.PlmProductId,
                record.PlmManufacturingVersionId,
                record.Status,
                failedAttempts
            }));
    }
}
