using System.Diagnostics;
using System.IO.Compression;
using System.Runtime;
using AiAnalyze.Services;
using Microsoft.AspNetCore.ResponseCompression;

// 榨干服务器性能配置
var process = Process.GetCurrentProcess();
var totalMemoryMB = (long)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);
var processorCount = Environment.ProcessorCount;

Console.WriteLine($"=== AiAnalyze 高性能模式 ===");
Console.WriteLine($"CPU核心数: {processorCount}");
Console.WriteLine($"可用内存: {totalMemoryMB}MB");

// 线程池优化 - 根据CPU核心数设置
ThreadPool.GetMinThreads(out int minWorker, out int minIO);
ThreadPool.GetMaxThreads(out int maxWorker, out int maxIO);

var newMinWorker = processorCount * 4;
var newMaxWorker = processorCount * 100;
var newMinIO = processorCount * 4;
var newMaxIO = processorCount * 100;

ThreadPool.SetMinThreads(newMinWorker, newMinIO);
ThreadPool.SetMaxThreads(newMaxWorker, newMaxIO);

Console.WriteLine($"线程池: 工作线程 {newMinWorker}-{newMaxWorker}, IO线程 {newMinIO}-{newMaxIO}");

// GC优化 - 服务器模式 + 最大内存限制(95%)
var maxMemoryMB = (long)(totalMemoryMB * 0.95);
GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
AppContext.SetData("GCHeapHardLimit", (ulong)(maxMemoryMB * 1024 * 1024));

Console.WriteLine($"GC模式: {GCSettings.LatencyMode}, 内存上限: {maxMemoryMB}MB (95%)");

var builder = WebApplication.CreateBuilder(args);

// 配置 Kestrel - 极致优化
builder.WebHost.ConfigureKestrel(options =>
{
    var maxConnections = Math.Min(50000, (int)(maxMemoryMB * 1024 / 20));
    options.Limits.MaxConcurrentConnections = maxConnections;
    options.Limits.MaxConcurrentUpgradedConnections = maxConnections;
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
    options.Limits.MaxRequestBufferSize = 1024 * 1024; // 1MB缓冲
    options.Limits.MaxResponseBufferSize = 1024 * 1024; // 1MB缓冲

    // HTTP/2优化
    options.Limits.Http2.MaxStreamsPerConnection = 100;
    options.Limits.Http2.HeaderTableSize = 4096;
    options.Limits.Http2.MaxFrameSize = 16384;
    options.Limits.Http2.MaxRequestHeaderFieldSize = 8192;

    // 超时设置 - 短连接快速回收
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);

    // 禁用延迟发送以提高吞吐量
    options.AllowSynchronousIO = true;

    Console.WriteLine($"Kestrel: 最大连接数 {maxConnections}");
});

// 添加服务到容器
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 添加响应压缩
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = false;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "application/xml" });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// 注册自定义服务
builder.Services.AddHttpClient<IAiAnalyzeService, AiAnalyzeService>();

var app = builder.Build();

// 配置HTTP请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseResponseCompression();
app.UseAuthorization();
app.MapControllers();

// 设置监听地址
app.Urls.Add("http://0.0.0.0:11451");

Console.WriteLine($"=== 服务器启动于 http://0.0.0.0:11451 ===");
Console.WriteLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine();

app.Run();
