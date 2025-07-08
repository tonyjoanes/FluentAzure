using WebApi.Example.Models;

namespace WebApi.Example.Services;

/// <summary>
/// Service interface for user management operations.
/// </summary>
public interface IUserService
{
    Task<User?> GetUserByIdAsync(int id);
    Task<User?> GetUserByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User> CreateUserAsync(User user, string password);
    Task<User> UpdateUserAsync(User user);
    Task<bool> DeleteUserAsync(int id);
    Task<bool> ValidatePasswordAsync(string email, string password);
    Task<string> HashPasswordAsync(string password);
    Task<bool> IsEmailUniqueAsync(string email, int? excludeUserId = null);
}
