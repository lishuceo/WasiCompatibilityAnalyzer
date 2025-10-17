// 这个文件用于测试WasiCompatibilityAnalyzer的检测功能

using System;
using System.IO;
using System.Threading;
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
    
    public void TestFileSystemAPIs()
    {
        // ⚠️ 应该产生 WASI006 Warning（而不是Error）
        // 文件系统API可用，但受沙箱限制
        var content = File.ReadAllText("test.txt");
        File.WriteAllText("output.txt", "data");
        Directory.CreateDirectory("folder");
        
        // 同样应该产生Warning
        var files = Directory.GetFiles("./data");
        var stream = new FileStream("test.dat", FileMode.Open);
        var reader = new StreamReader("test.txt");
        var writer = new StreamWriter("output.txt");
    }
}
