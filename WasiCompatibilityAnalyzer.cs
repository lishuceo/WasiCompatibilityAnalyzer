using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace WasiCompatibilityAnalyzer
{
    /// <summary>
    /// 平台API元数据缓存
    /// </summary>
    internal static class PlatformApiCache
    {
        public class ApiMetadata
        {
            // 🔧 移除命名空间级别的分类，只保留精确的类型和成员级别分析
            // public HashSet<string> ClientOnlyNamespaces { get; set; } = new();
            // public HashSet<string> ServerOnlyNamespaces { get; set; } = new();
            
            public HashSet<string> ClientOnlyTypes { get; set; } = new();
            public HashSet<string> ServerOnlyTypes { get; set; } = new();
            public HashSet<string> ClientOnlyMembers { get; set; } = new();
            public HashSet<string> ServerOnlyMembers { get; set; } = new();
            public Dictionary<string, string> MixedTypes { get; set; } = new();
            public Dictionary<string, object> Statistics { get; set; } = new();
        }

        private static readonly Lazy<ApiMetadata> _metadata = new(() => LoadMetadata());
        
        // 🔧 移除命名空间级别的属性，只保留精确的类型和成员级别访问
        // public static HashSet<string> ClientOnlyNamespaces => _metadata.Value.ClientOnlyNamespaces;
        // public static HashSet<string> ServerOnlyNamespaces => _metadata.Value.ServerOnlyNamespaces;
        
        public static HashSet<string> ClientOnlyTypes => _metadata.Value.ClientOnlyTypes;
        public static HashSet<string> ServerOnlyTypes => _metadata.Value.ServerOnlyTypes;
        public static HashSet<string> ClientOnlyMembers => _metadata.Value.ClientOnlyMembers;
        public static HashSet<string> ServerOnlyMembers => _metadata.Value.ServerOnlyMembers;
        public static Dictionary<string, string> MixedTypes => _metadata.Value.MixedTypes;

        private static ApiMetadata LoadMetadata()
        {
            try
            {
                // 尝试从嵌入资源加载
                var assembly = typeof(PlatformApiCache).Assembly;
                using var stream = assembly.GetManifestResourceStream("WasiCompatibilityAnalyzer.platform-api-metadata.json");
                
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var json = reader.ReadToEnd();
                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    var metadata = JsonSerializer.Deserialize<ApiMetadata>(json, options);
                    
                    if (metadata != null)
                    {
                        return metadata;
                    }
                }
                
                // 嵌入资源加载失败，抛出错误
                throw new InvalidOperationException(
                    "❌ 无法加载平台API元数据！\n" +
                    "📋 解决方案:\n" +
                    "1. 运行元数据生成器: cd Tools && dotnet run --project MetadataGenerator.csproj <WasiCore路径>\n" +
                    "2. 重新构建分析器: dotnet build\n" +
                    "3. 或使用便捷脚本: update-metadata.bat <WasiCore路径>\n\n" +
                    "⚠️ 不能使用默认数据，必须基于真实的WasiCore源码生成准确的元数据！");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "❌ 平台API元数据加载失败！\n" +
                    "📋 可能的原因:\n" +
                    "- 元数据文件未嵌入到分析器中\n" +
                    "- 元数据文件格式错误\n" +
                    "- 分析器版本与元数据不匹配\n\n" +
                    "🔧 解决方案:\n" +
                    "1. 重新生成元数据: cd Tools && dotnet run --project MetadataGenerator.csproj <WasiCore路径>\n" +
                    "2. 重新构建分析器: dotnet build\n\n" +
                    $"💥 详细错误: {ex.Message}");
            }
        }
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class WasiCompatibilityAnalyzer : DiagnosticAnalyzer
{
    #region 诊断规则定义
    
    public static readonly DiagnosticDescriptor TaskDelayRule = new(
        "WASI001",
        "不允许使用Task.Delay，请使用Game.Delay代替",
        "在WebAssembly环境中不应使用Task.Delay，请使用Game.Delay({0})代替",
        "WebAssembly兼容性",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Task.Delay在WebAssembly环境中不可用，会导致运行时错误。");

    public static readonly DiagnosticDescriptor TaskRunRule = new(
        "WASI002",
        "不允许使用Task.Run，WebAssembly不支持多线程",
        "在WebAssembly环境中不应使用Task.Run，请使用async/await模式代替",
        "WebAssembly兼容性",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Task.Run在WebAssembly环境中不可用，WebAssembly不支持多线程操作。");


    public static readonly DiagnosticDescriptor ThreadRule = new(
        "WASI003",
        "不允许使用Thread类，WebAssembly不支持多线程",
        "在WebAssembly环境中不应使用Thread.{0}，WebAssembly不支持多线程操作",
        "WebAssembly兼容性",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Thread类在WebAssembly环境中完全不可用。");

    public static readonly DiagnosticDescriptor ThreadPoolRule = new(
        "WASI004",
        "不允许使用ThreadPool类，WebAssembly不支持多线程",
        "在WebAssembly环境中不应使用ThreadPool.{0}，WebAssembly不支持线程池操作",
        "WebAssembly兼容性",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ThreadPool类在WebAssembly环境中完全不可用。");

    public static readonly DiagnosticDescriptor ParallelRule = new(
        "WASI005",
        "不允许使用Parallel类，WebAssembly不支持并行处理",
        "在WebAssembly环境中不应使用Parallel.{0}，请使用顺序异步处理代替",
        "WebAssembly兼容性",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Parallel类在WebAssembly环境中不可用，不支持并行处理。");

    public static readonly DiagnosticDescriptor FileSystemRule = new(
        "WASI006", 
        "不允许直接使用文件系统API，请使用框架提供的方法",
        "在WebAssembly环境中不应使用{0}，请使用Game.FileSystem或相关框架方法",
        "WebAssembly兼容性",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "直接的文件系统访问在WebAssembly环境中受限。");

    public static readonly DiagnosticDescriptor NetworkingRule = new(
        "WASI007",
        "不允许直接使用网络API，请使用框架提供的方法", 
        "在WebAssembly环境中不应使用{0}，请使用Game.Network或相关框架方法",
        "WebAssembly兼容性",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "直接的网络访问在WebAssembly环境中受限。");

    public static readonly DiagnosticDescriptor ProcessRule = new(
        "WASI008",
        "不允许使用Process类，WebAssembly不支持进程操作",
        "在WebAssembly环境中不应使用Process.{0}，WebAssembly不支持进程操作",
        "WebAssembly兼容性",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Process类在WebAssembly环境中完全不可用。");

    public static readonly DiagnosticDescriptor RegistryRule = new(
        "WASI009",
        "不允许使用Registry类，WebAssembly不支持注册表访问",
        "在WebAssembly环境中不应使用Registry.{0}，WebAssembly不支持注册表操作",
        "WebAssembly兼容性",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Registry类在WebAssembly环境中不可用。");

    public static readonly DiagnosticDescriptor TimerRule = new(
        "WASI010",
        "不允许使用System.Timers.Timer，请使用Game.CreateTimer代替",
        "在WebAssembly环境中不应使用System.Timers.Timer，请使用Game.CreateTimer代替",
        "WebAssembly兼容性",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "System.Timers.Timer在WebAssembly环境中不可用。");

    public static readonly DiagnosticDescriptor ObsoleteApiRule = new(
        "WASI011",
        "不允许使用已过时的API",
        "不建议在WebAssembly环境中使用已过时的API '{0}': {1}",
        "WebAssembly兼容性",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "过时的API可能在WebAssembly环境中存在兼容性问题，建议使用推荐的替代方案。");

    public static readonly DiagnosticDescriptor HiddenApiRule = new(
        "WASI012",
        "不允许使用编辑器隐藏的内部API",
        "不应在WebAssembly环境中使用编辑器隐藏的内部API '{0}'，这些API为内部实现细节",
        "WebAssembly兼容性",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "编辑器隐藏的API通常为内部实现细节，在WebAssembly环境中使用可能导致不可预期的行为。");

    public static readonly DiagnosticDescriptor ClientOnlyApiRule = new(
        "WASI013",
        "客户端专用API需要#if CLIENT",
        "客户端专用API '{0}' 必须在 #if CLIENT 预处理指令内使用",
        "平台专用API",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "此API仅在客户端可用，必须使用 #if CLIENT 进行条件编译，否则服务器编译会失败。");

    public static readonly DiagnosticDescriptor ServerOnlyApiRule = new(
        "WASI014",
        "服务器专用API需要#if SERVER",
        "服务器专用API '{0}' 必须在 #if SERVER 预处理指令内使用",
        "平台专用API",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "此API仅在服务器可用，必须使用 #if SERVER 进行条件编译，否则客户端编译会失败。");

    public static readonly DiagnosticDescriptor GameModeNotInitializedRule = new(
        "WASI015",
        "GameMode未初始化",
        "GameMode '{0}' 已定义但未创建对应的 GameDataGameMode 实例，这将导致运行时错误",
        "游戏数据完整性",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "每个定义的 GameMode 必须有对应的 GameDataGameMode 实例，否则在运行时使用该 GameMode 会报错：'Game Mode is set to XXX, but the data is not set, using default game mode'",
        customTags: "CompilationEnd");

    #endregion

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            TaskDelayRule, TaskRunRule, ThreadRule, ThreadPoolRule, 
            ParallelRule, FileSystemRule, NetworkingRule, ProcessRule, RegistryRule, TimerRule,
            ObsoleteApiRule, HiddenApiRule, ClientOnlyApiRule, ServerOnlyApiRule, GameModeNotInitializedRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterCompilationAction(AnalyzeGameModeInitialization);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        
        if (memberAccess == null) return;

        var memberName = memberAccess.Name.Identifier.ValueText;
        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
        
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol) return;
        
        var containingType = methodSymbol.ContainingType;
        if (containingType == null) return;

        // 检查特性
        CheckSymbolAttributes(context, invocation, methodSymbol);
        
        // 检查平台专用API
        CheckPlatformSpecificAPI(context, invocation, containingType);
        
        CheckRestrictedAPI(context, invocation, containingType, memberName);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;
        var symbolInfo = context.SemanticModel.GetSymbolInfo(objectCreation);
        
        if (symbolInfo.Symbol is not IMethodSymbol constructorSymbol) return;
        
        var containingType = constructorSymbol.ContainingType;
        if (containingType == null) return;

        // 检查构造函数特性
        CheckSymbolAttributes(context, objectCreation, constructorSymbol);
        
        // 检查平台专用API
        CheckPlatformSpecificAPI(context, objectCreation, containingType);
        
        CheckRestrictedTypeCreation(context, objectCreation, containingType);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
        
        if (symbolInfo.Symbol?.ContainingType == null) return;
        
        var containingType = symbolInfo.Symbol.ContainingType;
        var memberName = memberAccess.Name.Identifier.ValueText;

        // 检查成员特性
        CheckSymbolAttributes(context, memberAccess, symbolInfo.Symbol);
        
        // 检查平台专用API
        CheckPlatformSpecificAPI(context, memberAccess, containingType);
        
        CheckRestrictedMemberAccess(context, memberAccess, containingType, memberName);
    }

    private static void CheckRestrictedAPI(SyntaxNodeAnalysisContext context, SyntaxNode node, 
        INamedTypeSymbol containingType, string memberName)
    {
        var typeFullName = GetTypeFullName(containingType);
        
        switch (typeFullName)
        {
            case "System.Threading.Tasks.Task":
                CheckTaskMethods(context, node, memberName);
                break;
                
            case "System.Threading.Thread":
                CheckThreadMethods(context, node, memberName);
                break;
                
            case "System.Threading.ThreadPool":
                ReportDiagnostic(context, ThreadPoolRule, node, memberName);
                break;
                
            case "System.Threading.Tasks.Parallel":
                ReportDiagnostic(context, ParallelRule, node, memberName);
                break;
                
            case "System.IO.File":
            case "System.IO.Directory":
            case "System.IO.FileStream":
            case "System.IO.StreamReader":
            case "System.IO.StreamWriter":
                ReportDiagnostic(context, FileSystemRule, node, typeFullName);
                break;
                
            case "System.Net.HttpClient":
            case "System.Net.WebClient":
            case "System.Net.Sockets.TcpClient":
            case "System.Net.Sockets.UdpClient":
                ReportDiagnostic(context, NetworkingRule, node, typeFullName);
                break;
                
            case "System.Diagnostics.Process":
                ReportDiagnostic(context, ProcessRule, node, memberName);
                break;
                
            case "Microsoft.Win32.Registry":
                ReportDiagnostic(context, RegistryRule, node, memberName);
                break;
        }
    }

    private static void CheckRestrictedTypeCreation(SyntaxNodeAnalysisContext context, SyntaxNode node, 
        INamedTypeSymbol namedType)
    {
        var typeFullName = GetTypeFullName(namedType);
        
        switch (typeFullName)
        {
            case "System.Threading.Thread":
                ReportDiagnostic(context, ThreadRule, node, "constructor");
                break;
                
            case "System.Timers.Timer":
                ReportDiagnostic(context, TimerRule, node);
                break;
                
            case "System.Threading.Tasks.Task":
                // 检查Task构造函数，某些重载也不支持
                ReportDiagnostic(context, TaskRunRule, node);
                break;
                
            case "System.IO.FileStream":
            case "System.IO.StreamReader": 
            case "System.IO.StreamWriter":
                ReportDiagnostic(context, FileSystemRule, node, typeFullName);
                break;
                
            case "System.Net.HttpClient":
            case "System.Net.WebClient":
            case "System.Net.Sockets.TcpClient":
            case "System.Net.Sockets.UdpClient":
                ReportDiagnostic(context, NetworkingRule, node, typeFullName);
                break;
                
            case "System.Diagnostics.Process":
                ReportDiagnostic(context, ProcessRule, node, "constructor");
                break;
        }
    }

    private static void CheckRestrictedMemberAccess(SyntaxNodeAnalysisContext context, SyntaxNode node,
        INamedTypeSymbol containingType, string memberName)
    {
        var typeFullName = GetTypeFullName(containingType);
        
        // 检查静态成员访问和实例成员访问
        switch (typeFullName)
        {
            case "System.Threading.Thread":
                CheckThreadMemberAccess(context, node, memberName);
                break;
                
            case "System.Environment" when IsRestrictedEnvironmentMember(memberName):
                ReportDiagnostic(context, FileSystemRule, node, $"Environment.{memberName}");
                break;
        }
    }

    private static void CheckTaskMethods(SyntaxNodeAnalysisContext context, SyntaxNode node, string memberName)
    {
        switch (memberName)
        {
            case "Delay":
                ReportDiagnostic(context, TaskDelayRule, node, "Game.Delay()");
                break;
            case "Run":
            case "Factory":
                ReportDiagnostic(context, TaskRunRule, node);
                break;
        }
    }

    private static void CheckThreadMethods(SyntaxNodeAnalysisContext context, SyntaxNode node, string memberName)
    {
        // 区分危险和相对安全的Thread操作
        switch (memberName)
        {
            // 危险的多线程操作 - 报错
            case "Start":
            case "Join":
            case "Abort":
            case "Interrupt":
            case "Resume":
            case "Suspend":
            case "Sleep":
            case "Yield":
                ReportDiagnostic(context, ThreadRule, node, memberName);
                break;
                
            // 相对安全的信息获取操作 - 允许
            case "CurrentThread":
            case "ManagedThreadId":
            case "Name":
            case "IsAlive":
            case "IsBackground":
            case "ThreadState":
            case "Priority":
            case "IsThreadPoolThread":
                // 不报错，允许使用这些只读信息
                break;
                
            // 其他未明确分类的操作 - 报错
            default:
                ReportDiagnostic(context, ThreadRule, node, memberName);
                break;
        }
    }

    private static void CheckThreadMemberAccess(SyntaxNodeAnalysisContext context, SyntaxNode node, string memberName)
    {
        // 静态成员访问和实例成员访问检查
        switch (memberName)
        {
            // 危险的静态方法 - 报错
            case "Sleep":
            case "Yield":
            case "VolatileRead":
            case "VolatileWrite":
            case "MemoryBarrier":
            case "SpinWait":
                ReportDiagnostic(context, ThreadRule, node, memberName);
                break;
                
            // 相对安全的静态和实例属性 - 允许
            case "CurrentThread":
            case "ManagedThreadId":
            case "Name":
            case "IsAlive":
            case "IsBackground":
            case "ThreadState":
            case "Priority":
            case "IsThreadPoolThread":
                // 不报错，允许获取线程信息
                break;
                
            // 其他未明确分类的操作 - 报错
            default:
                ReportDiagnostic(context, ThreadRule, node, memberName);
                break;
        }
    }

    private static bool IsRestrictedEnvironmentMember(string memberName)
    {
        return memberName switch
        {
            "CurrentDirectory" => true,
            "GetFolderPath" => true,
            "GetEnvironmentVariable" => true,
            "GetEnvironmentVariables" => true,
            _ => false
        };
    }

    private static string GetTypeFullName(INamedTypeSymbol namedType)
    {
        if (namedType.ContainingNamespace?.IsGlobalNamespace == false)
        {
            return $"{namedType.ContainingNamespace}.{namedType.Name}";
        }
        return namedType.Name;
    }

    private static void CheckSymbolAttributes(SyntaxNodeAnalysisContext context, SyntaxNode node, ISymbol symbol)
    {
        if (symbol == null) return;

        var attributes = symbol.GetAttributes();
        
        foreach (var attribute in attributes)
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null) continue;

            var attributeName = GetTypeFullName(attributeClass);
            
            // 检查 ObsoleteAttribute
            if (attributeName == "System.ObsoleteAttribute")
            {
                var message = attribute.ConstructorArguments.Length > 0 
                    ? attribute.ConstructorArguments[0].Value?.ToString() ?? ""
                    : "此API已过时";
                    
                var symbolName = GetSymbolDisplayName(symbol);
                ReportDiagnostic(context, ObsoleteApiRule, node, symbolName, message);
            }
            
            // 检查 EditorBrowsableAttribute
            else if (attributeName == "System.ComponentModel.EditorBrowsableAttribute")
            {
                if (attribute.ConstructorArguments.Length > 0)
                {
                    var browseState = attribute.ConstructorArguments[0].Value;
                    // EditorBrowsableState.Never = 1
                    if (browseState is int state && state == 1)
                    {
                        var symbolName = GetSymbolDisplayName(symbol);
                        ReportDiagnostic(context, HiddenApiRule, node, symbolName);
                    }
                }
            }
        }
    }

    private static string GetSymbolDisplayName(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol method => $"{GetTypeFullName(method.ContainingType)}.{method.Name}",
            IPropertySymbol property => $"{GetTypeFullName(property.ContainingType)}.{property.Name}",
            IFieldSymbol field => $"{GetTypeFullName(field.ContainingType)}.{field.Name}",
            _ => symbol.Name
        };
    }

    private static void CheckPlatformSpecificAPI(SyntaxNodeAnalysisContext context, SyntaxNode node, INamedTypeSymbol containingType)
    {
        if (containingType == null) return;
        
        var namespaceName = containingType.ContainingNamespace?.ToString() ?? "";
        var typeFullName = GetTypeFullName(containingType);
        
        // 检查是否为客户端专用API
        if (IsClientOnlyAPI(namespaceName, typeFullName))
        {
            if (!IsInConditionalCompilation(context, node, "CLIENT"))
            {
                ReportDiagnostic(context, ClientOnlyApiRule, node, typeFullName);
                return;
            }
        }
        // 检查是否为服务器专用API
        else if (IsServerOnlyAPI(namespaceName, typeFullName))
        {
            if (!IsInConditionalCompilation(context, node, "SERVER"))
            {
                ReportDiagnostic(context, ServerOnlyApiRule, node, typeFullName);
                return;
            }
        }
        
        // 检查成员级别的平台特性
        CheckMemberSpecificAPI(context, node, typeFullName);
    }

    private static void CheckMemberSpecificAPI(SyntaxNodeAnalysisContext context, SyntaxNode node, string typeFullName)
    {
        // 如果是混合类型，需要检查具体的成员
        if (!PlatformApiCache.MixedTypes.ContainsKey(typeFullName))
            return;

        // 获取被访问的成员名称
        string? memberName = null;
        
        if (node is InvocationExpressionSyntax invocation && 
            invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            memberName = memberAccess.Name.Identifier.ValueText;
        }
        else if (node is MemberAccessExpressionSyntax access)
        {
            memberName = access.Name.Identifier.ValueText;
        }

        if (memberName == null) return;

        var fullMemberName = $"{typeFullName}.{memberName}";
        
        // 检查是否为客户端专用成员
        if (PlatformApiCache.ClientOnlyMembers.Contains(fullMemberName))
        {
            if (!IsInConditionalCompilation(context, node, "CLIENT"))
            {
                ReportDiagnostic(context, ClientOnlyApiRule, node, fullMemberName);
            }
        }
        // 检查是否为服务器专用成员
        else if (PlatformApiCache.ServerOnlyMembers.Contains(fullMemberName))
        {
            if (!IsInConditionalCompilation(context, node, "SERVER"))
            {
                ReportDiagnostic(context, ServerOnlyApiRule, node, fullMemberName);
            }
        }
    }
    
    private static bool IsClientOnlyAPI(string namespaceName, string typeFullName)
    {
        // 🔧 严谨的检查：只基于精确的元数据，移除假设性判断
        
        // 1. 检查具体类型（最精确）
        if (PlatformApiCache.ClientOnlyTypes.Contains(typeFullName))
        {
            return true;
        }
        
        // 2. 检查混合类型（需要进一步检查成员）
        if (PlatformApiCache.MixedTypes.ContainsKey(typeFullName))
        {
            return false; // 混合类型需要成员级别检查
        }
        
        // 🔧 已移除：命名空间级别的检查
        // 原因：完全依赖精确的类型和成员列表，避免假设性判断
        
        return false;
    }
    
    private static bool IsServerOnlyAPI(string namespaceName, string typeFullName)
    {
        // 🔧 严谨的检查：只基于精确的元数据，移除假设性判断
        
        // 1. 检查具体类型（最精确）
        if (PlatformApiCache.ServerOnlyTypes.Contains(typeFullName))
        {
            return true;
        }
        
        // 2. 检查混合类型（需要进一步检查成员）
        if (PlatformApiCache.MixedTypes.ContainsKey(typeFullName))
        {
            return false; // 混合类型需要成员级别检查
        }
        
        // 🔧 已移除：命名空间级别的检查
        // 原因：完全依赖精确的类型和成员列表，避免假设性判断
        
        return false;
    }

    // 🔧 已移除：ShouldUseNamespaceAsBackup 和 IsConfigurationNamespace 方法
    // 原因：分析器现在完全依赖精确的类型和成员列表，不再使用命名空间级别的判断
    
    private static bool IsInConditionalCompilation(SyntaxNodeAnalysisContext context, SyntaxNode node, string symbol)
    {
        var currentNode = node;
        
        while (currentNode != null)
        {
            // 查找包含此节点的所有前置指令
            var directives = currentNode.GetLeadingTrivia()
                .Where(trivia => trivia.IsDirective)
                .Select(trivia => trivia.GetStructure())
                .OfType<DirectiveTriviaSyntax>();
            
            foreach (var directive in directives)
            {
                if (directive is IfDirectiveTriviaSyntax ifDirective)
                {
                    var condition = ifDirective.Condition?.ToString();
                    if (condition?.Contains(symbol) == true)
                    {
                        // 检查是否有对应的#endif
                        var endIfFound = CheckForMatchingEndIf(currentNode, node);
                        if (endIfFound)
                            return true;
                    }
                }
            }
            
            currentNode = currentNode.Parent;
        }
        
        // 另一种方法：检查节点是否在条件编译范围内
        var root = node.SyntaxTree.GetRoot();
        var position = node.SpanStart;
        
        var allDirectives = root.DescendantTrivia()
            .Where(t => t.IsDirective)
            .Select(t => t.GetStructure())
            .OfType<DirectiveTriviaSyntax>()
            .OrderBy(d => d.SpanStart)
            .ToList();
        
        var activeConditions = new Stack<string>();
        
        foreach (var directive in allDirectives)
        {
            if (directive.SpanStart > position)
                break;
                
            switch (directive)
            {
                case IfDirectiveTriviaSyntax ifDir:
                    var cond = ifDir.Condition?.ToString();
                    if (cond?.Contains(symbol) == true)
                        activeConditions.Push(symbol);
                    else
                        activeConditions.Push("");
                    break;
                    
                case ElifDirectiveTriviaSyntax elifDir:
                    if (activeConditions.Count > 0)
                        activeConditions.Pop();
                    var elifCond = elifDir.Condition?.ToString();
                    if (elifCond?.Contains(symbol) == true)
                        activeConditions.Push(symbol);
                    else
                        activeConditions.Push("");
                    break;
                    
                case ElseDirectiveTriviaSyntax:
                    if (activeConditions.Count > 0)
                    {
                        var prev = activeConditions.Pop();
                        activeConditions.Push(prev == symbol ? "" : "else");
                    }
                    break;
                    
                case EndIfDirectiveTriviaSyntax:
                    if (activeConditions.Count > 0)
                        activeConditions.Pop();
                    break;
            }
        }
        
        return activeConditions.Any(c => c == symbol);
    }
    
    private static bool CheckForMatchingEndIf(SyntaxNode startNode, SyntaxNode targetNode)
    {
        // 简化的检查：假设如果找到了#if指令，则认为它有对应的#endif
        // 在实际应用中，可能需要更复杂的逻辑来确保配对正确
        return true;
    }

    /// <summary>
    /// 分析GameMode是否都已正确初始化
    /// 检查所有定义的GameMode是否都有对应的GameDataGameMode实例
    /// </summary>
    private static void AnalyzeGameModeInitialization(CompilationAnalysisContext context)
    {
        // 存储定义的GameMode及其位置
        var definedGameModes = new Dictionary<string, Location>();
        
        // 存储已实例化的GameMode
        var initializedGameModes = new HashSet<string>();

        foreach (var syntaxTree in context.Compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot(context.CancellationToken);

            // 1. 查找所有定义的GameMode字段
            // 🔧 改进：不限制在ScopeData类中，查找所有GameLink<GameDataGameMode>字段
            var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
            
            foreach (var field in fields)
            {
                // 使用语法检查而不是语义检查，以提高可靠性
                var fieldTypeSyntax = field.Declaration.Type.ToString();
                
                // 检查是否是GameLink<GameDataGameMode, GameDataGameMode>类型
                // 使用Contains而不是精确匹配，以兼容不同的命名空间前缀
                if (fieldTypeSyntax.Contains("GameLink<") && 
                    fieldTypeSyntax.Contains("GameDataGameMode"))
                {
                    // 获取字段名
                    foreach (var variable in field.Declaration.Variables)
                    {
                        var fieldName = variable.Identifier.ValueText;
                        
                        // 避免重复（如果在多个partial class中重复定义）
                        if (!definedGameModes.ContainsKey(fieldName))
                        {
                            definedGameModes[fieldName] = variable.GetLocation();
                        }
                    }
                }
            }

            // 2. 查找所有GameDataGameMode的实例化
            var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
            
            foreach (var creation in objectCreations)
            {
                // 先检查语法层面的类型名称
                var creationType = creation.Type;
                var syntaxTypeName = creationType.ToString();
                
                // 如果语法上看起来像GameDataGameMode，则进一步检查
                if (syntaxTypeName.Contains("GameDataGameMode"))
                {
                    // 获取构造函数参数（GameMode.XXX）
                    if (creation.ArgumentList?.Arguments.Count > 0)
                    {
                        var firstArg = creation.ArgumentList.Arguments[0].Expression;
                        
                        // 提取GameMode字段名
                        if (firstArg is MemberAccessExpressionSyntax memberAccess)
                        {
                            var gameModeName = memberAccess.Name.Identifier.ValueText;
                            initializedGameModes.Add(gameModeName);
                        }
                        else if (firstArg is IdentifierNameSyntax identifier)
                        {
                            initializedGameModes.Add(identifier.Identifier.ValueText);
                        }
                    }
                }
            }
        }

        // 3. 对比检查：找出定义了但未初始化的GameMode
        foreach (var definedMode in definedGameModes)
        {
            var gameModeName = definedMode.Key;
            var location = definedMode.Value;
            
            if (!initializedGameModes.Contains(gameModeName))
            {
                var diagnostic = Diagnostic.Create(
                    GameModeNotInitializedRule, 
                    location, 
                    gameModeName);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, 
        DiagnosticDescriptor rule, SyntaxNode node, params object[] args)
    {
        var diagnostic = Diagnostic.Create(rule, node.GetLocation(), args);
        context.ReportDiagnostic(diagnostic);
    }
    }
}
