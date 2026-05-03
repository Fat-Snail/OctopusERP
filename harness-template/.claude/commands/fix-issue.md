# /fix-issue — GitHub Issue fix workflow

From issue number to merged PR in one command.

## Usage
```
/fix-issue 42
/fix-issue https://github.com/{{GITHUB_ORG}}/{{GITHUB_REPO}}/issues/42
```

## Steps

### 1. Read the issue
```bash
gh issue view <number> --json title,body,labels,assignees
```

### 2. Check working tree
```bash
git status && git branch
```

### 3. Create a fix branch
```bash
# Convention: fix/<number>-<short-slug>
git checkout -b fix/42-short-description
```

### 4. Reproduce before fixing
Read the issue description. Locate related code. Confirm the problem is
reproducible before writing any fix.

### 5. Implement the fix
- Change only code that is directly related to the issue.
- No "while I'm here" refactors — open a separate issue for those.

### 6. Verify
```bash
{{TEST_CMD}}
# Manually verify the reproduction steps from the issue are now resolved.
```

### 7. Commit
```bash
git add <files>
git commit -m "fix: <brief description> (#<number>)"
```

### 8. Open PR
```bash
gh pr create \
  --title "fix: <brief description> (#<number>)" \
  --body "Closes #<number>

## Root cause

## Fix

## Verification"
```

## Rules
- PR scope must match issue scope exactly.
- `Closes #number` in the PR body auto-closes the issue on merge.
- Do not amend existing commits — create a new one if the hook rejects.
