-- ═══════════════════════════════════════════════════════════════════════
-- 20261020_001 — CRM 客户与联系人存量脱敏
--
-- 种子曾写入真实企业名、企业域名邮箱与联系人姓名（如知名互联网/科技公司）。
-- 本脚本只按主键定位，不含任何原始值；标题类字段统一按主键重新编号。
-- 幂等。
-- ═══════════════════════════════════════════════════════════════════════

-- ── 客户 ────────────────────────────────────────────────────────────
UPDATE crm_customer SET CustomerName = '示例客户1', Address = '示例省示例市示例区示例路1号', Website = 'https://example.invalid/customer1' WHERE CustomerId = 1;
UPDATE crm_customer SET CustomerName = '示例客户2', Address = '示例省示例市示例区示例路2号', Website = 'https://example.invalid/customer2' WHERE CustomerId = 2;
UPDATE crm_customer SET CustomerName = '示例客户3', Address = '示例省示例市示例区示例路3号', Website = 'https://example.invalid/customer3' WHERE CustomerId = 3;
UPDATE crm_customer SET CustomerName = '示例客户4', Address = '示例省示例市示例区示例路4号', Website = 'https://example.invalid/customer4' WHERE CustomerId = 4;
UPDATE crm_customer SET CustomerName = '示例客户5', Address = '示例省示例市示例区示例路5号', Website = 'https://example.invalid/customer5' WHERE CustomerId = 5;

-- ── 联系人 ──────────────────────────────────────────────────────────
UPDATE crm_contact SET Name = '联系人A', Email = 'contacta@example.invalid' WHERE ContactId = 1;
UPDATE crm_contact SET Name = '联系人B', Email = 'contactb@example.invalid' WHERE ContactId = 2;
UPDATE crm_contact SET Name = '联系人C', Email = 'contactc@example.invalid' WHERE ContactId = 3;
UPDATE crm_contact SET Name = '联系人D', Email = 'contactd@example.invalid' WHERE ContactId = 4;
UPDATE crm_contact SET Name = '联系人E', Email = 'contacte@example.invalid' WHERE ContactId = 5;
UPDATE crm_contact SET Name = '联系人F', Email = 'contactf@example.invalid' WHERE ContactId = 6;

-- ── 业务单据标题：按主键重编号，断开与原内容的关联 ──────────────────
UPDATE crm_inquiry  SET Title  = '示例采购询盘' || printf('%02d', InquiryId)  WHERE Title  IS NOT NULL AND TRIM(Title)  <> '';
UPDATE crm_contract SET Title  = '示例采购合同' || printf('%02d', ContractId) WHERE Title  IS NOT NULL AND TRIM(Title)  <> '';
UPDATE crm_quote    SET Remark = '示例报价备注' || printf('%02d', QuoteId)    WHERE Remark IS NOT NULL AND TRIM(Remark) <> '';

-- ── 服装客户（18 家，按 CustomerCode 定位）─────────────────────────
UPDATE crm_customer SET CustomerName = '服装客户01', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-001';
UPDATE crm_customer SET CustomerName = '服装客户02', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-002';
UPDATE crm_customer SET CustomerName = '服装客户03', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-003';
UPDATE crm_customer SET CustomerName = '服装客户04', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-004';
UPDATE crm_customer SET CustomerName = '服装客户05', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-005';
UPDATE crm_customer SET CustomerName = '服装客户06', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-006';
UPDATE crm_customer SET CustomerName = '服装客户07', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-007';
UPDATE crm_customer SET CustomerName = '服装客户08', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-008';
UPDATE crm_customer SET CustomerName = '服装客户09', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-009';
UPDATE crm_customer SET CustomerName = '服装客户10', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-010';
UPDATE crm_customer SET CustomerName = '服装客户11', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-011';
UPDATE crm_customer SET CustomerName = '服装客户12', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-012';
UPDATE crm_customer SET CustomerName = '服装客户13', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-013';
UPDATE crm_customer SET CustomerName = '服装客户14', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-014';
UPDATE crm_customer SET CustomerName = '服装客户15', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-015';
UPDATE crm_customer SET CustomerName = '服装客户16', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-016';
UPDATE crm_customer SET CustomerName = '服装客户17', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-017';
UPDATE crm_customer SET CustomerName = '服装客户18', Address = '示例省示例市示例区' WHERE CustomerCode = 'CUS-GARM-018';

-- ── 询盘明细备注 ──────────────────────────────────────────────────
UPDATE crm_inquiry_item SET Remark = '示例备注' || printf('%02d', ItemId) WHERE Remark IS NOT NULL AND TRIM(Remark) <> '';
