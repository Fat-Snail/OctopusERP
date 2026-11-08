-- ═══════════════════════════════════════════════════════════════════════
-- 20261108_001 — 审批中心：流程定义 + 示例审批数据
--
-- 幂等：全部用 WHERE NOT EXISTS 守卫，可手工重放。
-- 定义按 Code 判重；实例按 Title 判重；执行/任务/历史按实例内节点判重。
-- 注意：JSON 用真实 Unicode，避免 MySQL 把反斜杠当转义符。
-- ═══════════════════════════════════════════════════════════════════════

INSERT INTO wf_definition (Code, Name, Version, Status, GraphJson, FormSchema, Category, BusinessKey, CreateTime)
SELECT 'leave', '请假审批', 1, 1, '{"nodes":[{"id":"start","type":"start","name":"开始"},{"id":"n1","type":"approval","name":"直属主管审批","config":{"approverType":"dept_leader","slaHours":24}},{"id":"n2","type":"approval","name":"HR 确认","config":{"approverType":"role","approverValue":"oa_admin","slaHours":48}},{"id":"end","type":"end","name":"结束"}],"edges":[{"from":"start","to":"n1"},{"from":"n1","to":"n2"},{"from":"n2","to":"end"}]}', '{"fields":[{"key":"leaveType","label":"请假类型","type":"select","options":[{"label":"年假","value":"annual"},{"label":"事假","value":"personal"},{"label":"病假","value":"sick"}],"required":true},{"key":"startDate","label":"开始日期","type":"date","required":true},{"key":"endDate","label":"结束日期","type":"date","required":true},{"key":"days","label":"天数","type":"number","required":true},{"key":"reason","label":"事由","type":"textarea","required":true}]}', 'leave', 'leave', '2026-11-08 03:18:31.50881'
WHERE NOT EXISTS (SELECT 1 FROM wf_definition WHERE Code = 'leave');

INSERT INTO wf_definition (Code, Name, Version, Status, GraphJson, FormSchema, Category, BusinessKey, CreateTime)
SELECT 'expense', '报销审批', 1, 1, '{"nodes":[{"id":"start","type":"start","name":"开始"},{"id":"n1","type":"approval","name":"直属主管审批","config":{"approverType":"dept_leader","slaHours":24}},{"id":"n2","type":"approval","name":"财务审核","config":{"approverType":"role","approverValue":"oa_admin","slaHours":48}},{"id":"gw","type":"exclusive_gateway","name":"金额分支"},{"id":"n3","type":"approval","name":"总经理审批","config":{"approverType":"user","approverValue":"1","slaHours":72}},{"id":"end","type":"end","name":"结束"}],"edges":[{"from":"start","to":"n1"},{"from":"n1","to":"n2"},{"from":"n2","to":"gw"},{"from":"gw","to":"n3","condition":"amount >= 100000"},{"from":"gw","to":"end"},{"from":"n3","to":"end"}]}', '{"fields":[{"key":"expenseType","label":"报销类型","type":"select","options":[{"label":"差旅费","value":"travel"},{"label":"办公用品","value":"office"},{"label":"招待费","value":"entertainment"}],"required":true},{"key":"amount","label":"金额(元)","type":"number","required":true},{"key":"description","label":"说明","type":"textarea","required":true}]}', 'expense', 'expense', '2026-11-08 03:18:31.592299'
WHERE NOT EXISTS (SELECT 1 FROM wf_definition WHERE Code = 'expense');

INSERT INTO wf_definition (Code, Name, Version, Status, GraphJson, FormSchema, Category, BusinessKey, CreateTime)
SELECT 'attendance_fix', '补卡审批', 1, 1, '{"nodes":[{"id":"start","type":"start","name":"开始"},{"id":"n1","type":"approval","name":"直属主管审批","config":{"approverType":"dept_leader","slaHours":8}},{"id":"n2","type":"approval","name":"HR 确认","config":{"approverType":"role","approverValue":"oa_admin","slaHours":24}},{"id":"end","type":"end","name":"结束"}],"edges":[{"from":"start","to":"n1"},{"from":"n1","to":"n2"},{"from":"n2","to":"end"}]}', '{"fields":[{"key":"fixDate","label":"补卡日期","type":"date","required":true},{"key":"fixType","label":"补卡类型","type":"select","options":[{"label":"上班打卡","value":"checkIn"},{"label":"下班打卡","value":"checkOut"}],"required":true},{"key":"fixTime","label":"补卡时间","type":"text","required":true},{"key":"reason","label":"原因","type":"textarea","required":true}]}', 'attendance_fix', 'attendance_fix', '2026-11-08 03:18:31.59804'
WHERE NOT EXISTS (SELECT 1 FROM wf_definition WHERE Code = 'attendance_fix');

INSERT INTO wf_definition (Code, Name, Version, Status, GraphJson, FormSchema, Category, BusinessKey, CreateTime)
SELECT 'purchase_order', '采购订单审批', 1, 1, '{"nodes":[{"id":"start","type":"start","name":"开始"},{"id":"n1","type":"approval","name":"采购主管审批","config":{"approverType":"dept_leader","slaHours":24}},{"id":"n2","type":"approval","name":"财务审核","config":{"approverType":"role","approverValue":"oa_admin","slaHours":48}},{"id":"gw","type":"exclusive_gateway","name":"金额分支"},{"id":"n3","type":"approval","name":"总经理审批","config":{"approverType":"user","approverValue":"1","slaHours":72}},{"id":"svc","type":"service_task","name":"创建WMS入库单","config":{"approverType":"user","serviceKind":"create_wms_inbound"}},{"id":"end","type":"end","name":"结束"}],"edges":[{"from":"start","to":"n1"},{"from":"n1","to":"n2"},{"from":"n2","to":"gw"},{"from":"gw","to":"n3","condition":"amount >= 100000"},{"from":"gw","to":"svc"},{"from":"n3","to":"svc"},{"from":"svc","to":"end"}]}', '{"fields": [{"key": "supplierName", "label": "供应商", "type": "text", "required": true}, {"key": "productName", "label": "商品名称", "type": "text", "required": true}, {"key": "productPicker", "label": "商品选择（自定义组件）", "type": "text", "componentId": "demo:product-picker"}, {"key": "plmProductId", "label": "PLM商品ID", "type": "number"}, {"key": "spec", "label": "规格", "type": "text"}, {"key": "unit", "label": "单位", "type": "text"}, {"key": "quantity", "label": "数量", "type": "number", "required": true}, {"key": "amount", "label": "金额(元)", "type": "number", "required": true}, {"key": "reason", "label": "采购说明", "type": "textarea"}], "extensions": [{"type": "iframe", "slot": "after_form", "title": "采购单明细（来自 MES）", "url": "/subsystem-demo.html", "height": 220}]}', 'purchase', 'purchase_order', '2026-11-08 03:18:31.599679'
WHERE NOT EXISTS (SELECT 1 FROM wf_definition WHERE Code = 'purchase_order');

INSERT INTO wf_instance (DefinitionId, DefinitionVersion, BusinessKey, Title, ApplicantId, ApplicantName, Status, FormData, Variables, CreateTime, UpdateTime, Deadline, IsOverdue, IsSlaWarned)
SELECT (SELECT Id FROM wf_definition WHERE Code = 'leave'), 1, NULL, '张三的请假申请（示例）', 2, '张三', 'pending', '{"leaveType":"annual","startDate":"2026-03-10","endDate":"2026-03-11","days":2,"reason":"家中有事"}', '{}', '2026-11-07T03:22:31', '2026-11-07T03:22:31', NULL, 0, 0
WHERE NOT EXISTS (SELECT 1 FROM wf_instance WHERE Title = '张三的请假申请（示例）');

INSERT INTO wf_instance (DefinitionId, DefinitionVersion, BusinessKey, Title, ApplicantId, ApplicantName, Status, FormData, Variables, CreateTime, UpdateTime, Deadline, IsOverdue, IsSlaWarned)
SELECT (SELECT Id FROM wf_definition WHERE Code = 'expense'), 1, NULL, '李四的报销申请（示例）', 3, '李四', 'pending', '{"expenseType":"travel","amount":5000,"description":"上海出差"}', '{}', '2026-11-07T03:22:31', '2026-11-07T03:22:31', NULL, 0, 0
WHERE NOT EXISTS (SELECT 1 FROM wf_instance WHERE Title = '李四的报销申请（示例）');

INSERT INTO wf_instance (DefinitionId, DefinitionVersion, BusinessKey, Title, ApplicantId, ApplicantName, Status, FormData, Variables, CreateTime, UpdateTime, Deadline, IsOverdue, IsSlaWarned)
SELECT (SELECT Id FROM wf_definition WHERE Code = 'attendance_fix'), 1, NULL, '张三的补卡申请（示例）', 2, '张三', 'pending', '{"fixDate":"2026-03-05","fixType":"checkIn","fixTime":"09:20","reason":"忘记打卡"}', '{}', '2026-11-07T21:22:31', '2026-11-07T21:22:31', NULL, 0, 0
WHERE NOT EXISTS (SELECT 1 FROM wf_instance WHERE Title = '张三的补卡申请（示例）');

INSERT INTO wf_instance (DefinitionId, DefinitionVersion, BusinessKey, Title, ApplicantId, ApplicantName, Status, FormData, Variables, CreateTime, UpdateTime, Deadline, IsOverdue, IsSlaWarned)
SELECT (SELECT Id FROM wf_definition WHERE Code = 'purchase_order'), 1, 'purchase_order:9001', '采购面料 8 万（示例）', 2, '张三', 'pending', '{"supplierName":"供应商A","productName":"面料X","quantity":100,"amount":80000}', '{}', '2026-11-08T00:22:31', '2026-11-08T00:22:31', NULL, 0, 0
WHERE NOT EXISTS (SELECT 1 FROM wf_instance WHERE Title = '采购面料 8 万（示例）');

INSERT INTO wf_instance (DefinitionId, DefinitionVersion, BusinessKey, Title, ApplicantId, ApplicantName, Status, FormData, Variables, CreateTime, UpdateTime, Deadline, IsOverdue, IsSlaWarned)
SELECT (SELECT Id FROM wf_definition WHERE Code = 'expense'), 1, NULL, '李四的报销申请-已通过（示例）', 3, '李四', 'approved', '{"expenseType":"travel","amount":3000,"description":"北京出差"}', '{}', '2026-11-05T03:22:31', '2026-11-05T03:22:31', NULL, 0, 0
WHERE NOT EXISTS (SELECT 1 FROM wf_instance WHERE Title = '李四的报销申请-已通过（示例）');

INSERT INTO wf_instance (DefinitionId, DefinitionVersion, BusinessKey, Title, ApplicantId, ApplicantName, Status, FormData, Variables, CreateTime, UpdateTime, Deadline, IsOverdue, IsSlaWarned)
SELECT (SELECT Id FROM wf_definition WHERE Code = 'leave'), 1, NULL, '张三的请假申请-已驳回（示例）', 2, '张三', 'rejected', '{"leaveType":"personal","startDate":"2026-03-20","endDate":"2026-03-25","days":5,"reason":"事假"}', '{}', '2026-11-06T03:22:31', '2026-11-06T03:22:31', NULL, 0, 0
WHERE NOT EXISTS (SELECT 1 FROM wf_instance WHERE Title = '张三的请假申请-已驳回（示例）');

INSERT INTO wf_execution (InstanceId, NodeId, ParentId, ForkGroup, JoinExpected, Status, CreateTime)
SELECT i.Id, 'n1', NULL, NULL, 0, 'active', '2026-11-08T03:22:31'
FROM wf_instance i WHERE i.Title = '张三的请假申请（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_execution x WHERE x.InstanceId = i.Id AND x.NodeId = 'n1');

INSERT INTO wf_execution (InstanceId, NodeId, ParentId, ForkGroup, JoinExpected, Status, CreateTime)
SELECT i.Id, 'n1', NULL, NULL, 0, 'active', '2026-11-08T03:22:31'
FROM wf_instance i WHERE i.Title = '李四的报销申请（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_execution x WHERE x.InstanceId = i.Id AND x.NodeId = 'n1');

INSERT INTO wf_execution (InstanceId, NodeId, ParentId, ForkGroup, JoinExpected, Status, CreateTime)
SELECT i.Id, 'n1', NULL, NULL, 0, 'active', '2026-11-08T03:22:31'
FROM wf_instance i WHERE i.Title = '张三的补卡申请（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_execution x WHERE x.InstanceId = i.Id AND x.NodeId = 'n1');

INSERT INTO wf_execution (InstanceId, NodeId, ParentId, ForkGroup, JoinExpected, Status, CreateTime)
SELECT i.Id, 'n1', NULL, NULL, 0, 'active', '2026-11-08T03:22:31'
FROM wf_instance i WHERE i.Title = '采购面料 8 万（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_execution x WHERE x.InstanceId = i.Id AND x.NodeId = 'n1');

INSERT INTO wf_execution (InstanceId, NodeId, ParentId, ForkGroup, JoinExpected, Status, CreateTime)
SELECT i.Id, 'end', NULL, NULL, 0, 'completed', '2026-11-08T03:22:31'
FROM wf_instance i WHERE i.Title = '李四的报销申请-已通过（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_execution x WHERE x.InstanceId = i.Id AND x.NodeId = 'end');

INSERT INTO wf_execution (InstanceId, NodeId, ParentId, ForkGroup, JoinExpected, Status, CreateTime)
SELECT i.Id, 'n1', NULL, NULL, 0, 'cancelled', '2026-11-08T03:22:31'
FROM wf_instance i WHERE i.Title = '张三的请假申请-已驳回（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_execution x WHERE x.InstanceId = i.Id AND x.NodeId = 'n1');

INSERT INTO wf_task (InstanceId, NodeId, ExecutionId, AssigneeId, AssigneeName, Status, Action, Comment, Deadline, ActionTime, CreateTime)
SELECT i.Id, 'n1', (SELECT e.Id FROM wf_execution e WHERE e.InstanceId = i.Id AND e.NodeId = 'n1' LIMIT 1), 1, '超级管理员', 'pending', NULL, NULL, NULL, NULL, '2026-11-07T03:22:31'
FROM wf_instance i WHERE i.Title = '张三的请假申请（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_task x WHERE x.InstanceId = i.Id AND x.NodeId = 'n1');

INSERT INTO wf_task (InstanceId, NodeId, ExecutionId, AssigneeId, AssigneeName, Status, Action, Comment, Deadline, ActionTime, CreateTime)
SELECT i.Id, 'n1', (SELECT e.Id FROM wf_execution e WHERE e.InstanceId = i.Id AND e.NodeId = 'n1' LIMIT 1), 1, '超级管理员', 'pending', NULL, NULL, NULL, NULL, '2026-11-07T03:22:31'
FROM wf_instance i WHERE i.Title = '李四的报销申请（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_task x WHERE x.InstanceId = i.Id AND x.NodeId = 'n1');

INSERT INTO wf_task (InstanceId, NodeId, ExecutionId, AssigneeId, AssigneeName, Status, Action, Comment, Deadline, ActionTime, CreateTime)
SELECT i.Id, 'n1', (SELECT e.Id FROM wf_execution e WHERE e.InstanceId = i.Id AND e.NodeId = 'n1' LIMIT 1), 1, '超级管理员', 'pending', NULL, NULL, NULL, NULL, '2026-11-07T21:22:31'
FROM wf_instance i WHERE i.Title = '张三的补卡申请（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_task x WHERE x.InstanceId = i.Id AND x.NodeId = 'n1');

INSERT INTO wf_task (InstanceId, NodeId, ExecutionId, AssigneeId, AssigneeName, Status, Action, Comment, Deadline, ActionTime, CreateTime)
SELECT i.Id, 'n1', (SELECT e.Id FROM wf_execution e WHERE e.InstanceId = i.Id AND e.NodeId = 'n1' LIMIT 1), 1, '超级管理员', 'pending', NULL, NULL, NULL, NULL, '2026-11-08T00:22:31'
FROM wf_instance i WHERE i.Title = '采购面料 8 万（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_task x WHERE x.InstanceId = i.Id AND x.NodeId = 'n1');

INSERT INTO wf_task (InstanceId, NodeId, ExecutionId, AssigneeId, AssigneeName, Status, Action, Comment, Deadline, ActionTime, CreateTime)
SELECT i.Id, 'n1', (SELECT e.Id FROM wf_execution e WHERE e.InstanceId = i.Id AND e.NodeId = 'n1' LIMIT 1), 1, '超级管理员', 'approved', 'approve', '同意', NULL, '2026-11-06T03:22:31', '2026-11-05T03:22:31'
FROM wf_instance i WHERE i.Title = '李四的报销申请-已通过（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_task x WHERE x.InstanceId = i.Id AND x.NodeId = 'n1');

INSERT INTO wf_task (InstanceId, NodeId, ExecutionId, AssigneeId, AssigneeName, Status, Action, Comment, Deadline, ActionTime, CreateTime)
SELECT i.Id, 'n2', (SELECT e.Id FROM wf_execution e WHERE e.InstanceId = i.Id AND e.NodeId = 'n2' LIMIT 1), 1, '超级管理员', 'approved', 'approve', '同意', NULL, '2026-11-06T03:22:31', '2026-11-05T03:22:31'
FROM wf_instance i WHERE i.Title = '李四的报销申请-已通过（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_task x WHERE x.InstanceId = i.Id AND x.NodeId = 'n2');

INSERT INTO wf_task (InstanceId, NodeId, ExecutionId, AssigneeId, AssigneeName, Status, Action, Comment, Deadline, ActionTime, CreateTime)
SELECT i.Id, 'n3', (SELECT e.Id FROM wf_execution e WHERE e.InstanceId = i.Id AND e.NodeId = 'n3' LIMIT 1), 1, '超级管理员', 'approved', 'approve', '同意', NULL, '2026-11-06T03:22:31', '2026-11-05T03:22:31'
FROM wf_instance i WHERE i.Title = '李四的报销申请-已通过（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_task x WHERE x.InstanceId = i.Id AND x.NodeId = 'n3');

INSERT INTO wf_task (InstanceId, NodeId, ExecutionId, AssigneeId, AssigneeName, Status, Action, Comment, Deadline, ActionTime, CreateTime)
SELECT i.Id, 'n1', (SELECT e.Id FROM wf_execution e WHERE e.InstanceId = i.Id AND e.NodeId = 'n1' LIMIT 1), 1, '超级管理员', 'rejected', 'reject', '请假天数过多', NULL, '2026-11-07T03:22:31', '2026-11-06T03:22:31'
FROM wf_instance i WHERE i.Title = '张三的请假申请-已驳回（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_task x WHERE x.InstanceId = i.Id AND x.NodeId = 'n1');

INSERT INTO wf_history (InstanceId, NodeId, NodeName, Action, ActorId, ActorName, Comment, CreateTime)
SELECT i.Id, 'n1', '直属主管审批', 'approve', 1, '超级管理员', '同意', '2026-11-06T03:22:31'
FROM wf_instance i WHERE i.Title = '李四的报销申请-已通过（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_history x WHERE x.InstanceId = i.Id AND x.NodeId = 'n1' AND x.Action = 'approve');

INSERT INTO wf_history (InstanceId, NodeId, NodeName, Action, ActorId, ActorName, Comment, CreateTime)
SELECT i.Id, 'n2', '财务审核', 'approve', 1, '超级管理员', '同意', '2026-11-06T03:22:31'
FROM wf_instance i WHERE i.Title = '李四的报销申请-已通过（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_history x WHERE x.InstanceId = i.Id AND x.NodeId = 'n2' AND x.Action = 'approve');

INSERT INTO wf_history (InstanceId, NodeId, NodeName, Action, ActorId, ActorName, Comment, CreateTime)
SELECT i.Id, 'n3', '总经理审批', 'approve', 1, '超级管理员', '同意', '2026-11-06T03:22:31'
FROM wf_instance i WHERE i.Title = '李四的报销申请-已通过（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_history x WHERE x.InstanceId = i.Id AND x.NodeId = 'n3' AND x.Action = 'approve');

INSERT INTO wf_history (InstanceId, NodeId, NodeName, Action, ActorId, ActorName, Comment, CreateTime)
SELECT i.Id, 'n1', '直属主管审批', 'reject', 1, '超级管理员', '请假天数过多', '2026-11-07T03:22:31'
FROM wf_instance i WHERE i.Title = '张三的请假申请-已驳回（示例）'
  AND NOT EXISTS (SELECT 1 FROM wf_history x WHERE x.InstanceId = i.Id AND x.NodeId = 'n1' AND x.Action = 'reject');
