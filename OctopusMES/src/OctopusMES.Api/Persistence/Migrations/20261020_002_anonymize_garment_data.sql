-- ═══════════════════════════════════════════════════════════════════════
-- 20261020_002 — 服装排产存量数据脱敏
--
-- 背景：seed_garment_22ss.json 来自真实工厂导出，含 430 个真实客户/品牌、
-- 真实款号与订单号、外协厂名，以及个人姓名（如客户字段里的联系人）。
-- 这些属于商业机密与个人信息，不能留在仓库或演示库里。
--
-- 本脚本**不包含任何原始值**：
--   - 客户：在库内对 DISTINCT 值排序后统一编号为「客户NNN」
--   - 款号/订单号：按 ScheduleId 重新编号
--   - 外协组：按 TeamId 编号为「外协NN」
-- 因此即便脚本文本被公开，也推不出原始客户是谁。
--
-- 幂等：重复执行结果一致（再次执行时 DISTINCT 集合已是合成名，编号不变）。
-- ═══════════════════════════════════════════════════════════════════════

-- ── 1. 客户名称 ─────────────────────────────────────────────────────
CREATE TEMP TABLE _garment_customer_map AS
SELECT Customer AS old_name,
       '客户' || printf('%03d', ROW_NUMBER() OVER (ORDER BY Customer)) AS new_name
  FROM (SELECT DISTINCT Customer FROM mes_garment_schedule
         WHERE Customer IS NOT NULL AND TRIM(Customer) <> '');

UPDATE mes_garment_schedule
   SET Customer = (SELECT new_name FROM _garment_customer_map
                    WHERE old_name = mes_garment_schedule.Customer)
 WHERE Customer IS NOT NULL AND TRIM(Customer) <> '';

DROP TABLE _garment_customer_map;

-- ── 2. 款号 / 订单号（按主键重编号，彻底断开与原值的关系）──────────
UPDATE mes_garment_schedule SET StyleCode = 'SYN' || printf('%05d', ScheduleId)
 WHERE StyleCode IS NOT NULL AND TRIM(StyleCode) <> '';

UPDATE mes_garment_schedule SET OrderNo = 'SO' || printf('%05d', ScheduleId)
 WHERE OrderNo IS NOT NULL AND TRIM(OrderNo) <> '';

-- ── 3. 外协组：真实厂名 → 外协NN ────────────────────────────────────
UPDATE mes_garment_team
   SET TeamCode = 'L-9' || substr('0' || TeamId, -2),
       Leader   = '外协' || substr('0' || TeamId, -2),
       TeamName = 'L组-外协' || substr('0' || TeamId, -2)
 WHERE IsOutsource = 1;
