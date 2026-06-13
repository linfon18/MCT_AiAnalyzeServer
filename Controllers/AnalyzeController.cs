using Microsoft.AspNetCore.Mvc;
using AiAnalyze.Models;
using AiAnalyze.Services;

namespace AiAnalyze.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyzeController : ControllerBase
{
    private readonly IAiAnalyzeService _aiService;
    private readonly ILogger<AnalyzeController> _logger;

    public AnalyzeController(IAiAnalyzeService aiService, ILogger<AnalyzeController> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    /// <summary>
    /// 分析日志内容
    /// </summary>
    [HttpPost("log")]
    public async Task<IActionResult> AnalyzeLog([FromBody] AnalyzeRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.LogContent))
            {
                return BadRequest(ApiResponse<object>.Error("logContent 不能为空"));
            }

            var result = await _aiService.AnalyzeLogAsync(request);
            return Ok(ApiResponse<AnalyzeResponse>.Ok(result, "分析成功"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "分析日志失败: 配置错误");
            return StatusCode(500, ApiResponse<object>.Error($"服务端配置错误: {ex.Message}"));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "分析日志失败: AI API 错误");
            return StatusCode(502, ApiResponse<object>.Error($"AI 服务调用失败: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "分析日志失败");
            return StatusCode(500, ApiResponse<object>.Error($"分析失败: {ex.Message}"));
        }
    }
}
