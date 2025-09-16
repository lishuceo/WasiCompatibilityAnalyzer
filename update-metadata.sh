#!/bin/bash

echo "🔧 WasiCompatibilityAnalyzer 元数据更新脚本"

if [ $# -eq 0 ]; then
    echo ""
    echo "❌ 错误: 请指定WasiCore源码路径"
    echo ""
    echo "📋 用法:"
    echo "  ./update-metadata.sh <WasiCore路径>"
    echo ""
    echo "📋 示例:"
    echo "  ./update-metadata.sh \"/home/user/wasicore\""
    echo "  ./update-metadata.sh \"/opt/wasicore\""
    echo "  ./update-metadata.sh \"../wasicore\""
    echo ""
    exit 1
fi

WASICORE_PATH="$1"

if [ ! -d "$WASICORE_PATH" ]; then
    echo "❌ 错误: WasiCore路径不存在: $WASICORE_PATH"
    echo "❌ 请检查路径是否正确"
    exit 1
fi

echo "🔍 验证WasiCore目录结构..."
if [ ! -d "$WASICORE_PATH/GameCore" ]; then
    echo "❌ 警告: 未找到GameCore子目录，请确认这是正确的WasiCore根目录"
fi

if [ ! -d "$WASICORE_PATH/GameUI" ]; then
    echo "❌ 警告: 未找到GameUI子目录，请确认这是正确的WasiCore根目录"
fi

echo ""
echo "📁 WasiCore路径: $WASICORE_PATH"
echo "🚀 开始生成元数据..."
echo ""

cd Tools

dotnet run --project MetadataGenerator.csproj "$WASICORE_PATH" "../platform-api-metadata.json"

if [ $? -eq 0 ]; then
    echo ""
    echo "🔄 重新构建分析器..."
    cd ..
    dotnet build
    
    if [ $? -eq 0 ]; then
        echo ""
        echo "✅ 元数据更新成功完成！"
        echo "📄 生成的文件: platform-api-metadata.json"
        echo "🔧 分析器已重新构建"
    else
        echo "❌ 分析器构建失败"
    fi
else
    echo "❌ 元数据生成失败"
fi
