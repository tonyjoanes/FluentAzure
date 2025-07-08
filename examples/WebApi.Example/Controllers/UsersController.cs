using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Example.Configuration;
using WebApi.Example.Data;
using WebApi.Example.Models;
using WebApi.Example.Services;

namespace WebApi.Example.Controllers;

/// <summary>
/// Users controller demonstrating configuration usage in Web API.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly WebApiConfiguration _config;
    private readonly IUserService _userService;
    private readonly IFileService _fileService;
    private readonly INotificationService _notificationService;
    private readonly IAuditService _auditService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        ApplicationDbContext context,
        WebApiConfiguration config,
        IUserService userService,
        IFileService fileService,
        INotificationService notificationService,
        IAuditService auditService,
        ILogger<UsersController> logger
    )
    {
        _context = context;
        _config = config;
        _userService = userService;
        _fileService = fileService;
        _notificationService = notificationService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users with pagination and filtering.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PaginatedResult<UserDto>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortBy = "Name",
        [FromQuery] bool ascending = true
    )
    {
        try
        {
            _logger.LogInformation(
                "Getting users - Page: {Page}, Size: {PageSize}, Search: {SearchTerm}",
                page,
                pageSize,
                searchTerm
            );

            // Apply rate limiting check
            if (_config.RateLimit.EnableRateLimiting)
            {
                var clientId = User.Identity?.Name ?? "anonymous";
                var isRateLimited = await CheckRateLimitAsync(clientId);
                if (isRateLimited)
                {
                    return StatusCode(429, new { Message = "Rate limit exceeded" });
                }
            }

            var query = _context.Users.AsQueryable();

            // Apply search filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(u =>
                    u.Name.Contains(searchTerm) || u.Email.Contains(searchTerm)
                );
            }

            // Apply sorting
            query = sortBy.ToLower() switch
            {
                "name" => ascending
                    ? query.OrderBy(u => u.Name)
                    : query.OrderByDescending(u => u.Name),
                "email" => ascending
                    ? query.OrderBy(u => u.Email)
                    : query.OrderByDescending(u => u.Email),
                "created" => ascending
                    ? query.OrderBy(u => u.CreatedAt)
                    : query.OrderByDescending(u => u.CreatedAt),
                _ => query.OrderBy(u => u.Name),
            };

            // Apply pagination
            var totalCount = await query.CountAsync();
            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    LastLoginAt = u.LastLoginAt,
                })
                .ToListAsync();

            var result = new PaginatedResult<UserDto>
            {
                Items = users,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            };

            // Add rate limit headers
            if (_config.RateLimit.EnableRateLimiting)
            {
                Response.Headers.Add(
                    _config.RateLimit.RateLimitHeader,
                    _config.RateLimit.RequestsPerMinute.ToString()
                );
                Response.Headers.Add(_config.RateLimit.RateLimitRemainingHeader, "99"); // Simplified
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get users");
            return StatusCode(500, new { Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get user by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(int id)
    {
        try
        {
            var user = await _context
                .Users.Where(u => u.Id == id)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    LastLoginAt = u.LastLoginAt,
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { Message = "User not found" });
            }

            // Check if user can access this resource
            var currentUserId = User.Identity?.Name;
            if (user.Email != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user {UserId}", id);
            return StatusCode(500, new { Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Create a new user.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            // Validate password strength
            if (!ValidatePasswordStrength(request.Password))
            {
                return BadRequest(new { Message = "Password does not meet security requirements" });
            }

            // Check if email already exists
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return BadRequest(new { Message = "Email already exists" });
            }

            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                PasswordHash = await _userService.HashPasswordAsync(request.Password),
                Role = request.Role ?? "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Send notification
            if (_config.Telemetry.EnableTelemetry)
            {
                await _notificationService.SendWelcomeEmailAsync(user.Email, user.Name);
            }

            // Audit the action
            if (_config.Security.EnableAuditLogging)
            {
                await _auditService.LogUserCreatedAsync(user.Id, User.Identity?.Name ?? "system");
            }

            var userDto = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
            };

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user");
            return StatusCode(500, new { Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Update user profile.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> UpdateUser(
        int id,
        [FromBody] UpdateUserRequest request
    )
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { Message = "User not found" });
            }

            // Check if user can update this resource
            var currentUserId = User.Identity?.Name;
            if (user.Email != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            // Update user properties
            user.Name = request.Name ?? user.Name;
            user.IsActive = request.IsActive ?? user.IsActive;

            // Only admins can change roles
            if (User.IsInRole("Admin") && !string.IsNullOrEmpty(request.Role))
            {
                user.Role = request.Role;
            }

            await _context.SaveChangesAsync();

            // Audit the action
            if (_config.Security.EnableAuditLogging)
            {
                await _auditService.LogUserUpdatedAsync(user.Id, User.Identity?.Name ?? "system");
            }

            var userDto = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
            };

            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user {UserId}", id);
            return StatusCode(500, new { Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Upload user profile picture.
    /// </summary>
    [HttpPost("{id}/profile-picture")]
    public async Task<ActionResult<string>> UploadProfilePicture(int id, IFormFile file)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { Message = "User not found" });
            }

            // Check if user can upload for this resource
            var currentUserId = User.Identity?.Name;
            if (user.Email != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            // Validate file
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { Message = "No file provided" });
            }

            if (file.Length > _config.Storage.MaxBlobSizeMB * 1024 * 1024)
            {
                return BadRequest(
                    new
                    {
                        Message = $"File size exceeds maximum of {_config.Storage.MaxBlobSizeMB}MB",
                    }
                );
            }

            // Upload to storage
            var fileName =
                $"profile-pictures/{user.Id}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var fileUrl = await _fileService.UploadFileAsync(
                _config.Storage.DefaultContainer,
                fileName,
                file.OpenReadStream()
            );

            // Update user profile picture URL
            user.ProfilePictureUrl = fileUrl;
            await _context.SaveChangesAsync();

            return Ok(new { ProfilePictureUrl = fileUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload profile picture for user {UserId}", id);
            return StatusCode(500, new { Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete user (soft delete).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteUser(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { Message = "User not found" });
            }

            // Soft delete if enabled
            if (_config.Storage.EnableSoftDelete)
            {
                user.IsActive = false;
                user.DeletedAt = DateTime.UtcNow;
            }
            else
            {
                _context.Users.Remove(user);
            }

            await _context.SaveChangesAsync();

            // Audit the action
            if (_config.Security.EnableAuditLogging)
            {
                await _auditService.LogUserDeletedAsync(user.Id, User.Identity?.Name ?? "system");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user {UserId}", id);
            return StatusCode(500, new { Message = "Internal server error" });
        }
    }

    private bool ValidatePasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        if (password.Length < _config.Security.MinPasswordLength)
            return false;

        if (
            _config.Security.RequireSpecialCharacters
            && !password.Any(c => !char.IsLetterOrDigit(c))
        )
            return false;

        return true;
    }

    private async Task<bool> CheckRateLimitAsync(string clientId)
    {
        // Simplified rate limiting check
        // In a real implementation, you would use a distributed cache
        return false;
    }
}

/// <summary>
/// User DTO for API responses.
/// </summary>
public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? ProfilePictureUrl { get; set; }
}

/// <summary>
/// Create user request model.
/// </summary>
public class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Role { get; set; }
}

/// <summary>
/// Update user request model.
/// </summary>
public class UpdateUserRequest
{
    public string? Name { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// Paginated result wrapper.
/// </summary>
public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
