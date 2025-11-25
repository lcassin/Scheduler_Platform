using Microsoft.AspNetCore.Identity;

Console.WriteLine("=== Password Hasher Utility ===");
Console.WriteLine("This utility generates password hashes compatible with ASP.NET Core Identity.");
Console.WriteLine();

Console.Write("Enter the password you want to hash: ");
var password = Console.ReadLine();

if (string.IsNullOrWhiteSpace(password))
{
    Console.WriteLine("Error: Password cannot be empty.");
    return 1;
}

var dummyUser = new { Id = 2, Email = "admin@cassinfo.com" };
var hasher = new PasswordHasher<object>();
var hashedPassword = hasher.HashPassword(dummyUser, password);

Console.WriteLine();
Console.WriteLine("=== Generated Password Hash ===");
Console.WriteLine(hashedPassword);
Console.WriteLine();
Console.WriteLine("=== SQL Update Script ===");
Console.WriteLine($"UPDATE Users SET PasswordHash = '{hashedPassword}', MustChangePassword = 0, PasswordChangedAt = GETUTCDATE(), UpdatedAt = GETUTCDATE(), UpdatedBy = 'Manual' WHERE Id = 2;");
Console.WriteLine();
Console.WriteLine("Copy and run the SQL script above in your database to update the Super Admin password.");
Console.WriteLine("Then you can log in with username 'admin' and the password you entered.");

return 0;
