-- ═══════════════════════════════════════════════════════════════════════
-- 20261020_001 — UMC 存量真实姓名匿名化
--
-- 背景：`sys_user` 与 `sys_dept` 早期直接写入了 6 位真实员工的姓名、拼音账号
-- 与邮箱，并且 MES 的 `mes_sync_user` 又复制了一份。这些会出现在 5173 的
-- 用户管理、演示与截图中，属于个人信息泄露。
--
-- 方案：改为「worker{UserId} / 员工{UserId}」形式的匿名工号。
--
-- 本脚本**只按 UserId / DeptId 匹配，不包含任何真实姓名或拼音账号**——
-- 这样仓库里就不需要残留 PII，迁移依然能精确定位到目标行。
--
-- 幂等：重复执行结果一致。
-- 由 DataMigrationRunner 在启动时执行一次，并记入 sys_data_migration。
-- ═══════════════════════════════════════════════════════════════════════

-- ── 1. 用户：100-105 ────────────────────────────────────────────────
UPDATE sys_user SET UserName = 'worker100', NickName = '员工100', Email = 'worker100@example.invalid' WHERE UserId = 100;
UPDATE sys_user SET UserName = 'worker101', NickName = '员工101', Email = 'worker101@example.invalid' WHERE UserId = 101;
UPDATE sys_user SET UserName = 'worker102', NickName = '员工102', Email = 'worker102@example.invalid' WHERE UserId = 102;
UPDATE sys_user SET UserName = 'worker103', NickName = '员工103', Email = 'worker103@example.invalid' WHERE UserId = 103;
UPDATE sys_user SET UserName = 'worker104', NickName = '员工104', Email = 'worker104@example.invalid' WHERE UserId = 104;
UPDATE sys_user SET UserName = 'worker105', NickName = '员工105', Email = 'worker105@example.invalid' WHERE UserId = 105;

-- ── 2. 部门：L 组下的两个子组（名字里含组长姓名）─────────────────────
UPDATE sys_dept SET DeptName = 'L组-104' WHERE DeptId = 106;
UPDATE sys_dept SET DeptName = 'L组-105' WHERE DeptId = 107;
