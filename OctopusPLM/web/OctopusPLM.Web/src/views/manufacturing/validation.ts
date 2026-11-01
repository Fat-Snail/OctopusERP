export type ValidationIssue = { path: string; message: string };

type DraftLike = {
  bomItems: Array<{
    materialId?: number;
    materialCode: string;
    materialName: string;
    quantity: number;
    lossRate: number;
  }>;
  operations: Array<{
    operationCode: string;
    operationName: string;
    requiredCapability: string;
    standardCycleSec: number;
    setupMinutes: number;
  }>;
};

export function validateDraft(draft: DraftLike): ValidationIssue[] {
  const issues: ValidationIssue[] = [];
  if (!draft.bomItems.length)
    issues.push({ path: "bomItems", message: "至少需要一条 BOM 明细" });
  draft.bomItems.forEach((item, index) => {
    const prefix = `bomItems[${index}]`;
    if (!item.materialId)
      issues.push({ path: `${prefix}.materialId`, message: "待匹配物料" });
    if (!item.materialCode?.trim())
      issues.push({
        path: `${prefix}.materialCode`,
        message: "物料编码"
      });
    if (!item.materialName?.trim())
      issues.push({
        path: `${prefix}.materialName`,
        message: "物料名称"
      });
    if (Number(item.quantity) <= 0)
      issues.push({ path: `${prefix}.quantity`, message: "用量必须大于 0" });
    if (Number(item.lossRate) < 0)
      issues.push({ path: `${prefix}.lossRate`, message: "损耗率不能小于 0" });
  });

  if (!draft.operations.length)
    issues.push({ path: "operations", message: "至少需要一道工序" });
  draft.operations.forEach((operation, index) => {
    const prefix = `operations[${index}]`;
    if (!operation.operationCode?.trim())
      issues.push({
        path: `${prefix}.operationCode`,
        message: "工序编码"
      });
    if (!operation.operationName?.trim())
      issues.push({
        path: `${prefix}.operationName`,
        message: "工序名称"
      });
    if (!operation.requiredCapability?.trim())
      issues.push({
        path: `${prefix}.requiredCapability`,
        message: "能力编码"
      });
    if (Number(operation.standardCycleSec) <= 0)
      issues.push({
        path: `${prefix}.standardCycleSec`,
        message: "标准工时必须大于 0"
      });
    if (Number(operation.setupMinutes) < 0)
      issues.push({
        path: `${prefix}.setupMinutes`,
        message: "换型时间不能小于 0"
      });
  });
  return issues;
}

export function issueFor(issues: ValidationIssue[], path: string) {
  return issues.find(issue => issue.path === path)?.message ?? "";
}

export function hasIssue(issues: ValidationIssue[], path: string) {
  return Boolean(issueFor(issues, path));
}

export function issueSummary(issues: ValidationIssue[]) {
  const messages = [...new Set(issues.map(issue => issue.message))];
  if (messages.length <= 3) return messages.join("、");
  return `${messages.slice(0, 3).join("、")}，另有 ${messages.length - 3} 项`;
}
