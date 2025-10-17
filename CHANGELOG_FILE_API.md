# 文件系统API分析调整 - 更新日志

## 📅 更新时间: 2025-10-17

## 🎯 变更概述

根据实际的WASM运行环境反馈，**文件系统API（File、Directory等）在WebAssembly环境中是可用的**，只是受到沙箱限制，只能访问有权限的文件夹。因此调整了分析器对这些API的检测级别。

## 📋 具体变更

### 1. 诊断级别调整

**之前**: Error（阻止编译）  
**现在**: Warning（警告但不阻止编译）

### 2. 受影响的API

以下API的诊断级别从Error调整为Warning：

- `System.IO.File.*`
- `System.IO.Directory.*`
- `System.IO.FileStream`
- `System.IO.StreamReader`
- `System.IO.StreamWriter`

### 3. 诊断消息更新

**之前**:
```
WASI006 Error: 在WebAssembly环境中不应使用System.IO.File，请使用Game.FileSystem或相关框架方法
```

**现在**:
```
WASI006 Warning: System.IO.File 在WebAssembly环境中可用，但仅能访问沙箱内有权限的文件夹。建议使用Game.FileSystem以获得更好的跨平台支持
```

## 🔍 代码变更

### WasiCompatibilityAnalyzer.cs

```csharp
// 修改前
public static readonly DiagnosticDescriptor FileSystemRule = new(
    "WASI006", 
    "不允许直接使用文件系统API，请使用框架提供的方法",
    "在WebAssembly环境中不应使用{0}，请使用Game.FileSystem或相关框架方法",
    "WebAssembly兼容性",
    DiagnosticSeverity.Error,  // ❌ Error级别
    isEnabledByDefault: true,
    description: "直接的文件系统访问在WebAssembly环境中受限。");

// 修改后
public static readonly DiagnosticDescriptor FileSystemRule = new(
    "WASI006", 
    "文件系统API仅能访问WASM沙箱环境内有权限的文件夹",
    "{0} 在WebAssembly环境中可用，但仅能访问沙箱内有权限的文件夹。建议使用Game.FileSystem以获得更好的跨平台支持",
    "WebAssembly兼容性",
    DiagnosticSeverity.Warning,  // ✅ Warning级别
    isEnabledByDefault: true,
    description: "文件系统API在WebAssembly环境中可用，但受沙箱限制，只能访问特定的有权限的文件夹。");
```

## 📚 文档更新

### README.md

1. 更新了规则表格，将WASI006标记为Warning级别（⚠️）
2. 添加了详细的"文件系统API说明"章节，包括：
   - ✅ 可用性说明
   - ⚠️ 沙箱限制
   - 🎯 推荐做法
   - 🔧 分析器行为
3. 更新了测试验证示例，区分Error和Warning级别的API
4. 添加了正确使用文件系统API的代码示例

### Test/TestCode.cs

添加了专门的`TestFileSystemAPIs()`测试方法来验证文件系统API产生Warning诊断。

## ✅ 验证结果

测试项目构建输出显示：

```
✅ File.ReadAllText          → warning WASI006
✅ File.WriteAllText          → warning WASI006
✅ Directory.CreateDirectory  → warning WASI006
✅ Directory.GetFiles         → warning WASI006
✅ FileStream                 → warning WASI006
✅ StreamReader               → warning WASI006
✅ StreamWriter               → warning WASI006

✅ Task.Delay                 → error WASI001 (保持不变)
✅ Task.Run                   → error WASI002 (保持不变)
✅ Thread                     → error WASI003 (保持不变)
```

## 🎯 对开发者的影响

### 之前的行为

```csharp
var content = File.ReadAllText("config.json");  // ❌ Error: 阻止编译
```

开发者必须修改代码才能编译通过，即使在某些场景下File API完全可以正常工作。

### 现在的行为

```csharp
var content = File.ReadAllText("config.json");  // ⚠️ Warning: 提醒但允许编译
```

开发者可以：
1. **选项1（推荐）**: 使用框架API `Game.FileSystem.ReadAllText("config.json")`
2. **选项2（允许）**: 继续使用File API，但需要注意沙箱限制

## 📖 使用建议

### 推荐：使用框架API
```csharp
// 🌟 最佳实践
var content = Game.FileSystem.ReadAllText("data/config.json");
Game.FileSystem.WriteAllText("save/game.dat", data);
```

**优势**：
- 更好的跨平台支持
- 明确的权限管理
- 更好的错误处理
- 未来兼容性保证

### 允许：直接使用File API
```csharp
// ⚠️ 可以使用，但需要注意限制
var content = File.ReadAllText("./data/config.json");
File.WriteAllText("./save/game.dat", data);
```

**限制**：
- 只能访问沙箱内有权限的文件夹
- 无法访问系统目录（如C:\Windows\, /etc/等）
- 路径处理可能在不同平台表现不一致

### 不可访问的路径示例
```csharp
// ❌ 这些操作在WASM沙箱中会失败
File.ReadAllText("C:\\Windows\\System32\\config.sys");
File.ReadAllText("/etc/passwd");
File.ReadAllText("../../../sensitive_data.txt");
```

## 🔄 迁移指南

如果你的代码之前因为WASI006错误无法编译，现在有两个选择：

### 选项1: 迁移到框架API（推荐）
```csharp
// 之前（Error）
var content = File.ReadAllText("data.json");

// 推荐（无警告）
var content = Game.FileSystem.ReadAllText("data.json");
```

### 选项2: 保持现有代码（允许）
```csharp
// 现在（Warning，可以编译）
var content = File.ReadAllText("data.json");
// 你可以继续使用，只要确保：
// 1. 文件在沙箱内
// 2. 你了解平台限制
```

## 📝 总结

这次调整使得分析器更准确地反映了WebAssembly环境的实际能力：

1. ✅ **更准确**: 文件系统API可用但受限，不是完全不可用
2. ✅ **更灵活**: 开发者可以根据场景选择合适的方案
3. ✅ **更友好**: Warning而不是Error，不强制修改可以工作的代码
4. ✅ **仍然指导**: 通过警告消息引导开发者使用最佳实践

这个改进让分析器从"过度限制"转变为"明智提醒"，同时保持对真正不兼容API（如Thread、Task.Delay）的严格检查。

