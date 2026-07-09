# 地球Online — Claude Code Game Studios 架构

## 技术栈
- Engine: Unity (Tuanjie 团结引擎 1.9.3 / Unity 2022.3.62t11)
- Language: C# (.NET Standard 2.1)
- Version Control: Git (GitHub: 1685yhy/EarthOnline)
- Build System: Unity Build Pipeline
- Asset Pipeline: TextMesh Pro, Standard Shader, Unity UGUI

## 项目结构
@.claude/docs/directory-structure.md

## 协作协议 — User-driven, NOT autonomous
- Question → Options → Decision → Draft → Approval
- 多文件修改需显式批准
- 无用户指令不提交

## 编码规范
- 所有公开API需文档注释
- 游戏数值必须数据驱动（JSON配置），不硬编码
- EventBus解耦 > 直接引用
- 新系统需ADR文档
- 测试放Tests/目录

## Agent团队 (49人三层架构)
@.claude/agents/README.md

## 引擎版本
Tuanjie 1.9.3 (Unity 2022.3.62t11) — 团结引擎中国版
