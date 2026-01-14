# MindsetCoach AI Integration

## Overview

This integration adds AI-powered mental coaching features using Microsoft Semantic Kernel. It includes:

- **Goggins/Lasso Personas**: Generate weekly summaries with different coaching styles
- **Pattern Analysis**: Detect recurring mental barriers and trends
- **Auto-flagging**: AI-powered entry flagging for coach attention
- **Context Engineering Experiments**: Built-in tools to test and measure context strategies

## Quick Start

### 1. Install Dependencies

```bash
cd MindSetCoach.Api
dotnet restore
```

### 2. Configure API Key

Add your OpenAI API key to `appsettings.Development.json`:

```json
{
  "AI": {
    "Provider": "OpenAI",
    "ModelId": "gpt-4o-mini",
    "ApiKey": "sk-your-key-here"
  }
}
```

Or set an environment variable:
```bash
export OPENAI_API_KEY="sk-your-key-here"
```

### 3. Run the API

```bash
dotnet run
```

### 4. Test in Swagger

Open `http://localhost:5000/swagger` and try the AI endpoints.

## API Endpoints

### Core AI Features

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/ai/summary/{athleteId}?persona=goggins` | POST | Generate weekly summary |
| `/api/ai/patterns/{athleteId}` | GET | Analyze patterns |
| `/api/ai/flag-recommendation/{entryId}` | GET | Get flag recommendation |

### Context Engineering Experiments

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/ai/experiments/position-test/{athleteId}` | POST | Run U-curve position test |
| `/api/ai/experiments/compression-test/{athleteId}` | POST | Compare context configurations |
| `/api/ai/experiments/persona-compare/{athleteId}` | POST | Compare Goggins vs Lasso |
| `/api/ai/experiments/summary` | GET | Get experiment session summary |
| `/api/ai/experiments/export` | GET | Export all experiment logs |

## Personas

### Goggins Mode
- Direct, challenging, no-nonsense
- "Stop making excuses. The work isn't done."
- Calls out mental barriers directly

### Lasso Mode  
- Warm, encouraging, optimistic
- "You showed up. That's what matters."
- Builds confidence through belief

## Context Options

When calling `/api/ai/summary`, you can control context with query parameters:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `persona` | string | "lasso" | "goggins" or "lasso" |
| `maxEntries` | int? | null | Limit entries in context |
| `compress` | bool | false | Pre-summarize entries |
| `daysBack` | int? | null | Only include recent days |

Example:
```
POST /api/ai/summary/1?persona=goggins&maxEntries=7&compress=true
```

## Running Experiments

### Position Test (U-Curve Validation)

Tests if facts in the middle of context are missed:

```bash
curl -X POST "http://localhost:5000/api/ai/experiments/position-test/1?needleFact=shin%20splints"
```

Expected result: U-curve shows middle position has lower retrieval rate.

### Compression Test

Compares full vs compressed vs limited context:

```bash
curl -X POST "http://localhost:5000/api/ai/experiments/compression-test/1"
```

### View Experiment Results

```bash
curl "http://localhost:5000/api/ai/experiments/summary"
```

## File Structure

```
MindSetCoach.Api/
├── Services/
│   ├── AI/
│   │   ├── IMentalCoachAIService.cs      # Interface + DTOs
│   │   ├── MentalCoachAIService.cs       # Main implementation
│   │   └── Experiments/
│   │       └── ContextExperimentLogger.cs # Experiment tracking
├── Controllers/
│   └── AIController.cs                    # API endpoints
├── Configuration/
│   └── SemanticKernelConfiguration.cs     # DI setup + mock service
```

## Development Without API Key

The integration includes a mock service that runs when no API key is configured. This allows you to:
- Test API structure and routing
- Verify experiment logging
- Develop frontend without API costs

Mock responses are prefixed with `[MOCK GOGGINS]` or `[MOCK LASSO]`.

## Model Switching

To use a different model, update `appsettings.json`:

```json
{
  "AI": {
    "Provider": "OpenAI",
    "ModelId": "gpt-4-turbo"  // or "gpt-4o", "gpt-3.5-turbo"
  }
}
```

For Azure OpenAI:
```json
{
  "AI": {
    "Provider": "Azure",
    "ModelId": "gpt-4",
    "AzureEndpoint": "https://your-resource.openai.azure.com/",
    "DeploymentName": "your-deployment"
  }
}
```

## Logging

All AI calls and experiments are logged. Check console output for:
- `CONTEXT_SENT`: What context was sent to the LLM
- `RESULT_SUCCESS/FAILURE`: Call outcomes
- `POSITION_TEST`: Position test results

## Next Steps

1. Create test journal entries for an athlete
2. Run the position test to validate U-curve
3. Compare Goggins vs Lasso with same context
4. Experiment with compression settings
5. Document findings for your LinkedIn series

## Troubleshooting

**"No AI API key configured"**: Set `OPENAI_API_KEY` or update appsettings
**Mock responses only**: Check your API key is valid
**Rate limits**: Switch to `gpt-4o-mini` for cheaper testing
