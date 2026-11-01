#!/usr/bin/env bash
# ════════════════════════════════════════════════════════════════════════
#  Keycloak 存量用户匿名化（配合 ADR-011）
#
#  背景：UMC 的 6 个真实员工账号曾被同步进 Keycloak
#  （username / email / firstName 都是真实姓名与拼音）。
#  数据库侧已由 DataMigrationRunner 处理，但 Keycloak 是独立存储，
#  必须单独改，否则登录入口仍暴露真实姓名。
#
#  做法：**按 `octopus_user_id` 属性定位**，不依赖真实用户名
#  —— 脚本本身不包含任何 PII（否则清理工具就成了泄漏源）。
#
#  用法：
#    bash scripts/dev-anonymize-keycloak-users.sh
#    DRY_RUN=1 bash scripts/dev-anonymize-keycloak-users.sh
#
#  幂等：已是 worker{id} 的用户会被跳过；重复执行无副作用。
#  ⚠️ 仅用于本地开发/演示环境。
# ════════════════════════════════════════════════════════════════════════
set -uo pipefail

KEYCLOAK_URL="${KEYCLOAK_URL:-https://localhost:8088}"
REALM="${KEYCLOAK_REALM:-octopus}"
ADMIN_USER="${KC_ADMIN_USER:-admin}"
ADMIN_PASS="${KC_ADMIN_PASS:-admin}"
DRY_RUN="${DRY_RUN:-0}"

# 需要匿名化的 UmcUserId（与 UMC 迁移脚本保持一致）
USER_IDS=(100 101 102 103 104 105)

echo "════════════════════════════════════════════════════"
echo "  Keycloak : $KEYCLOAK_URL"
echo "  Realm    : $REALM"
echo "  范围     : UmcUserId ${USER_IDS[*]}"
echo "  模式     : $([ "$DRY_RUN" = "1" ] && echo '演练（不修改）' || echo '实际执行')"
echo "════════════════════════════════════════════════════"

TOKEN=$(curl -sk -X POST "$KEYCLOAK_URL/realms/master/protocol/openid-connect/token" \
  -d grant_type=password -d client_id=admin-cli \
  -d username="$ADMIN_USER" -d password="$ADMIN_PASS" \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('access_token',''))" 2>/dev/null)

if [ -z "$TOKEN" ]; then
  echo "❌ 拿不到 admin token。Keycloak 是否已启动？"
  echo "   校验: curl -sk $KEYCLOAK_URL/realms/$REALM/.well-known/openid-configuration"
  exit 1
fi

# 一次拉全量，避免逐个查询
USERS_JSON=$(curl -sk -H "Authorization: Bearer $TOKEN" \
  "$KEYCLOAK_URL/admin/realms/$REALM/users?max=500&briefRepresentation=false")

ok=0; skipped=0; missing=0; failed=0

for uid in "${USER_IDS[@]}"; do
  NEW_USER="worker${uid}"
  NEW_NICK="员工${uid}"
  NEW_MAIL="worker${uid}@example.invalid"

  # 通过自定义属性 octopus_user_id 精确定位（不依赖真实用户名）
  read -r kid kuser <<< "$(echo "$USERS_JSON" | python3 -c "
import sys, json
uid = int('$uid')
for u in json.load(sys.stdin):
    attrs = u.get('attributes') or {}
    vals = attrs.get('octopus_user_id') or []
    if vals and str(vals[0]) == str(uid):
        print(u['id'], u['username']); break
else:
    print('', '')
")"

  if [ -z "$kid" ]; then
    printf "  ⚠️  UmcUserId=%-4s 在 Keycloak 中找不到（attributes.octopus_user_id）\n" "$uid"
    missing=$((missing+1)); continue
  fi

  if [ "$kuser" = "$NEW_USER" ]; then
    printf "  ⏭  UmcUserId=%-4s 已是 %s，跳过\n" "$uid" "$NEW_USER"
    skipped=$((skipped+1)); continue
  fi

  if [ "$DRY_RUN" = "1" ]; then
    printf "  ·  UmcUserId=%-4s %-20s → %s\n" "$uid" "$kuser" "$NEW_USER"
    ok=$((ok+1)); continue
  fi

  CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X PUT \
    "$KEYCLOAK_URL/admin/realms/$REALM/users/$kid" \
    -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
    -d "{\"username\":\"$NEW_USER\",\"email\":\"$NEW_MAIL\",\"firstName\":\"$NEW_NICK\",\"emailVerified\":false}")

  if [ "$CODE" = "204" ] || [ "$CODE" = "200" ]; then
    printf "  ✅ UmcUserId=%-4s → %s（原账号已匿名）\n" "$uid" "$NEW_USER"
    ok=$((ok+1))
  else
    printf "  ❌ UmcUserId=%-4s 更新失败（HTTP %s）\n" "$uid" "$CODE"
    failed=$((failed+1))
  fi
done

echo
echo "════════════════════════════════════════════════════"
echo "  成功 $ok ｜ 跳过 $skipped ｜ 未找到 $missing ｜ 失败 $failed"
echo "════════════════════════════════════════════════════"
echo
echo "提示：Keycloak 用户的 sub（id）不会随用户名变化，"
echo "      因此 UMC 的 KcSub 与零售域成员关系都不受影响。"

[ "$failed" -gt 0 ] && exit 1
exit 0
