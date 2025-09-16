// 这个文件用于测试WasiCompatibilityAnalyzer的检测功能

using System;
using System.Threading.Tasks;

namespace TestAnalyzer;

public class TestBasicDetection
{
    public void TestWebAssemblyIncompatibleAPIs()
    {
        // ❌ 应该报 WASI001: Task.Delay
        Task.Delay(1000);
        
        // ❌ 应该报 WASI002: Task.Run
        Task.Run(() => { });
        
        // ❌ 应该报 WASI003: Console
        Console.WriteLine("test");
        
        // ❌ 应该报 WASI004: Thread
        var thread = new Thread(() => { });
        thread.Start();
    }
    
    public async void TestCorrectAPIs()
    {
        // ✅ 正确的异步等待
        await Task.CompletedTask;  // 这个应该是OK的
        
        // 模拟正确的游戏API调用
        // await Game.Delay(TimeSpan.FromSeconds(1));
    }
}
