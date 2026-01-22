# MindSetCoach AI Provider Guide

Run context engineering experiments across **7 AI providers** to compare quality, cost, and behavior patterns.

## Provider Overview

| Provider | Models | Cost Tier | Best For |
|----------|--------|-----------|----------|
| **OpenAI** | gpt-4o-mini, gpt-4o, o1, o3 | $$ | Baseline quality |
| **Anthropic** | Claude 4, Claude 3.5 | $$$ | Nuanced responses |
| **Google** | Gemini 2.5, 2.0 | $$ | Long context (1M) |
| **DeepSeek** | Chat, Reasoner | $ | Budget + reasoning |
| **Ollama** | Llama, Mistral, etc. | Free | Privacy, offline |
| **Azure** | OpenAI models | $$ | Enterprise (legacy) |
| **Azure OpenAI** | OpenAI models | $$ | Enterprise (recommended) |

## Cost Comparison (per 1M tokens)

| Model | Input | Output | Notes |
|-------|-------|--------|-------|
| **Budget Tier** |
| DeepSeek Chat | $0.14 | $0.28 | Best value cloud |
| Gemini 2.0 Flash | $0.10 | $0.40 | Fastest |
| Ollama (local) | $0.00 | $0.00 | Your hardware |
| **Mid Tier** |
| gpt-4o-mini | $0.15 | $0.60 | OpenAI baseline |
| Gemini 2.5 Flash | $0.15 | $0.60 | Google equivalent |
| Claude 3.5 Haiku | $0.80 | $4.00 | Fast Claude |
| **Premium Tier** |
| gpt-4o | $2.50 | $10.00 | OpenAI flagship |
| Claude Sonnet 4 | $3.00 | $15.00 | Balanced Claude |
| Gemini 2.5 Pro | $1.25 | $10.00 | Google flagship |
| **Reasoning Tier** |
| o1-mini | $3.00 | $12.00 | OpenAI reasoning |
| o3-mini | $1.10 | $4.40 | Latest reasoning |
| DeepSeek Reasoner | $0.55 | $2.19 | Budget reasoning |
| Claude Opus 4 | $15.00 | $75.00 | Most capable |

---

## Quick Start Configurations

### OpenAI (Baseline)
```json
{
  "AI": {
    "Provider": "OpenAI",
    "ModelId": "gpt-4o-mini",
    "ApiKey": "sk-..."
  }
}
```
Environment: `OPENAI_API_KEY`

### Anthropic (Claude)
```json
{
  "AI": {
    "Provider": "Anthropic",
    "ModelId": "claude-sonnet-4",
    "AnthropicApiKey": "sk-ant-..."
  }
}
```
Environment: `ANTHROPIC_API_KEY`
Get key: https://console.anthropic.com/

### Google (Gemini)
```json
{
  "AI": {
    "Provider": "Google",
    "ModelId": "gemini-2.5-flash",
    "GoogleApiKey": "..."
  }
}
```
Environment: `GOOGLE_API_KEY` or `GEMINI_API_KEY`
Get key: https://aistudio.google.com/apikey

### DeepSeek (Budget)
```json
{
  "AI": {
    "Provider": "DeepSeek",
    "ModelId": "deepseek-chat",
    "DeepSeekApiKey": "sk-..."
  }
}
```
Environment: `DEEPSEEK_API_KEY`
Get key: https://platform.deepseek.com/

### Ollama (Local/Free)
```json
{
  "AI": {
    "Provider": "Ollama",
    "ModelId": "llama3.1:8b",
    "OllamaEndpoint": "http://localhost:11434"
  }
}
```
No API key needed. Install: https://ollama.com/

### Azure OpenAI (Enterprise)
```json
{
  "SemanticKernel": {
    "Provider": "azureopenai",
    "AzureOpenAI": {
      "Endpoint": "https://your-resource-name.openai.azure.com/",
      "DeploymentName": "gpt-4o-mini",
      "ApiKey": "your-api-key",
      "UseAzureAD": false
    }
  }
}
```
Environment variables:
- `AZURE_OPENAI_ENDPOINT` - Azure OpenAI resource endpoint
- `AZURE_OPENAI_API_KEY` - API key (if not using Azure AD)
- `AZURE_OPENAI_DEPLOYMENT` - Model deployment name

**For Azure AD authentication** (managed identity, service principal):
```json
{
  "SemanticKernel": {
    "Provider": "azureopenai",
    "AzureOpenAI": {
      "Endpoint": "https://your-resource-name.openai.azure.com/",
      "DeploymentName": "gpt-4o-mini",
      "UseAzureAD": true
    }
  }
}
```

---

## Model Recommendations by Use Case

### For Benchmarking (run same experiment across providers)
1. **gpt-4o-mini** - OpenAI baseline
2. **claude-sonnet-4** - Anthropic comparison
3. **gemini-2.5-flash** - Google comparison
4. **deepseek-chat** - Budget comparison
5. **llama3.1:8b** - Local comparison

### For Cost Optimization
- **Lowest cost**: Ollama (free) or DeepSeek Chat ($0.14/1M)
- **Best value**: Gemini 2.5 Flash or gpt-4o-mini
- **Premium but worth it**: Claude Sonnet 4

### For Reasoning Experiments
- **Best**: Claude Opus 4 or o1-preview
- **Budget**: DeepSeek Reasoner
- **Local**: deepseek-r1:14b via Ollama

### For Long Context (Dumb Zone)
- **1M tokens**: Gemini 2.5 Pro/Flash
- **200K tokens**: Claude models
- **128K tokens**: GPT-4o, gpt-4o-mini

---

## Provider-Specific Notes

### OpenAI
- **o1/o3 models**: Reasoning models, different prompting style
- **gpt-4o-mini**: Best default for most experiments
- **Vision**: gpt-4o supports images

### Anthropic (Claude)
- **200K context**: Good for Dumb Zone experiments
- **Nuanced**: May give less extreme Goggins responses
- **Helpful**: Generally very good at following persona instructions
- **Model aliases**: Use "sonnet", "opus", "haiku" shortcuts

### Google (Gemini)
- **1M context**: Largest context window - interesting for experiments
- **Thinking**: 2.5 models have chain-of-thought like o1
- **Price-competitive**: Flash models match gpt-4o-mini pricing
- **Model aliases**: Use "pro", "flash", "fast" shortcuts

### DeepSeek
- **R1 Reasoning**: Strong chain-of-thought capabilities
- **Very cheap**: ~10x cheaper than OpenAI
- **Cache hits**: Even cheaper with repeated context
- **OpenAI-compatible**: Uses same API format

### Ollama (Local)
- **Privacy**: Data never leaves your machine
- **Free**: Only electricity cost
- **Your GPU**: RTX 5060 Ti 16GB handles 7B-14B models well
- **Recommended models**:
  - `llama3.1:8b` - Best general purpose
  - `deepseek-r1:14b` - Best reasoning
  - `mistral:7b` - Fastest
  - `phi3:medium` - Good 14B option

### Azure OpenAI (Enterprise)
- **Enterprise-grade**: SOC 2, HIPAA, GDPR compliant
- **Data privacy**: Data processed within your Azure tenant
- **SLA**: 99.9% availability guarantee
- **Regional deployment**: Deploy in specific Azure regions for latency optimization
- **Authentication options**:
  - **API Key**: Simple setup, good for development
  - **Azure AD**: Managed identity, service principal, or user authentication
- **Deployment names**: Use your custom deployment names, not model IDs
- **Setup steps**:
  1. Create Azure OpenAI resource in Azure Portal
  2. Deploy a model in Azure OpenAI Studio
  3. Copy endpoint URL from resource overview
  4. Get API key from "Keys and Endpoint" blade
  5. Configure app with endpoint, deployment name, and API key

---

## Running Cross-Provider Experiments

### Check Current Provider
```bash
curl http://localhost:5000/api/ai/provider
```

### Benchmark Script
```bash
#!/bin/bash
# benchmark-all-providers.sh

ATHLETE_ID=1
OUTPUT_DIR="./benchmark-$(date +%Y%m%d)"
mkdir -p "$OUTPUT_DIR"

# Define provider configs
declare -A CONFIGS=(
  ["openai_gpt4omini"]='{"Provider":"OpenAI","ModelId":"gpt-4o-mini"}'
  ["anthropic_sonnet"]='{"Provider":"Anthropic","ModelId":"claude-sonnet-4"}'
  ["google_flash"]='{"Provider":"Google","ModelId":"gemini-2.5-flash"}'
  ["deepseek_chat"]='{"Provider":"DeepSeek","ModelId":"deepseek-chat"}'
  ["ollama_llama"]='{"Provider":"Ollama","ModelId":"llama3.1:8b"}'
)

for name in "${!CONFIGS[@]}"; do
    echo "=== Testing: $name ==="
    
    # You'd restart the app with new config, then:
    curl -s -X POST "http://localhost:5000/api/ai/experiments/persona-compare/$ATHLETE_ID" \
      -H "Authorization: Bearer $JWT" \
      -o "$OUTPUT_DIR/${name}_persona.json"
    
    curl -s -X POST "http://localhost:5000/api/ai/experiments/position-test/$ATHLETE_ID" \
      -H "Authorization: Bearer $JWT" \
      -o "$OUTPUT_DIR/${name}_position.json"
    
    echo "  Saved to $OUTPUT_DIR/${name}_*.json"
done

echo "Done! Results in $OUTPUT_DIR"
```

---

## Expected Experimental Results

### Persona War (Goggins vs Lasso)

| Provider | Goggins Intensity | Lasso Warmth | Notes |
|----------|-------------------|--------------|-------|
| OpenAI | High | High | Good balance |
| Claude | Medium-High | Very High | More nuanced |
| Gemini | High | High | Similar to OpenAI |
| DeepSeek | High | Medium | Can be blunt |
| Ollama | Medium | Medium | Depends on model |

### Dumb Zone (Middle Retrieval)

| Provider | Start | Middle | End | Context Size |
|----------|-------|--------|-----|--------------|
| All models | ~95% | ~5-15% | ~90% | - |

**Key insight**: The U-curve attention pattern appears across ALL providers. This validates that "Lost in the Middle" is a fundamental LLM characteristic, not a vendor-specific bug.

### Cost Per Experiment

| Provider | Persona War | Position Test | Full Benchmark |
|----------|-------------|---------------|----------------|
| OpenAI gpt-4o-mini | ~$0.001 | ~$0.003 | ~$0.01 |
| Claude Sonnet 4 | ~$0.005 | ~$0.015 | ~$0.05 |
| Gemini 2.5 Flash | ~$0.001 | ~$0.003 | ~$0.01 |
| DeepSeek Chat | ~$0.0005 | ~$0.0015 | ~$0.005 |
| Ollama | $0.00 | $0.00 | $0.00 |

---

## Troubleshooting

### "Invalid API key"
- Check the key is correct for that provider
- Verify environment variable name matches provider
- Try setting explicitly in appsettings.Development.json

### "Model not found"
- Use exact model names (check config files for aliases)
- For Ollama: `ollama pull <model>` first
- Check provider supports the model version

### Slow responses
- Reasoning models (o1, deepseek-reasoner) are intentionally slower
- Local models: Check `nvidia-smi` for GPU usage
- Consider using smaller/faster models for iteration

### Context too long
- gpt-4o-mini: 128K limit
- Claude: 200K limit  
- Gemini: 1M limit
- Reduce MaxEntries in ContextOptions

---

## LinkedIn Content Ideas

After running experiments across providers:

**Post 1**: "I ran the same AI experiment on 5 different providers. The 'Lost in the Middle' bug? It's in ALL of them. Here's the data..."

**Post 2**: "Claude Opus 4 costs $75/M output tokens. DeepSeek Chat costs $0.28/M. For my coaching app experiments, the quality difference was [X]%. Here's what that means for your AI budget..."

**Post 3**: "Google Gemini has 1M token context. Claude has 200K. GPT-4o has 128K. I tested if bigger context = better middle-fact retrieval. Spoiler: Nope."

