-- ═══════════════════════════════════════════════════════════════════════
-- 20261020_003 — MES 供应商存量脱敏
--
-- 种子曾直接写入真实企业名、企业域名邮箱、疑似真实的统一社会信用代码
-- 与联系人姓名（例如知名代工厂及其采购对接人）。
--
-- 本脚本**只按 SupplierId 定位**，不含任何原始值。
-- 税号一律写成 91330100MA0000000N 形式，属于不可能真实注册的占位号。
-- 幂等。
-- ═══════════════════════════════════════════════════════════════════════

UPDATE mes_supplier SET SupplierName = '示例供应商1', ContactName = '联系人1', Email = 'supplier1@example.invalid', Address = '示例省示例市示例区', TaxNumber = '91330100MA00000001' WHERE SupplierId = 1;
UPDATE mes_supplier SET SupplierName = '示例供应商2', ContactName = '联系人2', Email = 'supplier2@example.invalid', Address = '示例省示例市示例区', TaxNumber = '91330100MA00000002' WHERE SupplierId = 2;
UPDATE mes_supplier SET SupplierName = '示例供应商3', ContactName = '联系人3', Email = 'supplier3@example.invalid', Address = '示例省示例市示例区', TaxNumber = '91330100MA00000003' WHERE SupplierId = 3;
UPDATE mes_supplier SET SupplierName = '示例供应商4', ContactName = '联系人4', Email = 'supplier4@example.invalid', Address = '示例省示例市示例区', TaxNumber = '91330100MA00000004' WHERE SupplierId = 4;
