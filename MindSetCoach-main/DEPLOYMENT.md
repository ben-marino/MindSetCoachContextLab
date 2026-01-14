# MindSetCoach - Railway Deployment Guide

## Overview
MindSetCoach is a mental training platform for athletes and coaches built with .NET 8 and React.

## Prerequisites
- GitHub account
- Railway account (free tier available)
- Repository pushed to GitHub

## Environment Variables Required

### Required for API Deployment:

| Variable | Description | Example |
|----------|-------------|---------|
| `DATABASE_URL` | PostgreSQL connection string | Automatically provided by Railway |
| `JWT_KEY` | Secret key for JWT token signing | Generate a secure 64+ character random string |
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Production` |
| `ALLOWED_ORIGINS` | Comma-separated list of allowed CORS origins | `https://your-app.railway.app,https://www.yourdomain.com` |

### Optional Environment Variables:

| Variable | Description | Default |
|----------|-------------|---------|
| `Jwt:Issuer` | JWT token issuer | `MindSetCoach` |
| `Jwt:Audience` | JWT token audience | `MindSetCoachUsers` |

### AI Features (Optional):
If using AI features, set the appropriate API keys:
- `ApiKey` - For OpenAI
- `AnthropicApiKey` - For Claude/Anthropic
- `GoogleApiKey` - For Google Gemini
- `DeepSeekApiKey` - For DeepSeek
- `OllamaEndpoint` - For Ollama (local models)

## Railway Deployment Steps

### 1. Create New Project
1. Go to [Railway](https://railway.app)
2. Click "New Project"
3. Select "Deploy from GitHub repo"
4. Select your MindSetCoach repository

### 2. Add PostgreSQL Database
1. Click "+ New" → "Database" → "Add PostgreSQL"
2. Railway will automatically set the `DATABASE_URL` environment variable

### 3. Configure Environment Variables
1. Go to your service → "Variables" tab
2. Add the following variables:

```
ASPNETCORE_ENVIRONMENT=Production
JWT_KEY=your-super-secret-jwt-key-here-make-it-long-and-random
ALLOWED_ORIGINS=https://your-app.railway.app
```

**To generate a secure JWT_KEY:**
```bash
# PowerShell
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 64 | ForEach-Object {[char]$_})

# Or use an online generator: https://randomkeygen.com/
```

### 4. Deploy
1. Railway will automatically detect .NET 8 and build the application
2. Migrations will run automatically on startup
3. Your API will be available at: `https://your-app.railway.app`

### 5. Verify Deployment
1. Check health endpoint: `https://your-app.railway.app/health`
   - Should return: `{"status":"healthy","timestamp":"..."}`
2. Check Swagger docs: `https://your-app.railway.app/swagger`
   - Only available in Development (disabled in Production for security)

## First Time Setup

### 1. Create First Coach Account
Use the registration endpoint to create the first coach account:

```bash
POST https://your-app.railway.app/api/auth/register
Content-Type: application/json

{
  "email": "coach@example.com",
  "password": "SecurePassword123!",
  "name": "John Doe",
  "role": "Coach"
}
```

### 2. Login and Get JWT Token
```bash
POST https://your-app.railway.app/api/auth/login
Content-Type: application/json

{
  "email": "coach@example.com",
  "password": "SecurePassword123!"
}
```

Response will include a JWT token for authentication.

## Database Migrations

Migrations run automatically on application startup. The app will:
1. Check for pending migrations
2. Apply them to the PostgreSQL database
3. Create all necessary tables and relationships

## Troubleshooting

### Database Connection Issues
- Verify `DATABASE_URL` is set by Railway
- Check Railway logs for connection errors
- Ensure PostgreSQL service is running

### JWT Authentication Errors
- Verify `JWT_KEY` environment variable is set
- Ensure the key is at least 32 characters
- Check that the key matches between deployments

### CORS Errors
- Add your frontend domain to `ALLOWED_ORIGINS`
- Separate multiple origins with commas
- Include protocol (https://)

### Application Won't Start
- Check Railway logs for detailed error messages
- Verify all required environment variables are set
- Ensure .NET 8 runtime is being used

## Monitoring

### Health Checks
- Endpoint: `GET /health`
- Returns 200 OK with status and timestamp when healthy

### Logs
- View logs in Railway dashboard → "Deployments" → "View Logs"
- Logs include:
  - Startup information
  - Database migration logs
  - API request logs
  - Error logs

## Security Notes

### Production Security Features:
✅ HTTPS enforced (UseHttpsRedirection)
✅ HSTS enabled for production
✅ JWT authentication required for protected endpoints
✅ Detailed errors hidden in production
✅ Environment-based CORS configuration
✅ Secure database connections with SSL

### Recommended Additional Security:
- Rotate JWT_KEY periodically
- Use strong passwords for admin accounts
- Monitor Railway logs for suspicious activity
- Keep dependencies updated
- Enable Railway's automatic security updates

## Support

For issues:
1. Check Railway deployment logs
2. Review application logs in Railway dashboard
3. Verify all environment variables are correctly set
4. Check database connection status

## Architecture

- **API**: ASP.NET Core 8 Web API
- **Database**: PostgreSQL (Railway)
- **Authentication**: JWT Bearer tokens
- **ORM**: Entity Framework Core
- **Hosting**: Railway

## Local Development vs Production

| Feature | Local (Development) | Production (Railway) |
|---------|-------------------|---------------------|
| Database | SQLite | PostgreSQL |
| Environment | Development | Production |
| CORS | Allow all origins | Specific origins only |
| Error Details | Full stack traces | Generic error messages |
| HTTPS | Optional | Enforced |
| Swagger | Enabled | Disabled |

---

**Last Updated:** December 2025
**Version:** 1.0
**Platform:** Railway
