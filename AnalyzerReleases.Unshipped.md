; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## 1.0.0

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
WASI001 | WebAssembly兼容性 | Error | Task.Delay在WebAssembly环境中不可用，会导致运行时错误。
WASI002 | WebAssembly兼容性 | Error | Task.Run在WebAssembly环境中不可用，WebAssembly不支持多线程操作。
WASI003 | WebAssembly兼容性 | Error | Thread类在WebAssembly环境中完全不可用。
WASI004 | WebAssembly兼容性 | Error | ThreadPool类在WebAssembly环境中完全不可用。
WASI005 | WebAssembly兼容性 | Error | Parallel类在WebAssembly环境中不可用，不支持并行处理。
WASI006 | WebAssembly兼容性 | Error | 直接的文件系统访问在WebAssembly环境中受限。
WASI007 | WebAssembly兼容性 | Error | 直接的网络访问在WebAssembly环境中受限。
WASI008 | WebAssembly兼容性 | Error | Process类在WebAssembly环境中完全不可用。
WASI009 | WebAssembly兼容性 | Error | Registry类在WebAssembly环境中不可用。
WASI010 | WebAssembly兼容性 | Error | System.Timers.Timer在WebAssembly环境中不可用。
WASI011 | WebAssembly兼容性 | Warning | 过时的API可能在WebAssembly环境中存在兼容性问题，建议使用推荐的替代方案。
WASI012 | WebAssembly兼容性 | Warning | 编辑器隐藏的API通常为内部实现细节，在WebAssembly环境中使用可能导致不可预期的行为。
WASI013 | 平台专用API | Error | 客户端专用API必须在#if CLIENT预处理指令内使用。
WASI014 | 平台专用API | Error | 服务器专用API必须在#if SERVER预处理指令内使用。
WASI015 | 游戏数据完整性 | Error | 每个定义的GameMode必须有对应的GameDataGameMode实例，否则运行时会报错。

### Changes
- 新增WASI013和WASI014规则用于检测平台专用API
- 新增WASI015规则用于检测GameMode未初始化的问题，避免运行时错误
- 添加基于WasiCore源码的智能API元数据生成器
- 移除默认元数据机制，强制用户生成准确的API数据
- 改进条件编译检测算法，支持嵌套和复杂场景
- 添加跨平台路径支持和便捷更新脚本
