# Security Audit Report - Scheduler Platform
**Date:** November 14, 2025  
**Auditor:** Devin AI  
**Scope:** SOC2 Compliance & Penetration Testing Readiness  
**Application:** Scheduler Platform (.NET 10)

---

## Executive Summary

This security audit identifies vulnerabilities that are likely to be flagged during SOC2 audits and penetration testing. The findings are prioritized by severity and include specific remediation steps with code examples.

**Risk Summary:**
- **Critical:** 4 findings
- **High:** 4 findings  
- **Medium:** 4 findings
- **Low:** 3 findings

---

## CRITICAL SEVERITY FINDINGS

### 1. Sensitive Request Body Logging in Production
**Risk:** Exposure of secrets and PII in log files  
**SOC2 Impact:** Confidentiality control failure  
**Location:** `src/SchedulerPlatform.API/Program.cs:178-198`

**Current Code:**
```csharp
app.Use(async (context, next) =>
{
    if (context.Request.Method == "POST" && context.Request.Path.StartsWithSegments("/api/schedules"))
    {
        context.Request.EnableBuffering();
        var body = await reader.ReadToEndAsync();
        logger.LogInformation("POST /api/schedules Request Body: {RequestBody}", body);
    }
    await next();
});
```

**Issue:** This middleware logs the complete request body for schedule creation, which contains:
- Database connection strings in `JobConfiguration`
- API keys and authorization tokens
- Vendor credentials
- Source connection strings in job parameters

**Remediation:**
```csharp
// Only log request bodies in Development environment
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Method == "POST" && context.Request.Path.StartsWithSegments("/api/schedules"))
        {
            context.Request.EnableBuffering();
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
            logger.LogDebug("POST /api/schedules Request Body: {RequestBody}", body);
        }
        await next();
    });
}
```

---

### 2. Plaintext Secrets Stored in Database
**Risk:** Secrets at rest without encryption  
**SOC2 Impact:** Data protection control failure  
**Location:** Multiple files

**Affected Fields:**
- `Schedule.JobConfiguration` - Contains connection strings, API keys
- `JobParameter.SourceConnectionString` - Database connection strings
- `ApiCallJobConfig.AuthorizationValue` - API tokens/keys

**Current Storage:** All stored as plaintext `nvarchar(max)` in SQL Server

**Evidence:**
```csharp
// StoredProcedureJob.cs:64
var jobConfig = JsonSerializer.Deserialize<StoredProcedureJobConfig>(
    schedule.JobConfiguration ?? "{}");
// jobConfig.ConnectionString is plaintext

// ApiCallJob.cs:91
if (!string.IsNullOrEmpty(jobConfig.AuthorizationType) && 
    !string.IsNullOrEmpty(jobConfig.AuthorizationValue))
{
    // AuthorizationValue is plaintext API key/token
}
```

**Remediation Options:**

**Option A: Use VendorCredentials Table (Recommended)**
1. Store all credentials in `VendorCredentials` table with encryption
2. Reference credentials by ID in `JobConfiguration`
3. Decrypt only at job execution time

**Option B: Field-Level Encryption**
1. Create `IEncryptionService` using AES-256
2. Encrypt sensitive fields before saving
3. Decrypt on retrieval

**Implementation Example:**
```csharp
// Add to JobConfiguration
public class StoredProcedureJobConfig
{
    public int? VendorCredentialId { get; set; }  // Reference instead of plaintext
    // Remove: public string ConnectionString { get; set; }
}

// In job execution
var credential = await _unitOfWork.VendorCredentials.GetByIdAsync(jobConfig.VendorCredentialId);
var connectionString = _encryptionService.Decrypt(credential.EncryptedPassword);
```

---

### 3. Insecure SQL Connection Settings
**Risk:** Man-in-the-middle attacks, certificate validation bypass  
**SOC2 Impact:** Encryption in transit control failure  
**Location:** `src/SchedulerPlatform.Jobs/Jobs/*.cs`

**Current Code:**
```csharp
// StoredProcedureJob.cs:80-84
var connectionString = jobConfig.ConnectionString;
if (!connectionString.Contains("TrustServerCertificate=True", StringComparison.OrdinalIgnoreCase) &&
    !connectionString.Contains("Encrypt=False", StringComparison.OrdinalIgnoreCase))
{
    connectionString += ";TrustServerCertificate=True";
}
```

**Issue:** 
- Automatically adds `TrustServerCertificate=True` which bypasses certificate validation
- Allows `Encrypt=False` which disables TLS encryption
- Applies to all environments including production

**Remediation:**
```csharp
private string ValidateAndSecureConnectionString(string connectionString, IHostEnvironment env)
{
    // In production, enforce secure settings
    if (env.IsProduction())
    {
        // Reject insecure connection strings
        if (connectionString.Contains("TrustServerCertificate=True", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Encrypt=False", StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                "Insecure SQL connection settings are not allowed in production. " +
                "Use Encrypt=True and TrustServerCertificate=False with a valid certificate.");
        }
        
        // Ensure encryption is enabled
        if (!connectionString.Contains("Encrypt=", StringComparison.OrdinalIgnoreCase))
        {
            connectionString += ";Encrypt=True;TrustServerCertificate=False";
        }
    }
    else
    {
        // Development: Allow TrustServerCertificate for local testing
        if (!connectionString.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase))
        {
            connectionString += ";TrustServerCertificate=True";
        }
    }
    
    return connectionString;
}
```

---

### 4. Insecure Direct Object Reference (IDOR) - Cross-Tenant Access
**Risk:** Users can access/modify other clients' schedules  
**SOC2 Impact:** Access control failure, data isolation breach  
**Location:** `src/SchedulerPlatform.API/Controllers/SchedulesController.cs`

**Vulnerable Endpoints:**
```csharp
// Line 97-114: GetSchedule
[HttpGet("{id}")]
public async Task<ActionResult<Schedule>> GetSchedule(int id)
{
    var schedule = await _unitOfWork.Schedules.GetByIdAsync(id);
    if (schedule == null)
        return NotFound();
    return Ok(schedule);  // ❌ No ClientId check!
}

// Line 171-252: UpdateSchedule
[HttpPut("{id}")]
public async Task<IActionResult> UpdateSchedule(int id, [FromBody] ScheduleRequest request)
{
    var existingSchedule = await _unitOfWork.Schedules.GetByIdAsync(id);
    // ❌ No verification that user's ClientId matches schedule.ClientId
}

// Line 254-281: DeleteSchedule - Same issue
```

**Attack Scenario:**
1. User A (ClientId=1) authenticates and gets valid token
2. User A discovers Schedule ID 500 belongs to Client 2
3. User A calls `GET /api/schedules/500` and receives Client 2's schedule
4. User A can also update or delete Client 2's schedule

**Remediation:**
```csharp
[HttpGet("{id}")]
public async Task<ActionResult<Schedule>> GetSchedule(int id)
{
    var schedule = await _unitOfWork.Schedules.GetByIdAsync(id);
    if (schedule == null)
        return NotFound();
    
    // Get user's ClientId from claims
    var userClientId = User.FindFirst("client_id")?.Value;
    var isSystemAdmin = User.FindFirst("is_system_admin")?.Value == "True";
    
    // Verify ownership unless system admin
    if (!isSystemAdmin && schedule.ClientId.ToString() != userClientId)
    {
        _logger.LogWarning(
            "Unauthorized access attempt: User with ClientId {UserClientId} attempted to access Schedule {ScheduleId} belonging to ClientId {ScheduleClientId}",
            userClientId, id, schedule.ClientId);
        return Forbid();
    }
    
    return Ok(schedule);
}
```

**Better Approach - Global Query Filter:**
```csharp
// In SchedulerDbContext.OnModelCreating
modelBuilder.Entity<Schedule>().HasQueryFilter(s => 
    !s.IsDeleted && 
    (s.ClientId == _currentActor.GetClientId() || _currentActor.IsSystemAdmin()));
```

---

## HIGH SEVERITY FINDINGS

### 5. Missing Security Headers
**Risk:** Clickjacking, MIME sniffing, XSS attacks  
**SOC2 Impact:** Security baseline control failure  
**Location:** `src/SchedulerPlatform.API/Program.cs`

**Missing Headers:**
- `Strict-Transport-Security` (HSTS)
- `Content-Security-Policy`
- `X-Content-Type-Options`
- `X-Frame-Options`
- `Referrer-Policy`
- `Permissions-Policy`

**Remediation:**
```csharp
// Add after app.UseHttpsRedirection()
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Add("Content-Security-Policy", 
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self'; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none';");
        
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Add("Permissions-Policy", 
            "geolocation=(), microphone=(), camera=()");
        
        await next();
    });
}
```

---

### 6. No Rate Limiting or Brute Force Protection
**Risk:** API abuse, credential stuffing, DoS attacks  
**SOC2 Impact:** Availability control failure  
**Location:** `src/SchedulerPlatform.API/Program.cs`

**Current State:** No rate limiting configured

**Remediation:**
```csharp
// Add to builder.Services
builder.Services.AddRateLimiter(options =>
{
    // Global rate limit
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
    
    // Stricter limit for authentication endpoints
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));
    
    // Stricter limit for schedule trigger endpoints
    options.AddPolicy("trigger", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Add after app.UseAuthorization()
app.UseRateLimiter();
```

**Apply to Controllers:**
```csharp
[EnableRateLimiting("trigger")]
[HttpPost("{id}/trigger")]
public async Task<IActionResult> TriggerSchedule(int id)
```

---

### 7. Weak Session/Cookie Configuration
**Risk:** Session hijacking, CSRF attacks  
**SOC2 Impact:** Session management control failure  
**Location:** `src/SchedulerPlatform.UI/Program.cs`

**Remediation Needed:**
```csharp
// In UI Program.cs
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "oidc";
})
.AddCookie("Cookies", options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.Name = "__Host-SchedulerAuth";  // __Host- prefix for security
});
```

---

### 8. Swagger Exposed in Non-Development Environments
**Risk:** API documentation leakage, attack surface discovery  
**SOC2 Impact:** Information disclosure  
**Location:** `src/SchedulerPlatform.API/Program.cs:167-174`

**Current Code:**
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => { ... });
}
```

**Issue:** The audit logging PR (#23) added UAT/Staging but it wasn't merged to development branch yet.

**Remediation:** Ensure Swagger is ONLY in Development:
```csharp
// Keep as-is for production security
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Scheduler Platform API v1");
    });
}

// For UAT/Staging, use a separate configuration flag
if (app.Environment.IsEnvironment("UAT") || app.Environment.IsEnvironment("Staging"))
{
    var enableSwagger = builder.Configuration.GetValue<bool>("Features:EnableSwagger", false);
    if (enableSwagger)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => { ... });
    }
}
```

---

## MEDIUM SEVERITY FINDINGS

### 9. Hardcoded Encryption Key in Configuration
**Risk:** Key compromise if config file is exposed  
**SOC2 Impact:** Cryptographic key management failure  
**Location:** `src/SchedulerPlatform.API/appsettings.json:24-26`

**Current Code:**
```json
"Encryption": {
  "Key": "YourSecureEncryptionKey123456789012"
}
```

**Remediation:**
```csharp
// Use environment variables or Azure Key Vault
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddAzureKeyVault(/* ... */);

// In production appsettings.json, remove the key
"Encryption": {
  "Key": ""  // Set via environment variable ENCRYPTION__KEY
}
```

---

### 10. CORS Configuration May Be Too Permissive
**Risk:** Cross-origin attacks if misconfigured  
**SOC2 Impact:** Access control weakness  
**Location:** `src/SchedulerPlatform.API/Program.cs:154-163`

**Current Code:**
```csharp
policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "https://localhost:7299" })
```

**Remediation:**
- Ensure production `appsettings.json` has explicit HTTPS origins only
- No wildcards in production
- Verify `AllowCredentials()` is necessary

```json
// Production appsettings.json
"Cors": {
  "AllowedOrigins": [
    "https://scheduler.cassinfo.com",
    "https://scheduler-ui.cassinfo.com"
  ]
}
```

---

### 11. Potential SQL Injection in Dynamic Queries
**Risk:** SQL injection if parameters aren't properly sanitized  
**SOC2 Impact:** Input validation failure  
**Location:** `src/SchedulerPlatform.Jobs/Jobs/StoredProcedureJob.cs:116-123`

**Current Code:**
```csharp
using (var sourceCmd = new SqlCommand(param.SourceQuery, sourceConn))
{
    var sourceResult = await sourceCmd.ExecuteScalarAsync();
}
```

**Issue:** `param.SourceQuery` is user-provided and executed directly

**Remediation:**
```csharp
// Option 1: Restrict to stored procedures only
if (!IsValidStoredProcedureName(param.SourceQuery))
{
    throw new SecurityException("Only stored procedure names are allowed in SourceQuery");
}

// Option 2: Use parameterized queries with validation
private bool IsValidStoredProcedureName(string query)
{
    // Only allow alphanumeric, underscore, and dot (for schema.proc)
    return Regex.IsMatch(query, @"^[a-zA-Z0-9_\.]+$");
}
```

---

### 12. Insufficient Audit Logging for Security Events
**Risk:** Inability to detect/investigate security incidents  
**SOC2 Impact:** Monitoring and logging control failure  
**Location:** Multiple

**Missing Audit Events:**
- Failed login attempts
- Permission denied events
- Bulk data exports
- Configuration changes
- User privilege escalations

**Remediation:**
```csharp
// Add security event logging
public class SecurityEventLogger
{
    public void LogFailedLogin(string username, string ipAddress, string reason)
    {
        _logger.LogWarning(
            "SECURITY: Failed login attempt for user {Username} from {IpAddress}. Reason: {Reason}",
            username, ipAddress, reason);
    }
    
    public void LogUnauthorizedAccess(string username, string resource, string action)
    {
        _logger.LogWarning(
            "SECURITY: Unauthorized access attempt by {Username} to {Resource} for action {Action}",
            username, resource, action);
    }
}
```

---

## LOW SEVERITY FINDINGS

### 13. Missing Input Validation on Schedule Names/Descriptions
**Risk:** XSS, data integrity issues  
**SOC2 Impact:** Input validation control weakness  

**Remediation:**
```csharp
[StringLength(200, MinimumLength = 1)]
[RegularExpression(@"^[a-zA-Z0-9\s\-_\.]+$", ErrorMessage = "Invalid characters in schedule name")]
public string Name { get; set; }
```

---

### 14. No Password Complexity Enforcement for Local Accounts
**Risk:** Weak passwords  
**SOC2 Impact:** Password policy control failure  
**Location:** Local password authentication

**Current:** Password history tracking exists, but no complexity validation

**Remediation:**
```csharp
public class PasswordValidator
{
    public bool IsValid(string password, out string errorMessage)
    {
        if (password.Length < 12)
        {
            errorMessage = "Password must be at least 12 characters";
            return false;
        }
        
        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) || !password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            errorMessage = "Password must contain uppercase, lowercase, digit, and special character";
            return false;
        }
        
        errorMessage = null;
        return true;
    }
}
```

---

### 15. Dependency Vulnerabilities
**Risk:** Known CVEs in third-party packages  
**SOC2 Impact:** Vulnerability management control failure  

**Remediation:**
```bash
# Run regularly
dotnet list package --vulnerable
dotnet list package --outdated

# Set up automated scanning
# - GitHub Dependabot
# - Snyk
# - WhiteSource
```

---

## Remediation Priority

### Immediate (Before Production Deployment):
1. ✅ Remove/gate request body logging (Critical #1)
2. ✅ Fix IDOR vulnerabilities - add ClientId checks (Critical #4)
3. ✅ Enforce secure SQL connections in production (Critical #3)
4. ✅ Add security headers and HSTS (High #5)

### Short Term (Within 1 Month):
5. ✅ Implement secrets encryption/vault (Critical #2)
6. ✅ Add rate limiting (High #6)
7. ✅ Harden cookie configuration (High #7)
8. ✅ Verify Swagger is disabled in production (High #8)

### Medium Term (Within 3 Months):
9. ✅ Move encryption keys to vault (Medium #9)
10. ✅ Review and restrict CORS (Medium #10)
11. ✅ Add SQL injection protection (Medium #11)
12. ✅ Enhance security event logging (Medium #12)

### Ongoing:
13. ✅ Dependency vulnerability scanning
14. ✅ Regular security reviews
15. ✅ Penetration testing

---

## SOC2 Control Mapping

| Finding | SOC2 Control | Impact |
|---------|--------------|--------|
| #1 - Request body logging | CC6.1 - Confidentiality | High |
| #2 - Plaintext secrets | CC6.1 - Confidentiality | Critical |
| #3 - Insecure SQL | CC6.7 - Encryption in transit | Critical |
| #4 - IDOR | CC6.3 - Logical access | Critical |
| #5 - Missing headers | CC6.6 - Security baseline | High |
| #6 - No rate limiting | CC7.2 - Availability | High |
| #7 - Weak cookies | CC6.1 - Session management | High |
| #8 - Swagger exposure | CC6.1 - Information disclosure | High |

---

## Compliance Checklist

### SOC2 Type II Requirements:
- [ ] Encryption at rest for sensitive data
- [ ] Encryption in transit (TLS 1.2+)
- [ ] Multi-factor authentication (OIDC provides this)
- [ ] Role-based access control (Implemented)
- [ ] Audit logging (Implemented in PR #23)
- [ ] Password policies
- [ ] Session management
- [ ] Vulnerability management
- [ ] Change management
- [ ] Incident response procedures

### PCI DSS (if processing payment data):
- [ ] Network segmentation
- [ ] Firewall configuration
- [ ] Strong cryptography
- [ ] Secure development practices
- [ ] Regular security testing

---

## Testing Recommendations

### Penetration Testing Focus Areas:
1. **Authentication/Authorization**
   - Brute force attacks on login
   - Token manipulation
   - Session hijacking
   - IDOR testing across all endpoints

2. **Injection Attacks**
   - SQL injection in SourceQuery
   - Command injection in ProcessJob
   - LDAP injection (if applicable)

3. **Business Logic**
   - Schedule manipulation
   - Job execution bypass
   - Permission escalation

4. **API Security**
   - Rate limiting bypass
   - Mass assignment
   - Excessive data exposure

### Automated Security Scanning:
- **SAST:** SonarQube, Checkmarx
- **DAST:** OWASP ZAP, Burp Suite
- **Dependency:** Snyk, WhiteSource
- **Container:** Trivy, Clair

---

## Conclusion

The Scheduler Platform has a solid foundation with OIDC authentication, permission-based authorization, and audit logging. However, several critical vulnerabilities must be addressed before SOC2 certification and penetration testing:

**Most Critical:**
1. Remove sensitive data from logs
2. Encrypt secrets at rest
3. Fix IDOR vulnerabilities
4. Enforce secure SQL connections

**Estimated Remediation Effort:**
- Critical fixes: 2-3 days
- High priority: 3-5 days
- Medium priority: 5-7 days
- Total: 2-3 weeks for full remediation

**Next Steps:**
1. Review and prioritize findings with security team
2. Create remediation tickets
3. Implement fixes in priority order
4. Conduct security testing
5. Schedule external penetration test
6. Prepare for SOC2 audit

---

**Report Prepared By:** Devin AI  
**Date:** November 14, 2025  
**Contact:** Available for implementation assistance
