# /fix-issue — GitHub Issue 修复流程

根据 GitHub Issue 编号，完成从分析到 PR 的完整修复流程。

## 用法
```
/fix-issue 42
/fix-issue https://github.com/owner/repo/issues/42
```

## 执行步骤

### 1. 读取 Issue
```bash
gh issue view <issue-number> --json title,body,labels,assignees
```

### 2. 确认当前分支状态
```bash
git status
git branch
```

### 3. 创建修复分支
```bash
# 命名规范：fix/<issue-number>-<简短描述>
git checkout -b fix/42-oidc-token-audience-mismatch
```

### 4. 复现问题
- 阅读 Issue 描述，理解预期行为 vs 实际行为
- 找到相关代码文件（优先搜索 Issue 中提到的关键词）
- 在修复前先确认问题可复现

### 5. 实施修复
- 只改与 Issue 直接相关的代码
- 不做额外重构或"顺手优化"
- 如果发现其他问题，另开 Issue 记录

### 6. 验证修复
- 运行相关测试：`dotnet test` 或 `npm test`
- 手动验证场景（参考 Issue 中的复现步骤）

### 7. 提交
```bash
git add <changed-files>
git commit -m "fix: <问题简述> (#<issue-number>)"
```

### 8. 创建 PR
```bash
gh pr create \
  --title "fix: <问题简述> (#<issue-number>)" \
  --body "Closes #<issue-number>\n\n## 根本原因\n\n## 修复方案\n\n## 测试验证"
```

## 注意事项
- 修复范围严格对齐 Issue，不扩大
- PR 描述必须包含"根本原因"和"修复方案"
- 关联 Issue 使用 `Closes #number` 语法，合并后自动关闭
