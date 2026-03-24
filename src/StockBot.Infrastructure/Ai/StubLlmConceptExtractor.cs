using Microsoft.Extensions.Logging;

namespace StockBot.Infrastructure.Ai;

/// <summary>
/// LLM 概念萃取的 Stub 實作，永遠回傳空列表（不消耗 API 費用）。
/// TODO: 替換為真實實作（Semantic Kernel + GPT-4o-mini）：
///   1. 安裝 Microsoft.SemanticKernel NuGet
///   2. 注入 API Key / Endpoint（透過 IOptions&lt;LlmOptions&gt;）
///   3. 建立 Kernel，呼叫 ChatCompletionService
///   4. Prompt 範例（繁體中文）：
///      "你是一位台股分析助理，請從以下文章中找出 3-5 個可能成為新投資主題的關鍵字。
///       只回傳關鍵字列表，每行一個，不要解釋。
///       標題：{title}
///       內文：{content}"
///   5. 解析回傳文字，拆分成 List&lt;string&gt;
/// </summary>
internal sealed class StubLlmConceptExtractor(ILogger<StubLlmConceptExtractor> logger) : ILlmConceptExtractor
{
    public Task<IReadOnlyList<string>> ExtractConceptsAsync(
        string title,
        string content,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "StubLlmConceptExtractor: skipping concept extraction for \"{Title}\" (stub).",
            title);

        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}
