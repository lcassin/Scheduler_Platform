using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace SchedulerPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VendorCredentialsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VendorCredentialsController> _logger;
    private readonly IConfiguration _configuration;

    public VendorCredentialsController(
        IUnitOfWork unitOfWork,
        ILogger<VendorCredentialsController> logger,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VendorCredential>>> GetCredentials([FromQuery] int? clientId = null)
    {
        try
        {
            var credentials = clientId.HasValue
                ? await _unitOfWork.VendorCredentials.FindAsync(vc => vc.ClientId == clientId.Value && !vc.IsDeleted)
                : await _unitOfWork.VendorCredentials.GetAllAsync();

            foreach (var cred in credentials)
            {
                cred.EncryptedPassword = "***HIDDEN***";
            }

            return Ok(credentials);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vendor credentials");
            return StatusCode(500, "An error occurred while retrieving vendor credentials");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VendorCredential>> GetCredential(int id)
    {
        try
        {
            var credential = await _unitOfWork.VendorCredentials.GetByIdAsync(id);
            if (credential == null)
            {
                return NotFound();
            }

            credential.EncryptedPassword = "***HIDDEN***";
            return Ok(credential);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vendor credential {CredentialId}", id);
            return StatusCode(500, "An error occurred while retrieving the vendor credential");
        }
    }

    [HttpPost]
    public async Task<ActionResult<VendorCredential>> CreateCredential([FromBody] VendorCredentialDto credentialDto)
    {
        try
        {
            var credential = new VendorCredential
            {
                ClientId = credentialDto.ClientId,
                VendorName = credentialDto.VendorName,
                VendorUrl = credentialDto.VendorUrl,
                Username = credentialDto.Username,
                EncryptedPassword = EncryptPassword(credentialDto.Password),
                AdditionalData = credentialDto.AdditionalData,
                IsValid = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "System"
            };

            await _unitOfWork.VendorCredentials.AddAsync(credential);
            await _unitOfWork.SaveChangesAsync();

            credential.EncryptedPassword = "***HIDDEN***";
            return CreatedAtAction(nameof(GetCredential), new { id = credential.Id }, credential);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vendor credential");
            return StatusCode(500, "An error occurred while creating the vendor credential");
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCredential(int id, [FromBody] VendorCredentialDto credentialDto)
    {
        try
        {
            var credential = await _unitOfWork.VendorCredentials.GetByIdAsync(id);
            if (credential == null)
            {
                return NotFound();
            }

            credential.VendorName = credentialDto.VendorName;
            credential.VendorUrl = credentialDto.VendorUrl;
            credential.Username = credentialDto.Username;
            
            if (!string.IsNullOrEmpty(credentialDto.Password))
            {
                credential.EncryptedPassword = EncryptPassword(credentialDto.Password);
            }
            
            credential.AdditionalData = credentialDto.AdditionalData;
            credential.UpdatedAt = DateTime.UtcNow;
            credential.UpdatedBy = User.Identity?.Name ?? "System";

            await _unitOfWork.VendorCredentials.UpdateAsync(credential);
            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vendor credential {CredentialId}", id);
            return StatusCode(500, "An error occurred while updating the vendor credential");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCredential(int id)
    {
        try
        {
            var credential = await _unitOfWork.VendorCredentials.GetByIdAsync(id);
            if (credential == null)
            {
                return NotFound();
            }

            credential.IsDeleted = true;
            credential.UpdatedAt = DateTime.UtcNow;
            credential.UpdatedBy = User.Identity?.Name ?? "System";

            await _unitOfWork.VendorCredentials.UpdateAsync(credential);
            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vendor credential {CredentialId}", id);
            return StatusCode(500, "An error occurred while deleting the vendor credential");
        }
    }

    private string EncryptPassword(string password)
    {
        var encryptionKey = _configuration["Encryption:Key"] ?? "DefaultEncryptionKey12345678901234";
        
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32).Substring(0, 32));
        aes.IV = new byte[16];
        
        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        
        using var msEncrypt = new MemoryStream();
        using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(password);
        }
        
        return Convert.ToBase64String(msEncrypt.ToArray());
    }
}

public class VendorCredentialDto
{
    public int ClientId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string VendorUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? AdditionalData { get; set; }
}
