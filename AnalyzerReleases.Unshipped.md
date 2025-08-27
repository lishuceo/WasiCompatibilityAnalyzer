; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## 1.0.0

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
WASI001 | WebAssembly兼容性 | Error | Task.Delay在WebAssembly环境中不可用，会导致运行时错误。
WASI002 | WebAssembly兼容性 | Error | Task.Run在WebAssembly环境中不可用，WebAssembly不支持多线程操作。
WASI003 | WebAssembly兼容性 | Error | Console类在WebAssembly环境中不可用，请使用Game.Logger进行日志记录。
WASI004 | WebAssembly兼容性 | Error | Thread类在WebAssembly环境中完全不可用。
WASI005 | WebAssembly兼容性 | Error | ThreadPool类在WebAssembly环境中完全不可用。
WASI006 | WebAssembly兼容性 | Error | Parallel类在WebAssembly环境中不可用，不支持并行处理。
WASI007 | WebAssembly兼容性 | Error | 直接的文件系统访问在WebAssembly环境中受限。
WASI008 | WebAssembly兼容性 | Error | 直接的网络访问在WebAssembly环境中受限。
WASI009 | WebAssembly兼容性 | Error | Process类在WebAssembly环境中完全不可用。
WASI010 | WebAssembly兼容性 | Error | Registry类在WebAssembly环境中不可用。
WASI011 | WebAssembly兼容性 | Error | System.Timers.Timer在WebAssembly环境中不可用。
WASI012 | WebAssembly兼容性 | Warning | 过时的API可能在WebAssembly环境中存在兼容性问题，建议使用推荐的替代方案。
WASI013 | WebAssembly兼容性 | Warning | 编辑器隐藏的API通常为内部实现细节，在WebAssembly环境中使用可能导致不可预期的行为。
