using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiAnalyze.Models;

namespace AiAnalyze.Services;

public class AiAnalyzeService : IAiAnalyzeService
{
    private readonly ILogger<AiAnalyzeService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _defaultModel;
    private readonly int _defaultMaxTokens;
    private readonly double _defaultTemperature;
    private readonly double _defaultTopP;

    public AiAnalyzeService(ILogger<AiAnalyzeService> logger, IConfiguration configuration, HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;

        var aiConfig = configuration.GetSection("AiConfig");
        _baseUrl = aiConfig["BaseUrl"] ?? "https://token-plan-cn.xiaomimimo.com/v1";
        _apiKey = aiConfig["ApiKey"] ?? "";
        _defaultModel = aiConfig["Model"] ?? "mimo-v2.5-pro";
        _defaultMaxTokens = aiConfig.GetValue<int>("MaxTokens", 4096);
        _defaultTemperature = aiConfig.GetValue<double>("Temperature", 1.0);
        _defaultTopP = aiConfig.GetValue<double>("TopP", 0.95);
    }

    public async Task<AnalyzeResponse> AnalyzeLogAsync(AnalyzeRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("服务端未配置 API Key");
        }

        var model = string.IsNullOrWhiteSpace(request.Model) ? _defaultModel : request.Model;
        var maxTokens = request.MaxTokens ?? _defaultMaxTokens;
        var temperature = request.Temperature ?? _defaultTemperature;
        var topP = request.TopP ?? _defaultTopP;

        var systemPrompt = request.SystemPrompt ??
            @"你是 MiMo，是小米公司研发的 AI 智能助手。你擅长分析应用程序日志，能够识别错误、异常、性能瓶颈，并给出清晰的诊断建议和解决方案。请用中文回答。

【输出格式规范 - 必须严格遵守】
请严格按照以下固定分区格式输出，每个分区必须使用指定的 ## 标题，方便 UI 前端按区域渲染：

## 总体摘要
用 2-3 句话概括日志整体状况和核心问题。

## 问题清单
使用 Markdown 表格列出所有发现的问题，格式如下：
| 级别 | 时间/位置 | 问题描述 | 影响范围 |
|------|-----------|----------|----------|
| 🔴 错误 / 🟡 警告 / 🔵 信息 | ... | ... | ... |

## 根因分析
逐条分析每个问题的根本原因，使用编号列表 (1. 2. 3.)。

## 解决方案
针对每个问题给出具体可操作的修复步骤，使用编号列表 (1. 2. 3.)，代码片段用 ```csharp 包裹。

## 优先级排序
按紧急程度排序，格式：
- 🔴 立即处理：...
- 🟡 重要：...
- 🟢 建议优化：...

## 结论
用 1-2 句话总结最重要的行动建议。

注意：
- 禁止输出【输出格式规范】这段说明本身
- 每个分区标题必须保留
- 表格列必须对齐，不要合并单元格
- 如果某类问题不存在，写""未发现问题""即可";

        var userPrompt = $"请分析以下应用程序日志：\n\n```\n{request.LogContent}\n```";

        var chatRequest = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            max_completion_tokens = maxTokens,
            temperature,
            top_p = topP,
            stream = false
        };

        var json = JsonSerializer.Serialize(chatRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        req.Headers.Add("api-key", _apiKey);
        req.Content = content;

        var response = await _httpClient.SendAsync(req, cancellationToken);
        var respJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("MIMO API 返回 {StatusCode}: {Response}", response.StatusCode, respJson);
            throw new HttpRequestException($"MIMO API 调用失败: {response.StatusCode}");
        }

        var node = JsonNode.Parse(respJson);
        var choice = node?["choices"]?.AsArray()?.FirstOrDefault();
        var message = choice?["message"];
        var result = message?["content"]?.ToString() ?? "";
        var reasoningContent = message?["reasoning_content"]?.ToString();
        var usage = node?["usage"];

        _logger.LogInformation("分析完成 输入tokens: {PromptTokens} 输出tokens: {CompletionTokens}",
            usage?["prompt_tokens"], usage?["completion_tokens"]);

        return new AnalyzeResponse
        {
            Result = result,
            ReasoningContent = reasoningContent,
            Usage = usage,
            Model = model
        };
    }
}
