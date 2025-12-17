using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.API.Controllers;

/// <summary>
/// Controller for managing client entities.
/// Provides endpoints for CRUD operations on clients.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ClientsController> _logger;

    public ClientsController(IUnitOfWork unitOfWork, ILogger<ClientsController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all clients.
    /// </summary>
    /// <returns>A list of all clients.</returns>
    /// <response code="200">Returns the list of clients.</response>
    /// <response code="500">An error occurred while retrieving clients.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Client>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<Client>>> GetClients()
    {
        try
        {
            var clients = await _unitOfWork.Clients.GetAllAsync();
            return Ok(clients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving clients");
            return StatusCode(500, "An error occurred while retrieving clients");
        }
    }

    /// <summary>
    /// Retrieves a specific client by ID.
    /// </summary>
    /// <param name="id">The client ID.</param>
    /// <returns>The client with the specified ID.</returns>
    /// <response code="200">Returns the client.</response>
    /// <response code="404">The client was not found.</response>
    /// <response code="500">An error occurred while retrieving the client.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Client), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Client>> GetClient(int id)
    {
        try
        {
            var client = await _unitOfWork.Clients.GetByIdAsync(id);
            if (client == null)
            {
                return NotFound();
            }

            return Ok(client);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client {ClientId}", id);
            return StatusCode(500, "An error occurred while retrieving the client");
        }
    }

    /// <summary>
    /// Creates a new client. Requires Admin role.
    /// </summary>
    /// <param name="client">The client to create.</param>
    /// <returns>The created client.</returns>
    /// <response code="201">Returns the newly created client.</response>
    /// <response code="500">An error occurred while creating the client.</response>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Client), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Client>> CreateClient([FromBody] Client client)
    {
        try
        {
            var now = DateTime.UtcNow;
            var createdBy = User.Identity?.Name ?? "System";
            client.CreatedDateTime = now;
            client.CreatedBy = createdBy;
            client.ModifiedDateTime = now;
            client.ModifiedBy = createdBy;

            await _unitOfWork.Clients.AddAsync(client);
            await _unitOfWork.SaveChangesAsync();

            return CreatedAtAction(nameof(GetClient), new { id = client.Id }, client);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating client");
            return StatusCode(500, "An error occurred while creating the client");
        }
    }

    /// <summary>
    /// Updates an existing client. Requires Admin role.
    /// </summary>
    /// <param name="id">The client ID.</param>
    /// <param name="client">The updated client data.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">The client was successfully updated.</response>
    /// <response code="400">The client ID in the URL does not match the client ID in the body.</response>
    /// <response code="404">The client was not found.</response>
    /// <response code="500">An error occurred while updating the client.</response>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateClient(int id, [FromBody] Client client)
    {
        try
        {
            if (id != client.Id)
            {
                return BadRequest("Client ID mismatch");
            }

            var existingClient = await _unitOfWork.Clients.GetByIdAsync(id);
            if (existingClient == null)
            {
                return NotFound();
            }

            client.ModifiedDateTime = DateTime.UtcNow;
            client.ModifiedBy = User.Identity?.Name ?? "System";

            await _unitOfWork.Clients.UpdateAsync(client);
            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating client {ClientId}", id);
            return StatusCode(500, "An error occurred while updating the client");
        }
    }
}
