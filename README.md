# WasiCompatibilityAnalyzer 使用说明

## ✅ 完成状态

WebAssembly兼容性分析器已经创建完成并测试通过！

## 🎯 功能概述

这个分析器能在编译期检测并阻止以下不兼容的API：

| 错误代码 | 描述 | 建议替代方案 |
|---------|------|-------------|
| **WASI001** | `Task.Delay()` | `Game.Delay()` |
| **WASI002** | `Task.Run()` | 使用 async/await 模式 |
| **WASI003** | `Console.*` 全部方法 | `Game.Logger.LogXXX()` |
| **WASI004** | `Thread.*` 全部成员 | 避免多线程，使用异步 |
| **WASI005** | `ThreadPool.*` 全部成员 | 避免线程池操作 |
| **WASI006** | `Parallel.*` 全部成员 | 使用顺序异步处理 |
| **WASI007** | 文件系统API | `Game.FileSystem` |
| **WASI008** | 网络API | `Game.Network` |
| **WASI009** | `Process.*` 进程操作 | 不支持 |
| **WASI010** | `Registry.*` 注册表 | 不支持 |
| **WASI011** | `System.Timers.Timer` | `Game.CreateTimer()` |
| **WASI012** ⚠️ | 标记为过时的API (ObsoleteAttribute) | 使用推荐的替代方案 |
| **WASI013** ⚠️ | 编辑器隐藏的内部API (EditorBrowsableAttribute.Never) | 避免使用内部实现细节 |

### 📋 规则说明

- **❌ Error级别**: 这些API会阻止编译，必须修改
- **⚠️ Warning级别**: 这些API会产生警告，建议修改但不阻止编译

**WASI012** 检测所有标记为 `[Obsolete]` 的API，过时的API可能在WebAssembly环境中存在兼容性问题。

**WASI013** 检测所有标记为 `[EditorBrowsable(EditorBrowsableState.Never)]` 的内部API，这些API为框架内部实现，不应在用户代码中直接使用。

## 🚀 集成到项目

### 方法1：项目引用（推荐）

在你的 `.csproj` 文件中添加：

```xml
<ItemGroup>
  <ProjectReference Include="../WasiCore/WasiCompatibilityAnalyzer/WasiCompatibilityAnalyzer.csproj" 
                    OutputItemType="Analyzer" 
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

### 方法2：NuGet包引用

```xml
<ItemGroup>
  <PackageReference Include="WasiCompatibilityAnalyzer" Version="1.0.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

## 🧪 测试验证

分析器已通过完整测试，能够成功检测：

```csharp
// ❌ 这些代码会产生编译错误
await Task.Delay(1000);                    // WASI001
Console.WriteLine("Hello");                // WASI003  
var thread = new Thread(() => {});         // WASI004
thread.Start();                            // WASI004
File.ReadAllText("test.txt");              // WASI007
```

```csharp
// ✅ 正确的替代方案
await Game.Delay(1000);                    // 替代 Task.Delay
Game.Logger.LogInformation("Hello");       // 替代 Console.WriteLine
Game.CreateTimer(1000, () => {});          // 替代 Timer
```

## 🔧 开发团队使用建议

1. **立即集成**：将分析器添加到所有WasiAsync项目中
2. **CI/CD集成**：构建服务器会自动拒绝不兼容的代码  
3. **IDE集成**：Visual Studio/Rider中会实时显示错误波浪线
4. **团队培训**：确保所有开发者了解WebAssembly环境的限制

## 📍 文件位置

- **分析器源码**：`WasiCore/WasiCompatibilityAnalyzer/`
- **构建产物**：`WasiCore/WasiCompatibilityAnalyzer/bin/Debug/netstandard2.0/WasiCompatibilityAnalyzer.dll`
- **测试项目**：`WasiAnalyzerTest/TestAnalyzer/`

## 🎯 成果总结

✅ **编译期阻止**：完全防止不兼容代码进入运行时  
✅ **全面覆盖**：检测所有主要的WebAssembly不兼容API  
✅ **清晰提示**：提供具体的替代方案建议  
✅ **团队统一**：确保所有开发者遵循相同规则  
✅ **CI/CD友好**：自动化构建流程集成  

现在你的团队可以放心地开发WasiAsync项目，分析器会在编译期就阻止所有WebAssembly不兼容的API使用！
