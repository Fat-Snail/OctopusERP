-- ═══════════════════════════════════════════════════════════════════════
-- 20261020_001 — MES 存量真实姓名匿名化
--
-- 背景：`mes_sync_user` 与 `mes_garment_team` 早期直接写入了 6 位真实员工的
-- 姓名、拼音账号与邮箱（来源 seed_garment_22ss.json）。这些会出现在演示、
-- 截图与测试库中，属于个人信息泄露。
--
-- 方案：改为「worker{UmcUserId} / 员工{UmcUserId}」形式的匿名工号。
--
-- 本脚本**只按 UmcUserId / LeaderUmcUserId 匹配，不包含任何真实姓名或拼音账号**——
-- 仓库里因此不需要残留 PII，迁移依然能精确定位到目标行。
--
-- 幂等：重复执行结果一致。
-- 由 MesDataMigrationRunner 在启动时执行一次，并记入 mes_data_migration。
-- ═══════════════════════════════════════════════════════════════════════

-- ── 1. 同步用户缓存 ─────────────────────────────────────────────────
UPDATE mes_sync_user SET UserName = 'worker100', NickName = '员工100', Email = 'worker100@example.invalid' WHERE UmcUserId = 100;
UPDATE mes_sync_user SET UserName = 'worker101', NickName = '员工101', Email = 'worker101@example.invalid' WHERE UmcUserId = 101;
UPDATE mes_sync_user SET UserName = 'worker102', NickName = '员工102', Email = 'worker102@example.invalid' WHERE UmcUserId = 102;
UPDATE mes_sync_user SET UserName = 'worker103', NickName = '员工103', Email = 'worker103@example.invalid' WHERE UmcUserId = 103;
UPDATE mes_sync_user SET UserName = 'worker104', NickName = '员工104', Email = 'worker104@example.invalid' WHERE UmcUserId = 104;
UPDATE mes_sync_user SET UserName = 'worker105', NickName = '员工105', Email = 'worker105@example.invalid' WHERE UmcUserId = 105;

-- ── 2. 服装组：组长与组名（用 GroupLabel 拼装，无需知道原姓名）──────
UPDATE mes_garment_team
   SET Leader   = '员工' || LeaderUmcUserId,
       TeamName = GroupLabel || '-员工' || LeaderUmcUserId
 WHERE LeaderUmcUserId IN (100, 101, 102, 103, 104, 105);

-- ── 3. 服装组：L 组的团队代码里含组长姓名 ────────────────────────────
-- mes_garment_schedule 引用的是 TeamId，改 TeamCode 安全
UPDATE mes_garment_team
   SET TeamCode = 'L-' || LeaderUmcUserId
 WHERE LeaderUmcUserId IN (104, 105) AND TeamCode LIKE 'L-%';
