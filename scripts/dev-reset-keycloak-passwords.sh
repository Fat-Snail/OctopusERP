#!/usr/bin/env bash
# ════════════════════════════════════════════════════════════════════════
#  ⚠️ 仅限本地开发环境使用 —— 禁止用于任何共享/生产环境
#
#  用途：把 Keycloak 里所有用户的密码统一重置为指定值（默认 Admin@123）。
#
#  为什么需要它：
#    UMC 的用户密码是 BCrypt 哈希，**无法迁移到 Keycloak**（算法不同）。
#    UMC→Keycloak 同步只带用户名/邮箱/姓名/启用状态，不带密码，
#    所以同步过去的用户在 Keycloak 里没有凭据，登录任何 OIDC 应用都会
#    报 Invalid user credentials。
#
#  安全边界：
#    - 本脚本会把**所有人的密码设成同一个已知值**，等于批量开后门。
#      只能在纯本地的开发库上跑。
#    - 跳过 service-account-* 账号（它们是 Keycloak 内部服务账号）。
#    - 不会创建/删除用户，只重置密码。
#
#  用法：
#    bash scripts/dev-reset-keycloak-passwords.sh                  # 用默认 Admin@123
#    bash scripts/dev-reset-keycloak-passwords.sh 'MyPass@123'     # 指定密码
#    DRY_RUN=1 bash scripts/dev-reset-keycloak-passwords.sh        # 只打印，不改
#
#  依赖：curl、python3
# ════════════════════════════════════════════════════════════════════════
set -uo pipefail

KEYCLOAK_URL="${KEYCLOAK_URL:-https://localhost:8088}"
REALM="${KEYCLOAK_REALM:-octopus}"
NEW_PASSWORD="${1:-Admin@123}"
DRY_RUN="${DRY_RUN:-0}"

# 用零售域的邀请机器人（已有 manage-users 权限），避免依赖 master admin 口令
BOT_CLIENT_ID="${BOT_CLIENT_ID:-retail-invite-bot}"
BOT_CLIENT_SECRET="${BOT_CLIENT_SECRET:-retail-invite-bot-secret-dev}"

echo "════════════════════════════════════════════════════"
echo "  Keycloak : $KEYCLOAK_URL"
echo "  Realm    : $REALM"
echo "  新密码   : $NEW_PASSWORD"
echo "  模式     : $([ "$DRY_RUN" = "1" ] && echo '演练（不修改）' || echo '实际执行')"
echo "════════════════════════════════════════════════════"

# ── 1. 取 admin token ─────────────────────────────────────
TOKEN=$(curl -sk -X POST "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" \
  -d grant_type=client_credentials \
  -d client_id="$BOT_CLIENT_ID" \
  -d client_secret="$BOT_CLIENT_SECRET" \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('access_token',''))" 2>/dev/null)

if [ -z "$TOKEN" ]; then
  echo "❌ 拿不到 token（client=$BOT_CLIENT_ID）。"
  echo "   检查 Keycloak 是否启动：curl -sk $KEYCLOAK_URL/realms/$REALM/.well-known/openid-configuration"
  exit 1
fi

# ── 2. 拉用户列表 ─────────────────────────────────────────
USERS_JSON=$(curl -sk -H "Authorization: Bearer $TOKEN" \
  "$KEYCLOAK_URL/admin/realms/$REALM/users?max=500")

COUNT=$(echo "$USERS_JSON" | python3 -c "import sys,json;d=json.load(sys.stdin);print(len(d) if isinstance(d,list) else 0)")
if [ "$COUNT" = "0" ]; then
  echo "❌ 没有取到用户（或返回了错误）："
  echo "$USERS_JSON" | head -c 300
  exit 1
fi
echo "找到 $COUNT 个用户"
echo

# ── 3. 逐个重置密码 ───────────────────────────────────────
ok=0; skipped=0; failed=0

while IFS=$'\t' read -r uid uname; do
  # 跳过 Keycloak 内部服务账号
  case "$uname" in
    service-account-*)
      printf "  ⏭  %-24s 跳过（服务账号）\n" "$uname"
      skipped=$((skipped+1)); continue ;;
  esac

  if [ "$DRY_RUN" = "1" ]; then
    printf "  ·  %-24s 将重置\n" "$uname"
    ok=$((ok+1)); continue
  fi

  CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X PUT \
    "$KEYCLOAK_URL/admin/realms/$REALM/users/$uid/reset-password" \
    -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
    -d "{\"type\":\"password\",\"value\":\"$NEW_PASSWORD\",\"temporary\":false}")

  if [ "$CODE" = "204" ] || [ "$CODE" = "200" ]; then
    printf "  ✅ %-24s 已重置\n" "$uname"
    ok=$((ok+1))
  else
    printf "  ❌ %-24s 失败（HTTP %s）\n" "$uname" "$CODE"
    failed=$((failed+1))
  fi
done < <(echo "$USERS_JSON" | python3 -c "
import sys,json
for u in json.load(sys.stdin):
    print(f\"{u['id']}\t{u['username']}\")")

echo
echo "════════════════════════════════════════════════════"
echo "  成功 $ok ｜ 跳过 $skipped ｜ 失败 $failed"
echo "════════════════════════════════════════════════════"

if [ "$DRY_RUN" != "1" ] && [ "$ok" -gt 0 ]; then
  echo
  echo "验证（任选一个）:"
  echo "  curl -sk -X POST $KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token \\"
  echo "    -d grant_type=password -d client_id=octopus-portal \\"
  echo "    -d username=zhangsan -d password='$NEW_PASSWORD' | head -c 100"
fi

[ "$failed" -gt 0 ] && exit 1
exit 0
