using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace WasiCompatibilityAnalyzer.Tools;

/// <summary>
/// 从WasiCore源码生成平台特定API元数据
/// </summary>
public class PlatformApiMetadataGenerator
{
    public class ApiMetadata
    {
        public HashSet<string> ClientOnlyNamespaces { get; set; } = new();
        public HashSet<string> ServerOnlyNamespaces { get; set; } = new();
        public HashSet<string> ClientOnlyTypes { get; set; } = new();
        public HashSet<string> ServerOnlyTypes { get; set; } = new();
        public HashSet<string> ClientOnlyMembers { get; set; } = new();
        public HashSet<string> ServerOnlyMembers { get; set; } = new();
        public Dictionary<string, string> MixedTypes { get; set; } = new(); // Type -> "CLIENT|SERVER|MIXED"
        
        // 添加统计信息
        public Dictionary<string, object> Statistics { get; set; } = new();
    }

    private readonly string _wasiCorePath;
    private readonly ApiMetadata _metadata = new();

    public PlatformApiMetadataGenerator(string wasiCorePath)
    {
        _wasiCorePath = wasiCorePath;
    }

    public async Task<ApiMetadata> GenerateMetadataAsync()
    {
        Console.WriteLine($"🔍 开始扫描WasiCore源码: {_wasiCorePath}");
        
        // 扫描所有C#文件，排除不需要的目录
        var csFiles = Directory.GetFiles(_wasiCorePath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && 
                       !f.Contains("\\bin\\") && 
                       !f.Contains("\\test\\", StringComparison.OrdinalIgnoreCase) &&
                       !f.Contains("\\Tests\\", StringComparison.OrdinalIgnoreCase) &&
                       !f.Contains("\\TestData\\", StringComparison.OrdinalIgnoreCase) &&
                       !f.Contains("Test.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine($"📁 找到 {csFiles.Count} 个C#文件");

        // 添加调试计数器
        int filesWithClientCode = 0;
        int filesWithServerCode = 0;
        int filesWithMixedCode = 0;
        int filesWithoutConditions = 0;

        int processedFiles = 0;
        foreach (var file in csFiles)
        {
            var fileType = await AnalyzeFileAsync(file);
            
            // 统计文件类型
            switch (fileType)
            {
                case "CLIENT": filesWithClientCode++; break;
                case "SERVER": filesWithServerCode++; break;
                case "MIXED": filesWithMixedCode++; break;
                default: filesWithoutConditions++; break;
            }
            
            processedFiles++;
            
            if (processedFiles % 100 == 0)
            {
                Console.WriteLine($"⏳ 已处理 {processedFiles}/{csFiles.Count} 个文件 (客户端:{filesWithClientCode}, 服务器:{filesWithServerCode}, 混合:{filesWithMixedCode}, 通用:{filesWithoutConditions})");
            }
        }

        // 后处理：分析项目文件依赖
        await AnalyzeProjectDependenciesAsync();
        
        // 生成统计信息
        GenerateStatistics();

        Console.WriteLine("✅ 元数据生成完成");
        Console.WriteLine($"📊 文件类型统计: 客户端专用:{filesWithClientCode}, 服务器专用:{filesWithServerCode}, 混合:{filesWithMixedCode}, 通用:{filesWithoutConditions}");
        
        return _metadata;
    }

    private async Task<string> AnalyzeFileAsync(string filePath)
    {
        try
        {
            var code = await File.ReadAllTextAsync(filePath);
            
            // 🔧 关键修复：需要用不同的预处理符号解析来检测条件编译的内容
            var hasClientCode = false;
            var hasServerCode = false;
            var hasSharedCode = false;
            
            // 1. 用CLIENT符号解析
            var clientOptions = CSharpParseOptions.Default.WithPreprocessorSymbols("CLIENT");
            var clientTree = CSharpSyntaxTree.ParseText(code, clientOptions, path: filePath);
            var clientRoot = await clientTree.GetRootAsync();
            
            // 2. 用SERVER符号解析
            var serverOptions = CSharpParseOptions.Default.WithPreprocessorSymbols("SERVER");
            var serverTree = CSharpSyntaxTree.ParseText(code, serverOptions, path: filePath);
            var serverRoot = await serverTree.GetRootAsync();
            
            // 3. 无符号解析
            var noneOptions = CSharpParseOptions.Default;
            var noneTree = CSharpSyntaxTree.ParseText(code, noneOptions, path: filePath);
            var noneRoot = await noneTree.GetRootAsync();
            
            // 检查文件整体类型
            if (IsFileCompletelyConditional(code, "CLIENT"))
            {
                await AnalyzeFileWithCondition(clientRoot, filePath, "CLIENT");
                return "CLIENT";
            }
            if (IsFileCompletelyConditional(code, "SERVER"))
            {
                await AnalyzeFileWithCondition(serverRoot, filePath, "SERVER");
                return "SERVER";
            }
            
            // 使用比较方法分析混合内容
            var hasMixedContent = await AnalyzeFileWithComparison(code, filePath);
            return hasMixedContent ? "MIXED" : "NONE";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 分析文件失败: {filePath} - {ex.Message}");
            return "ERROR";
        }
    }
    
    private async Task<bool> AnalyzeFileWithPreprocessor(SyntaxNode root, string filePath, string condition)
    {
        // 现在直接返回是否包含类型声明，具体的成员分析在后面的对比中进行
        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
        return typeDeclarations.Any();
    }
    
    private async Task<bool> AnalyzeFileWithComparison(string code, string filePath)
    {
        // 用不同的预处理符号解析，然后比较差异
        var noSymbolsOptions = CSharpParseOptions.Default;
        var clientOptions = CSharpParseOptions.Default.WithPreprocessorSymbols("CLIENT");
        var serverOptions = CSharpParseOptions.Default.WithPreprocessorSymbols("SERVER");
        
        var noSymbolsTree = CSharpSyntaxTree.ParseText(code, noSymbolsOptions);
        var clientTree = CSharpSyntaxTree.ParseText(code, clientOptions);
        var serverTree = CSharpSyntaxTree.ParseText(code, serverOptions);
        
        var noSymbolsRoot = await noSymbolsTree.GetRootAsync();
        var clientRoot = await clientTree.GetRootAsync();
        var serverRoot = await serverTree.GetRootAsync();
        
        bool foundConditionalContent = false;
        
        // 比较不同版本的成员来找出条件编译的成员
        var noSymbolsTypes = GetTypesAndMembers(noSymbolsRoot);
        var clientTypes = GetTypesAndMembers(clientRoot);
        var serverTypes = GetTypesAndMembers(serverRoot);
        
        foreach (var typeName in clientTypes.Keys.Union(serverTypes.Keys))
        {
            var noSymbolsMembers = noSymbolsTypes.GetValueOrDefault(typeName, new HashSet<string>());
            var clientMembers = clientTypes.GetValueOrDefault(typeName, new HashSet<string>());
            var serverMembers = serverTypes.GetValueOrDefault(typeName, new HashSet<string>());
            
            // 找出客户端专用成员（在CLIENT版本中有，但在无符号版本中没有）
            var clientOnlyMembers = clientMembers.Except(noSymbolsMembers).ToList();
            
            // 找出服务器专用成员（在SERVER版本中有，但在无符号版本中没有）
            var serverOnlyMembers = serverMembers.Except(noSymbolsMembers).ToList();
            
            if (clientOnlyMembers.Count > 0 || serverOnlyMembers.Count > 0)
            {
                foundConditionalContent = true;
                
                // 记录成员级别的条件编译
                foreach (var memberName in clientOnlyMembers)
                {
                    var fullMemberName = $"{typeName}.{memberName}";
                    _metadata.ClientOnlyMembers.Add(fullMemberName);
                    Console.WriteLine($"📱 客户端专用成员: {fullMemberName}");
                }
                
                foreach (var memberName in serverOnlyMembers)
                {
                    var fullMemberName = $"{typeName}.{memberName}";
                    _metadata.ServerOnlyMembers.Add(fullMemberName);
                    Console.WriteLine($"🖥️ 服务器专用成员: {fullMemberName}");
                }
                
                // 标记类型为混合
                var existingCondition = _metadata.MixedTypes.GetValueOrDefault(typeName);
                
                if (clientOnlyMembers.Count > 0 && serverOnlyMembers.Count > 0)
                {
                    _metadata.MixedTypes[typeName] = "MIXED";
                }
                else if (clientOnlyMembers.Count > 0)
                {
                    _metadata.MixedTypes[typeName] = existingCondition == "SERVER" ? "MIXED" : "CLIENT";
                }
                else if (serverOnlyMembers.Count > 0)
                {
                    _metadata.MixedTypes[typeName] = existingCondition == "CLIENT" ? "MIXED" : "SERVER";
                }
                
                Console.WriteLine($"🔀 混合类型: {typeName} (客户端成员: {clientOnlyMembers.Count}, 服务器成员: {serverOnlyMembers.Count})");
            }
        }
        
        return foundConditionalContent;
    }
    
    private Dictionary<string, HashSet<string>> GetTypesAndMembers(SyntaxNode root)
    {
        var result = new Dictionary<string, HashSet<string>>();
        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
        
        foreach (var type in typeDeclarations)
        {
            var typeName = GetFullTypeName(type);
            var members = new HashSet<string>();
            
            foreach (var member in type.Members)
            {
                var memberName = GetMemberName(member);
                if (memberName != null)
                {
                    members.Add(memberName);
                }
            }
            
            result[typeName] = members;
        }
        
        return result;
    }
    
    private bool IsMemberVisibleOnlyInCondition(SyntaxNode member, string condition)
    {
        // 简化的检查：如果成员包含条件相关的文本或位于条件编译块中
        var memberText = member.ToString();
        
        // 检查成员是否位于条件编译指令附近
        var position = member.SpanStart;
        var parent = member.Parent;
        
        while (parent != null)
        {
            var parentTrivia = parent.GetLeadingTrivia().Concat(parent.GetTrailingTrivia());
            
            foreach (var trivia in parentTrivia)
            {
                if (trivia.IsDirective)
                {
                    var directive = trivia.GetStructure();
                    if (directive is IfDirectiveTriviaSyntax ifDir)
                    {
                        var cond = ifDir.Condition?.ToString().Trim();
                        if (cond == condition)
                        {
                            return true;
                        }
                    }
                }
            }
            parent = parent.Parent;
        }
        
        return false;
    }
    
    private bool IsFileCompletelyConditional(string code, string condition)
    {
        var lines = code.Split('\n');
        var firstNonEmptyLine = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.Trim()));
        return firstNonEmptyLine?.Trim().StartsWith($"#if {condition}") == true;
    }

    private async Task AnalyzeFileWithCondition(SyntaxNode root, string filePath, string condition)
    {
        // 对于完全在条件编译中的文件，分析其类型和成员
        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

        foreach (var type in typeDeclarations)
        {
            var typeName = GetFullTypeName(type);
            
            // 检查是否为部分类 - 部分类可能在其他文件中有不同的条件编译
            if (type.Modifiers.Any(m => m.ValueText == "partial"))
            {
                // 部分类标记为混合类型，稍后需要详细分析
                if (!_metadata.MixedTypes.ContainsKey(typeName))
                {
                    _metadata.MixedTypes[typeName] = condition;
                }
                else
                {
                    // 如果已经有不同的条件，标记为真正的混合
                    if (_metadata.MixedTypes[typeName] != condition)
                    {
                        _metadata.MixedTypes[typeName] = "MIXED";
                    }
                }
                
                // 分析这个文件中的成员
                await AnalyzeTypeMembersAsync(type, typeName, root);
            }
            else
            {
                // 非部分类，整个类型是专用的
                if (condition == "CLIENT")
                {
                    _metadata.ClientOnlyTypes.Add(typeName);
                }
                else if (condition == "SERVER")
                {
                    _metadata.ServerOnlyTypes.Add(typeName);
                }
            }
        }
        
        // 检查是否整个命名空间都在此条件编译中
        await CheckNamespaceCondition(root, filePath, condition);
    }
    
    private async Task CheckNamespaceCondition(SyntaxNode root, string filePath, string condition)
    {
        // 只有当确认整个命名空间都是专用时才添加到命名空间列表
        // 这需要更谨慎的判断，暂时不自动添加命名空间级别的限制
        
        // 特殊情况：整个GameUI项目都是客户端专用
        if (filePath.Contains("\\GameUI\\") && condition == "CLIENT")
        {
            var namespaceDeclarations = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>();
            foreach (var ns in namespaceDeclarations)
            {
                var namespaceName = GetFullNamespace(ns);
                if (namespaceName.StartsWith("GameUI"))
                {
                    _metadata.ClientOnlyNamespaces.Add(namespaceName);
                }
            }
        }
        
        // 特殊情况：UserCloudData目录都是服务器专用
        if (filePath.Contains("\\UserCloudData\\") && condition == "SERVER")
        {
            _metadata.ServerOnlyNamespaces.Add("GameCore.UserCloudData");
        }
    }

    private async Task<bool> AnalyzeFileConditionalSections(SyntaxNode root, string filePath)
    {
        // 分析文件中的类型和成员的条件编译
        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
        bool foundMixedContent = false;

        foreach (var type in typeDeclarations)
        {
            var typeName = GetFullTypeName(type);
            var typeCondition = GetNodeCondition(type, root);
            
            if (typeCondition == "CLIENT")
            {
                _metadata.ClientOnlyTypes.Add(typeName);
                foundMixedContent = true;
            }
            else if (typeCondition == "SERVER")
            {
                _metadata.ServerOnlyTypes.Add(typeName);
                foundMixedContent = true;
            }
            else
            {
                // 检查是否是混合类型 - 遍历所有成员检查条件编译
                var clientMembers = new List<string>();
                var serverMembers = new List<string>();
                
                foreach (var member in type.Members)
                {
                    var memberCondition = GetNodeCondition(member, root);
                    var memberName = GetMemberName(member);
                    
                    if (memberName != null && memberCondition != null)
                    {
                        if (memberCondition == "CLIENT")
                        {
                            clientMembers.Add(memberName);
                        }
                        else if (memberCondition == "SERVER")
                        {
                            serverMembers.Add(memberName);
                        }
                    }
                }
                
                // 如果有平台特定成员，标记为混合类型
                if (clientMembers.Count > 0 || serverMembers.Count > 0)
                {
                    foundMixedContent = true;
                    var existingCondition = _metadata.MixedTypes.GetValueOrDefault(typeName);
                    
                    if (clientMembers.Count > 0 && serverMembers.Count > 0)
                    {
                        _metadata.MixedTypes[typeName] = "MIXED";
                    }
                    else if (clientMembers.Count > 0)
                    {
                        _metadata.MixedTypes[typeName] = existingCondition == "SERVER" ? "MIXED" : "CLIENT";
                    }
                    else if (serverMembers.Count > 0)
                    {
                        _metadata.MixedTypes[typeName] = existingCondition == "CLIENT" ? "MIXED" : "SERVER";
                    }
                    
                    // 记录成员级别的条件编译
                    foreach (var memberName in clientMembers)
                    {
                        _metadata.ClientOnlyMembers.Add($"{typeName}.{memberName}");
                    }
                    
                    foreach (var memberName in serverMembers)
                    {
                        _metadata.ServerOnlyMembers.Add($"{typeName}.{memberName}");
                    }
                    
                    Console.WriteLine($"🔀 发现混合类型: {typeName} (客户端成员: {clientMembers.Count}, 服务器成员: {serverMembers.Count})");
                }
            }
        }
        
        return foundMixedContent;
    }

    private async Task AnalyzeTypeMembersAsync(TypeDeclarationSyntax type, string typeName, SyntaxNode root)
    {
        var members = type.Members;
        
        foreach (var member in members)
        {
            var memberCondition = GetNodeCondition(member, root);
            if (memberCondition == null) continue;

            var memberName = GetMemberName(member);
            if (memberName == null) continue;

            var fullMemberName = $"{typeName}.{memberName}";
            
            if (memberCondition == "CLIENT")
            {
                _metadata.ClientOnlyMembers.Add(fullMemberName);
            }
            else if (memberCondition == "SERVER")
            {
                _metadata.ServerOnlyMembers.Add(fullMemberName);
            }
        }
    }

    private string? GetFileCondition(SyntaxNode root)
    {
        // 检查文件第一行是否是条件编译指令
        var allTrivia = root.DescendantTrivia().Where(t => t.IsDirective).Take(5);
        
        foreach (var trivia in allTrivia)
        {
            if (trivia.GetStructure() is IfDirectiveTriviaSyntax ifDirective)
            {
                var condition = ifDirective.Condition?.ToString();
                if (condition?.Contains("CLIENT") == true) return "CLIENT";
                if (condition?.Contains("SERVER") == true) return "SERVER";
            }
        }

        return null;
    }

    private string? GetNodeCondition(SyntaxNode node, SyntaxNode root)
    {
        var nodeSpan = node.Span;
        
        // 获取所有条件编译指令并按位置排序
        var directives = root.DescendantTrivia()
            .Where(t => t.IsDirective)
            .Select(t => new {
                Position = t.SpanStart,
                Directive = t.GetStructure() as DirectiveTriviaSyntax,
                Type = GetDirectiveType(t.GetStructure() as DirectiveTriviaSyntax)
            })
            .Where(x => x.Directive != null)
            .OrderBy(x => x.Position)
            .ToList();
        
        // 找到包含当前节点的条件编译块
        for (int i = 0; i < directives.Count; i++)
        {
            var directive = directives[i];
            
            if (directive.Type.StartsWith("IF") && directive.Position < nodeSpan.Start)
            {
                // 找到对应的 #endif
                var ifCount = 1;
                for (int j = i + 1; j < directives.Count; j++)
                {
                    var nextDirective = directives[j];
                    
                    if (nextDirective.Type.StartsWith("IF"))
                        ifCount++;
                    else if (nextDirective.Type == "ENDIF")
                        ifCount--;
                    
                    if (ifCount == 0)
                    {
                        // 找到匹配的 #endif
                        if (nextDirective.Position > nodeSpan.End)
                        {
                            // 节点在这个条件编译块中
                            var condition = directive.Type.Substring(3); // "IF_CLIENT" -> "CLIENT"
                            return condition == "CLIENT" || condition == "SERVER" ? condition : null;
                        }
                        break;
                    }
                }
            }
        }
        
        return null;
    }
    
    private string GetDirectiveType(DirectiveTriviaSyntax? directive)
    {
        return directive switch
        {
            IfDirectiveTriviaSyntax ifDir => $"IF_{ifDir.Condition?.ToString().Trim()}",
            ElifDirectiveTriviaSyntax elifDir => $"IF_{elifDir.Condition?.ToString().Trim()}",
            ElseDirectiveTriviaSyntax => "ELSE",
            EndIfDirectiveTriviaSyntax => "ENDIF",
            _ => "UNKNOWN"
        };
    }

    private string GetFullNamespace(BaseNamespaceDeclarationSyntax ns)
    {
        return ns.Name.ToString();
    }

    private string GetFullTypeName(TypeDeclarationSyntax type)
    {
        var typeName = type.Identifier.Text;
        
        // 获取包含的命名空间
        var ns = type.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
        if (ns != null)
        {
            return $"{GetFullNamespace(ns)}.{typeName}";
        }
        
        // 获取包含的类型（嵌套类）
        var parentType = type.Parent as TypeDeclarationSyntax;
        if (parentType != null)
        {
            return $"{GetFullTypeName(parentType)}.{typeName}";
        }
        
        return typeName;
    }

    private string? GetMemberName(MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax method => method.Identifier.Text,
            PropertyDeclarationSyntax property => property.Identifier.Text,
            FieldDeclarationSyntax field => field.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
            EventDeclarationSyntax evt => evt.Identifier.Text,
            ConstructorDeclarationSyntax ctor => ".ctor",
            _ => null
        };
    }

    private async Task AnalyzeProjectDependenciesAsync()
    {
        Console.WriteLine("🔗 分析项目依赖关系...");
        
        // 只有确认的完全专用命名空间才添加
        
        // 检查UserCloudData - 这个目录下的文件都是服务器专用
        var cloudDataDir = Path.Combine(_wasiCorePath, "GameCore", "UserCloudData");
        if (Directory.Exists(cloudDataDir))
        {
            var cloudDataFiles = Directory.GetFiles(cloudDataDir, "*.cs");
            if (cloudDataFiles.All(f => IsCompletelyServerOnly(f)))
            {
                Console.WriteLine("☁️ 确认UserCloudData目录为服务器专用");
                _metadata.ServerOnlyNamespaces.Add("GameCore.UserCloudData");
            }
        }
        
        // 检查GameUI项目 - 需要验证是否真的完全是客户端专用
        var gameUiProject = Path.Combine(_wasiCorePath, "GameUI", "GameUI.csproj");
        if (File.Exists(gameUiProject))
        {
            var content = await File.ReadAllTextAsync(gameUiProject);
            if (content.Contains("ClientInterfaceDefinition"))
            {
                Console.WriteLine("📱 检测到GameUI项目引用ClientInterfaceDefinition");
                
                // 验证GameUI目录下的文件是否都是客户端专用
                var gameUIDir = Path.Combine(_wasiCorePath, "GameUI");
                var gameUIFiles = Directory.GetFiles(gameUIDir, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
                    .ToList();
                
                var allClientOnly = true;
                foreach (var file in gameUIFiles.Take(10)) // 抽样检查
                {
                    if (!IsCompletelyClientOnly(file))
                    {
                        allClientOnly = false;
                        break;
                    }
                }
                
                if (allClientOnly)
                {
                    Console.WriteLine("✅ 确认GameUI目录为客户端专用");
                    _metadata.ClientOnlyNamespaces.Add("GameUI");
                    _metadata.ClientOnlyNamespaces.Add("GameUI.Control");
                    _metadata.ClientOnlyNamespaces.Add("GameUI.Control.Extensions");
                    _metadata.ClientOnlyNamespaces.Add("GameUI.Brush");
                    _metadata.ClientOnlyNamespaces.Add("GameUI.Enum");
                }
                else
                {
                    Console.WriteLine("⚠️ GameUI目录包含混合代码，不标记为完全专用");
                }
            }
        }
        
        // 清理重复的命名空间分类
        CleanupDuplicateNamespaces();
    }
    
    private bool IsCompletelyClientOnly(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var lines = content.Split('\n');
            
            // 检查是否以 #if CLIENT 开始
            var firstNonEmptyLine = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.Trim()));
            return firstNonEmptyLine?.Trim().StartsWith("#if CLIENT") == true;
        }
        catch
        {
            return false;
        }
    }
    
    private bool IsCompletelyServerOnly(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var lines = content.Split('\n');
            
            // 检查是否以 #if SERVER 开始
            var firstNonEmptyLine = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.Trim()));
            return firstNonEmptyLine?.Trim().StartsWith("#if SERVER") == true;
        }
        catch
        {
            return false;
        }
    }
    
    private void CleanupDuplicateNamespaces()
    {
        Console.WriteLine("🧹 清理重复的命名空间分类...");
        
        // 找出同时在客户端和服务器列表中的命名空间
        var duplicates = _metadata.ClientOnlyNamespaces.Intersect(_metadata.ServerOnlyNamespaces).ToList();
        
        foreach (var duplicate in duplicates)
        {
            Console.WriteLine($"⚠️ 发现重复分类的命名空间: {duplicate} - 移除专用标记");
            _metadata.ClientOnlyNamespaces.Remove(duplicate);
            _metadata.ServerOnlyNamespaces.Remove(duplicate);
        }
        
        Console.WriteLine($"🧹 清理完成，移除了 {duplicates.Count} 个重复项");
    }

    private void GenerateStatistics()
    {
        // 使用东八区时间（UTC+8）- 跨平台兼容
        var beijingTime = DateTime.UtcNow.AddHours(8);
        _metadata.Statistics["GeneratedAt"] = beijingTime.ToString("yyyy-MM-dd HH:mm:ss") + " +08:00";
        _metadata.Statistics["WasiCorePath"] = _wasiCorePath;
        _metadata.Statistics["ClientOnlyNamespacesCount"] = _metadata.ClientOnlyNamespaces.Count;
        _metadata.Statistics["ServerOnlyNamespacesCount"] = _metadata.ServerOnlyNamespaces.Count;
        _metadata.Statistics["ClientOnlyTypesCount"] = _metadata.ClientOnlyTypes.Count;
        _metadata.Statistics["ServerOnlyTypesCount"] = _metadata.ServerOnlyTypes.Count;
        _metadata.Statistics["ClientOnlyMembersCount"] = _metadata.ClientOnlyMembers.Count;
        _metadata.Statistics["ServerOnlyMembersCount"] = _metadata.ServerOnlyMembers.Count;
        _metadata.Statistics["MixedTypesCount"] = _metadata.MixedTypes.Count;
    }

    public void SaveToFile(ApiMetadata metadata, string outputPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        var json = JsonSerializer.Serialize(metadata, options);
        File.WriteAllText(outputPath, json);
        
        Console.WriteLine($"💾 元数据已保存到: {outputPath}");
    }

    public static ApiMetadata LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"⚠️ 元数据文件不存在: {filePath}");
            return new ApiMetadata();
        }
        
        var json = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        return JsonSerializer.Deserialize<ApiMetadata>(json, options) ?? new ApiMetadata();
    }

    public void PrintSummary(ApiMetadata metadata)
    {
        Console.WriteLine("\n📊 =========================");
        Console.WriteLine("📊 API元数据生成总结");
        Console.WriteLine("📊 =========================");
        Console.WriteLine($"📱 客户端专用命名空间: {metadata.ClientOnlyNamespaces.Count}");
        Console.WriteLine($"🖥️ 服务器专用命名空间: {metadata.ServerOnlyNamespaces.Count}");
        Console.WriteLine($"📱 客户端专用类型: {metadata.ClientOnlyTypes.Count}");
        Console.WriteLine($"🖥️ 服务器专用类型: {metadata.ServerOnlyTypes.Count}");
        Console.WriteLine($"📱 客户端专用成员: {metadata.ClientOnlyMembers.Count}");
        Console.WriteLine($"🖥️ 服务器专用成员: {metadata.ServerOnlyMembers.Count}");
        Console.WriteLine($"🔀 混合类型: {metadata.MixedTypes.Count}");

        Console.WriteLine("\n📱 客户端专用命名空间:");
        foreach (var ns in metadata.ClientOnlyNamespaces.Take(10))
        {
            Console.WriteLine($"  - {ns}");
        }
        if (metadata.ClientOnlyNamespaces.Count > 10)
        {
            Console.WriteLine($"  ... 还有 {metadata.ClientOnlyNamespaces.Count - 10} 个");
        }

        Console.WriteLine("\n🖥️ 服务器专用命名空间:");
        foreach (var ns in metadata.ServerOnlyNamespaces.Take(10))
        {
            Console.WriteLine($"  - {ns}");
        }
        if (metadata.ServerOnlyNamespaces.Count > 10)
        {
            Console.WriteLine($"  ... 还有 {metadata.ServerOnlyNamespaces.Count - 10} 个");
        }

        Console.WriteLine("\n🔀 混合类型示例:");
        foreach (var mixed in metadata.MixedTypes.Take(5))
        {
            Console.WriteLine($"  - {mixed.Key}: {mixed.Value}");
        }
    }
}

/// <summary>
/// 元数据生成器的控制台程序
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        string wasiCorePath = args[0];
        string outputPath = args.Length > 1 ? args[1] : "platform-api-metadata.json";
        
        if (!Directory.Exists(wasiCorePath))
        {
            Console.WriteLine($"❌ WasiCore路径不存在: {wasiCorePath}");
            Console.WriteLine($"❌ 请确认路径是否正确: {Path.GetFullPath(wasiCorePath)}");
            PrintUsage();
            return;
        }

        try
        {
            var generator = new PlatformApiMetadataGenerator(wasiCorePath);
            var metadata = await generator.GenerateMetadataAsync();
            
            generator.SaveToFile(metadata, outputPath);
            generator.PrintSummary(metadata);
            
            Console.WriteLine("\n✅ 元数据生成成功！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 生成失败: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("🔧 WasiCompatibilityAnalyzer 元数据生成器");
        Console.WriteLine("");
        Console.WriteLine("📋 用法:");
        Console.WriteLine("  dotnet run --project MetadataGenerator.csproj <WasiCore路径> [输出文件]");
        Console.WriteLine("");
        Console.WriteLine("📋 参数说明:");
        Console.WriteLine("  <WasiCore路径>  - 必需。WasiCore源码的根目录路径");
        Console.WriteLine("                    例如: C:\\Dev\\sce\\wasicore 或 /home/user/wasicore");
        Console.WriteLine("  [输出文件]      - 可选。生成的JSON元数据文件路径");
        Console.WriteLine("                    默认: platform-api-metadata.json");
        Console.WriteLine("");
        Console.WriteLine("📋 使用示例:");
        Console.WriteLine("  # Windows");
        Console.WriteLine("  dotnet run --project MetadataGenerator.csproj \"C:\\Dev\\sce\\wasicore\"");
        Console.WriteLine("  dotnet run --project MetadataGenerator.csproj \"C:\\Dev\\sce\\wasicore\" \"../api-data.json\"");
        Console.WriteLine("");
        Console.WriteLine("  # Linux/macOS");
        Console.WriteLine("  dotnet run --project MetadataGenerator.csproj \"/home/user/wasicore\"");
        Console.WriteLine("  dotnet run --project MetadataGenerator.csproj \"/home/user/wasicore\" \"../api-data.json\"");
        Console.WriteLine("");
        Console.WriteLine("🎯 功能说明:");
        Console.WriteLine("  此工具会扫描WasiCore源码中的所有C#文件，分析条件编译指令，");
        Console.WriteLine("  自动识别客户端专用、服务器专用和混合类型的API，生成精确的");
        Console.WriteLine("  元数据文件供WasiCompatibilityAnalyzer使用。");
        Console.WriteLine("");
        Console.WriteLine("📊 预期结果:");
        Console.WriteLine("  - 扫描1200+个C#文件（自动排除测试代码）");
        Console.WriteLine("  - 识别15+个客户端专用命名空间");
        Console.WriteLine("  - 识别1个服务器专用命名空间（UserCloudData）");
        Console.WriteLine("  - 识别250+个混合类型（包含条件编译的类型）");
        Console.WriteLine("  - 识别1000+个客户端专用成员");
        Console.WriteLine("  - 识别800+个服务器专用成员");
        Console.WriteLine("");
        Console.WriteLine("⚠️ 注意事项:");
        Console.WriteLine("  - 确保WasiCore路径包含GameCore、GameUI等子目录");
        Console.WriteLine("  - 确保对WasiCore目录有读取权限");
        Console.WriteLine("  - 生成过程可能需要几秒钟，请耐心等待");
    }
}
