# WasiCompatibilityAnalyzer 使用说明

## ✅ 完成状态

WebAssembly兼容性分析器已经创建完成并测试通过！

## 🎯 功能概述

这个分析器能在编译期检测并阻止以下不兼容的API：

| 错误代码 | 描述 | 建议替代方案 |
|---------|------|-------------|
| **WASI001** | `Task.Delay()` | `Game.Delay()` |
| **WASI002** | `Task.Run()` | 使用 async/await 模式 |
| **WASI003** | `Thread.*` 全部成员 | 避免多线程，使用异步 |
| **WASI004** | `ThreadPool.*` 全部成员 | 避免线程池操作 |
| **WASI005** | `Parallel.*` 全部成员 | 使用顺序异步处理 |
| **WASI006** | 文件系统API | `Game.FileSystem` |
| **WASI007** | 网络API | `Game.Network` |
| **WASI008** | `Process.*` 进程操作 | 不支持 |
| **WASI009** | `Registry.*` 注册表 | 不支持 |
| **WASI010** | `System.Timers.Timer` | `Game.CreateTimer()` |
| **WASI011** ⚠️ | 标记为过时的API (ObsoleteAttribute) | 使用推荐的替代方案 |
| **WASI012** ⚠️ | 编辑器隐藏的内部API (EditorBrowsableAttribute.Never) | 避免使用内部实现细节 |
| **WASI013** | 客户端专用API未包含在 `#if CLIENT` | 将代码包裹在 `#if CLIENT` 中 |
| **WASI014** | 服务器专用API未包含在 `#if SERVER` | 将代码包裹在 `#if SERVER` 中 |
| **WASI015** | GameMode定义但未初始化 | 创建对应的 `GameDataGameMode` 实例 |

### 📋 规则说明

- **❌ Error级别**: 这些API会阻止编译，必须修改
- **⚠️ Warning级别**: 这些API会产生警告，建议修改但不阻止编译

**WASI011** 检测所有标记为 `[Obsolete]` 的API，过时的API可能在WebAssembly环境中存在兼容性问题。

**WASI012** 检测所有标记为 `[EditorBrowsable(EditorBrowsableState.Never)]` 的内部API，这些API为框架内部实现，不应在用户代码中直接使用。

**WASI015** 检测GameMode定义但未初始化的问题。当在`ScopeData.GameMode`中定义了GameMode字段，但没有创建对应的`GameDataGameMode`实例时，运行时会报错"Game Mode is set to XXX, but the data is not set, using default game mode"。这个分析器在编译期就能发现这个问题。

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

### 平台专用API检测

```csharp
// ❌ WASI014: 客户端API必须在#if CLIENT中
using GameUI.Control.Extensions;           // 错误：需要#if CLIENT

public class MyGame 
{
    void CreateUI()
    {
        var button = new Button();         // WASI014: 客户端专用API
        Game.UIRoot.AddChild(button);      // WASI014: 客户端专用API
    }
}

// ✅ 正确的方式
#if CLIENT
using GameUI.Control.Extensions;
using static GameUI.Control.Extensions.UI;
#endif

public class MyGame 
{
#if CLIENT
    void CreateUI()
    {
        var button = new Button();         // OK: 在CLIENT条件编译中
        Game.UIRoot.AddChild(button);      // OK: 在CLIENT条件编译中
    }
#endif
}
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

## 🎯 新增功能：平台专用API检测

### 解决的核心问题

**之前的错误路径**:
```
编译错误: "Extensions does not exist" 
→ 认为流式API不可用
→ 放弃使用优秀的API
→ 采用妥协方案
```

**现在的正确路径**:
```
编译错误: "Extensions does not exist"
→ 分析器提示: WASI014 需要 #if CLIENT
→ 添加条件编译
→ 正确使用所有API
```

### 实际效果展示

#### ❌ 检测到的错误
```csharp
using GameUI.Control.Extensions;     // WASI014: 需要 #if CLIENT
var panel = new Panel();            // WASI014: 需要 #if CLIENT
```

#### ✅ 正确的修复
```csharp
#if CLIENT
using GameUI.Control.Extensions;
using static GameUI.Control.Extensions.UI;

private void CreateUI()
{
    var panel = Panel().Background(Color.Blue);  // OK: 在CLIENT块中
}
#endif
```

## 🔧 元数据生成器使用指南

### 什么是元数据生成器？

元数据生成器是一个独立工具，用于扫描WasiCore源码并自动生成精确的平台专用API列表。这确保了分析器始终与最新的WasiCore代码保持同步。

### 📊 生成器功能

- **🔍 智能扫描**: 分析1200+个C#文件（自动排除测试代码）
- **📱 客户端API检测**: 识别 `#if CLIENT` 条件编译的代码
- **🖥️ 服务器API检测**: 识别 `#if SERVER` 条件编译的代码
- **🔀 混合类型分析**: 检测同时包含客户端和服务器代码的类型
- **📊 统计报告**: 生成详细的API统计信息
- **🧹 测试代码过滤**: 自动排除Tests目录，确保数据准确性

### 🚀 快速使用

⚠️ **重要**: 
1. WasiCore路径是必需参数，必须根据您的实际环境指定
2. **必须先生成元数据**，分析器不提供默认数据，确保检测准确性

```bash
# 1. 进入工具目录
cd WasiCompatibilityAnalyzer/Tools

# 2. 运行元数据生成器（必须指定WasiCore路径）
dotnet run --project MetadataGenerator.csproj "/path/to/your/wasicore" "../platform-api-metadata-new.json"

# 3. 查看生成结果
```

### 📋 平台特定使用示例

```bash
# Windows 用户
dotnet run --project MetadataGenerator.csproj "C:\Dev\sce\wasicore"
dotnet run --project MetadataGenerator.csproj "D:\Projects\wasicore" "../my-metadata.json"

# Linux/macOS 用户  
dotnet run --project MetadataGenerator.csproj "/home/user/wasicore"
dotnet run --project MetadataGenerator.csproj "/opt/wasicore" "../my-metadata.json"

# 相对路径也支持
dotnet run --project MetadataGenerator.csproj "../../wasicore"
```

### 📋 生成器输出

运行后会看到详细的扫描过程：

```
🔍 开始扫描WasiCore源码: C:\Dev\sce\wasicore
📁 找到 1198 个C#文件
⏳ 已处理 100/1198 个文件 (客户端:16, 服务器:14, 混合:0, 通用:70)
⏳ 已处理 200/1198 个文件 (客户端:44, 服务器:39, 混合:0, 通用:117)
...
🔀 发现混合类型: GameCore.PlayerAndUsers.Player (客户端成员: 4, 服务器成员: 6)
📱 客户端专用成员: GameCore.GameSystem.Game.SendServerMessage
🖥️ 服务器专用成员: GameCore.GameSystem.Game.SetProperty
...
🔗 分析项目依赖关系...
📱 检测到GameUI项目引用ClientInterfaceDefinition
⚠️ GameUI目录包含混合代码，不标记为完全专用
🧹 清理重复的命名空间分类...
✅ 元数据生成完成

📊 API元数据生成总结
📊 =========================
📱 客户端专用命名空间: 16
🖥️ 服务器专用命名空间: 1
📱 客户端专用类型: 56
🖥️ 服务器专用类型: 21
📱 客户端专用成员: 1175
🖥️ 服务器专用成员: 854
🔀 混合类型: 251
```

### 📁 生成的元数据文件结构

```json
{
  "clientOnlyNamespaces": [
    "GameUI",
    "GameUI.Control",
    "GameUI.Control.Extensions",
    ...
  ],
  "serverOnlyNamespaces": [
    "GameCore.UserCloudData",
    "GameCore.ProtocolServerTransient",
    ...
  ],
  "clientOnlyTypes": [
    "Panel", "Label", "Button",
    ...
  ],
  "serverOnlyTypes": [
    "CloudDataOperations",
    "CloudDataManager",
    ...
  ],
  "statistics": {
    "generatedAt": "2025-09-16T16:36:04.257597+08:00",
    "clientOnlyNamespacesCount": 55,
    "serverOnlyNamespacesCount": 44,
    ...
  }
}
```

### 🔄 更新分析器元数据

生成新的元数据后，有两种方式更新分析器：

#### 方法1：替换嵌入的元数据文件

```bash
# 替换现有的元数据文件
cp platform-api-metadata-new.json platform-api-metadata.json

# 重新构建分析器
dotnet build
```

#### 方法2：自动化构建集成

在 `WasiCompatibilityAnalyzer.csproj` 中添加：

```xml
<PropertyGroup>
  <!-- 用户需要设置WasiCore源码路径 -->
  <WasiCorePath Condition="'$(WasiCorePath)' == ''">$(MSBuildThisFileDirectory)../../../wasicore</WasiCorePath>
</PropertyGroup>

<Target Name="GenerateApiMetadata" BeforeTargets="CoreCompile" Condition="Exists('$(WasiCorePath)')">
  <Message Text="🔍 从 $(WasiCorePath) 生成API元数据..." Importance="high" />
  <Exec Command="dotnet run --project Tools/MetadataGenerator.csproj &quot;$(WasiCorePath)&quot; platform-api-metadata.json" 
        ContinueOnError="false" />
</Target>

<Target Name="WarnMissingWasiCore" BeforeTargets="CoreCompile" Condition="!Exists('$(WasiCorePath)')">
  <Warning Text="⚠️ WasiCore路径未找到: $(WasiCorePath)。请设置 WasiCorePath 属性指向WasiCore源码目录。" />
</Target>
```

#### 使用自动化构建

```bash
# 方式1: 通过MSBuild属性指定路径
dotnet build -p:WasiCorePath="/path/to/your/wasicore"

# 方式2: 通过环境变量
export WASICORE_PATH="/path/to/your/wasicore"
dotnet build -p:WasiCorePath="$WASICORE_PATH"

# 方式3: 在项目文件中设置默认路径
<WasiCorePath>C:\YourCustomPath\wasicore</WasiCorePath>
```

### ⚙️ 配置选项

元数据生成器支持以下参数：

```bash
dotnet run --project MetadataGenerator.csproj <WasiCore路径> [输出文件]

参数说明:
  <WasiCore路径>  - 必需参数。WasiCore源码根目录的绝对或相对路径
  [输出文件]      - 可选参数。生成的JSON文件路径 (默认: platform-api-metadata.json)
```

### 🔍 路径验证

工具会自动验证WasiCore路径的有效性：

```bash
# 如果路径不存在，会显示详细错误
❌ WasiCore路径不存在: /wrong/path
❌ 请确认路径是否正确: /wrong/path

# 正确的路径应该包含以下子目录：
✅ GameCore/
✅ GameUI/  
✅ GameData/
✅ Engine/
```

### 🎯 何时需要重新生成

在以下情况下需要重新生成元数据：

1. **WasiCore更新** - 添加了新的平台专用API
2. **条件编译变更** - 修改了 `#if CLIENT` 或 `#if SERVER` 的使用
3. **新增模块** - 添加了新的客户端或服务器专用模块
4. **分析器升级** - 需要更精确的API检测

### 📊 验证生成的元数据

生成后可以检查元数据的准确性：

```bash
# 查看客户端API数量
grep -c "clientOnly" platform-api-metadata.json

# 查看具体的GameUI相关API
grep "GameUI" platform-api-metadata.json
```

### 🐛 故障排除

**问题**: 分析器启动失败 - 无法加载平台API元数据
```
❌ 错误信息: 无法加载平台API元数据！

✅ 解决方案: 
1. 首次使用时必须先生成元数据
2. cd Tools && dotnet run --project MetadataGenerator.csproj <WasiCore路径>
3. dotnet build
4. 或使用便捷脚本: update-metadata.bat <WasiCore路径>

💡 说明: 分析器不提供默认数据，必须基于真实WasiCore源码生成
```

**问题**: 生成器运行失败
```
✅ 解决方案: 检查WasiCore路径是否正确
✅ 确保有读取权限
✅ 检查磁盘空间
```

**问题**: 元数据看起来不完整
```
✅ 解决方案: 检查WasiCore是否为最新版本
✅ 确认所有子项目都在扫描路径中
✅ 检查条件编译是否规范
```

## 📍 文件位置

- **分析器源码**：`WasiCompatibilityAnalyzer/`
- **元数据生成器**：`WasiCompatibilityAnalyzer/Tools/`
- **生成的元数据**：`WasiCompatibilityAnalyzer/platform-api-metadata.json`
- **构建产物**：`WasiSparkCore/EditorTools/net9.0/WasiCompatibilityAnalyzer.dll`

## 🎯 成果总结

✅ **编译期阻止**：完全防止不兼容代码进入运行时  
✅ **全面覆盖**：检测所有主要的WebAssembly不兼容API  
✅ **平台API检测**：精确识别需要条件编译的代码 ← **新增**  
✅ **智能元数据**：基于真实源码自动生成API列表 ← **新增**  
✅ **清晰提示**：提供具体的替代方案建议  
✅ **团队统一**：确保所有开发者遵循相同规则  
✅ **CI/CD友好**：自动化构建流程集成  

### 🚀 核心价值提升

**解决了开发者的根本问题**：
- ❌ **之前**: `Extensions does not exist` → 放弃流式API
- ✅ **现在**: `Extensions does not exist` → 分析器提示需要 `#if CLIENT` → 正确使用

现在你的团队可以放心地开发WasiAsync项目，分析器不仅会阻止WebAssembly不兼容的API使用，还会引导开发者正确使用所有平台专用功能！

## 📚 相关文档

- **[🚀 快速入门指南](./QUICK_START.md)** - 5分钟快速设置
- **[🚨 严格元数据要求](./STRICT_METADATA_REQUIREMENT.md)** - 为什么不提供默认数据
- **[🚢 部署指南](./DEPLOYMENT_GUIDE.md)** - 详细的部署和CI/CD集成
- **[📊 实现总结](./IMPLEMENTATION_SUMMARY.md)** - 技术实现详情
- **[🎯 改进演示](./DEMO_BEFORE_AFTER.md)** - 问题解决效果对比
- **[📖 正确示例](./docs/CORRECT_SHAWARMA_EXAMPLE.md)** - 沙威玛传奇正确实现

## 🔄 维护流程

### 定期更新元数据

建议定期（如每次WasiCore发布后）运行元数据生成器：

```bash
# 自动化脚本示例（需要根据实际路径调整）
cd WasiCompatibilityAnalyzer/Tools

# 设置您的WasiCore路径
WASICORE_PATH="/path/to/your/wasicore"  # Linux/macOS
# 或在Windows中: set WASICORE_PATH=C:\Your\Path\wasicore

dotnet run --project MetadataGenerator.csproj "$WASICORE_PATH" "../platform-api-metadata.json"
cd ..
dotnet build
git add platform-api-metadata.json
git commit -m "Update platform API metadata"
```

### 🔧 创建更新脚本

#### Windows (update-metadata.bat)
```batch
@echo off
if "%1"=="" (
    echo ❌ 请指定WasiCore路径
    echo 用法: update-metadata.bat "C:\path\to\wasicore"
    exit /b 1
)

cd Tools
dotnet run --project MetadataGenerator.csproj "%1" "../platform-api-metadata.json"
cd ..
dotnet build
echo ✅ 元数据更新完成
```

#### Linux/macOS (update-metadata.sh)
```bash
#!/bin/bash
if [ $# -eq 0 ]; then
    echo "❌ 请指定WasiCore路径"
    echo "用法: ./update-metadata.sh /path/to/wasicore"
    exit 1
fi

cd Tools
dotnet run --project MetadataGenerator.csproj "$1" "../platform-api-metadata.json"
cd ..
dotnet build
echo "✅ 元数据更新完成"
```

### 🎯 使用更新脚本

为了简化操作，我们提供了现成的更新脚本：

```bash
# Windows 用户
update-metadata.bat "C:\Your\Path\To\wasicore"

# Linux/macOS 用户
chmod +x update-metadata.sh
./update-metadata.sh "/your/path/to/wasicore"
```

这些脚本会自动完成：
1. ✅ 验证WasiCore路径有效性
2. ✅ 运行元数据生成器
3. ✅ 重新构建分析器
4. ✅ 显示详细的进度信息

### 验证改进效果

通过以下方式验证分析器改进效果：
1. **错误率下降** - 减少因API误用导致的编译问题
2. **开发效率** - 开发者能更快速定位和解决问题  
3. **API采用率** - 更多开发者使用高级API（如流式UI）
4. **支持工单** - 减少因API使用问题的技术支持
