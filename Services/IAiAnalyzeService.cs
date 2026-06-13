using AiAnalyze.Models;

namespace AiAnalyze.Services;

public interface IAiAnalyzeService
{
    Task<AnalyzeResponse> AnalyzeLogAsync(AnalyzeRequest request, CancellationToken cancellationToken = default);
}
