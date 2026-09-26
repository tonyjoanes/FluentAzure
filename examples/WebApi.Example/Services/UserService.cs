using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using WebApi.Example.Configuration;
using WebApi.Example.Data;
using WebApi.Example.Models;

namespace WebApi.Example.Services;

/// <summary>
/// Service implementation for user management operations.
/// </summary>
public class UserService : IUserService
{
    // Salted PBKDF2-SHA256 at the OWASP-recommended work factor, stored as "iterations.salt.hash".
    // In an ASP.NET Core Identity app, use its PasswordHasher<TUser> instead.
    private const int PasswordIterations = 600_000;
    private const int PasswordSaltSize = 16;
    private const int PasswordHashSize = 32;

    private readonly ApplicationDbContext _context;
    private readonly WebApiConfiguration _config;
    private readonly ILogger<UserService> _logger;

    public UserService(
        ApplicationDbContext context,
        WebApiConfiguration config,
        ILogger<UserService> logger)
    {
        _context = context;
        _config = config;
        _logger = logger;
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.DeletedAt == null);
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _context.Users
            .Where(u => u.DeletedAt == null)
            .OrderBy(u => u.Name)
            .ToListAsync();
    }

    public async Task<User> CreateUserAsync(User user, string password)
    {
        // Validate password strength
        if (!ValidatePasswordStrength(password))
        {
            throw new ArgumentException("Password does not meet security requirements");
        }

        // Check if email is unique
        if (!await IsEmailUniqueAsync(user.Email))
        {
            throw new InvalidOperationException("Email already exists");
        }

        // Hash password
        user.PasswordHash = await HashPasswordAsync(password);
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created user with ID: {UserId}, Email: {Email}", user.Id, user.Email);

        return user;
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        var existingUser = await GetUserByIdAsync(user.Id);
        if (existingUser == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Check if email is unique (excluding current user)
        if (!await IsEmailUniqueAsync(user.Email, user.Id))
        {
            throw new InvalidOperationException("Email already exists");
        }

        existingUser.Name = user.Name;
        existingUser.Email = user.Email;
        existingUser.Role = user.Role;
        existingUser.IsActive = user.IsActive;
        existingUser.ProfilePictureUrl = user.ProfilePictureUrl;
        existingUser.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated user with ID: {UserId}", user.Id);

        return existingUser;
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        var user = await GetUserByIdAsync(id);
        if (user == null)
        {
            return false;
        }

        // Soft delete
        user.DeletedAt = DateTime.UtcNow;
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted user with ID: {UserId}", id);

        return true;
    }

    public async Task<bool> ValidatePasswordAsync(string email, string password)
    {
        var user = await GetUserByEmailAsync(email);
        if (user == null)
        {
            return false;
        }

        return VerifyPassword(password, user.PasswordHash);
    }

    public Task<string> HashPasswordAsync(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(PasswordSaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, PasswordHashSize);
        return Task.FromResult(
            $"{PasswordIterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}");
    }

    private static bool VerifyPassword(string password, string? storedHash)
    {
        var parts = storedHash?.Split('.');
        if (parts is not { Length: 3 } || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public async Task<bool> IsEmailUniqueAsync(string email, int? excludeUserId = null)
    {
        var query = _context.Users.Where(u => u.Email == email && u.DeletedAt == null);

        if (excludeUserId.HasValue)
        {
            query = query.Where(u => u.Id != excludeUserId.Value);
        }

        return !await query.AnyAsync();
    }

    private bool ValidatePasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var minLength = _config.Security.MinPasswordLength;
        if (password.Length < minLength)
        {
            return false;
        }

        if (_config.Security.RequireSpecialCharacters)
        {
            var hasSpecialChar = password.Any(c => !char.IsLetterOrDigit(c));
            if (!hasSpecialChar)
            {
                return false;
            }
        }

        return true;
    }
}
