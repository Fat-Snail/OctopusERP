-- ═══════════════════════════════════════════════════════════════════════
-- 20261020_001 — WMS 出入库单据里的真实企业名脱敏
--
-- 种子曾把真实供应商与客户名称、真实发货地址写进入库/出库单。
-- 本脚本只按主键定位，不含原始值。幂等。
-- ═══════════════════════════════════════════════════════════════════════

UPDATE wms_inbound_order SET Supplier = '示例供应商1' WHERE InboundId = 1;
UPDATE wms_inbound_order SET Supplier = '示例供应商2' WHERE InboundId = 2;

UPDATE wms_outbound_order SET Recipient = '示例客户1', ShipAddress = '示例省示例市示例区' WHERE OutboundId = 1;
UPDATE wms_outbound_order SET Recipient = '示例客户2', ShipAddress = '示例省示例市示例区' WHERE OutboundId = 2;
