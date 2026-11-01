# OctopusERP — UI/交互设计文档

本文件夹包含交给 Claude Design 完成的 UI/交互设计素材。

## 目录

| 文件 | 类别 | 内容 |
|---|---|---|
| `tokens.md` | 基础 | 设计 Token（颜色、字体、间距、圆角） |
| `oa-h5.md` | H5 移动端 | OctopusOA 移动端完整功能规格 |
| `umc-h5.md` | H5 移动端 | OctopusUMC 移动端完整功能规格 |
| `mes-production-schedule.md` | 桌面端·MES | 服装生产排程板（甘特 + 日历多选拖拽） |

## 背景说明

OctopusERP 是一套企业级 ERP 系统，包含：
- **OctopusUMC**：统一用户中心（IdP，管理全平台账号/权限/组织）
- **OctopusOA**：办公自动化系统（审批流/考勤/公告/通讯录/会议室）

当前桌面端已完成，现需针对上述两个系统设计 **H5 移动端版本**，技术栈为 Vue 3 + Vite，面向手机浏览器（375px 基准宽度）。

## 设计要求

- **风格**：企业内网 App，克制、专业，参考飞书/钉钉移动端风格
- **交互**：原生手机操作习惯（底部导航、顶部返回栏、下拉刷新、上拉加载）
- **底部安全区**：支持 iPhone 刘海屏 safe-area-inset
- **字号基准**：14px 正文，12px 辅助，16px 标题
- **色彩系统**：延续桌面端 Token，见 `tokens.md`
