using Microsoft.AspNetCore.Identity;

Console.WriteLine("=============================================================================");
Console.WriteLine("Password Hash Generator for Scheduler Platform Dev Users");
Console.WriteLine("=============================================================================");
Console.WriteLine();
Console.WriteLine("Generating PBKDF2 password hashes using Microsoft.AspNetCore.Identity.PasswordHasher...");
Console.WriteLine();

var hasher = new PasswordHasher<object>();
var passwords = new Dictionary<string, string>
{
    { "dev-admin@cassinfo.com", "DevAdmin!2025!!" },
    { "dev-editor@cassinfo.com", "DevEditor!2025!!" },
    { "dev-viewer@cassinfo.com", "DevViewer!2025!!" }
};

foreach (var kvp in passwords)
{
    var email = kvp.Key;
    var password = kvp.Value;
    var hash = hasher.HashPassword(null!, password);
    
    Console.WriteLine($"User: {email}");
    Console.WriteLine($"Password: {password}");
    Console.WriteLine($"Hash:");
    Console.WriteLine(hash);
    Console.WriteLine();
}

Console.WriteLine("=============================================================================");
Console.WriteLine("INSTRUCTIONS:");
Console.WriteLine("=============================================================================");
Console.WriteLine();
Console.WriteLine("1. Copy the hash values above");
Console.WriteLine("2. Open SETUP_DEV_USERS.sql in the repository root");
Console.WriteLine("3. Replace each 'PLACEHOLDER_HASH_FOR_*' with the corresponding hash");
Console.WriteLine("4. Open RESET_DEV_PASSWORDS.sql and do the same");
Console.WriteLine("5. Run SETUP_DEV_USERS.sql against your DEV/UAT database");
Console.WriteLine();
Console.WriteLine("Example replacement in SQL:");
Console.WriteLine("  DECLARE @DevAdminPasswordHash NVARCHAR(500) = 'AQAAAAIAAYagAAAA...'");
Console.WriteLine();
Console.WriteLine("Note: Each time you run this tool, different hashes are generated");
Console.WriteLine("      (random salt). This is normal and secure.");
Console.WriteLine();
Console.WriteLine("=============================================================================");
Console.WriteLine();
Console.WriteLine("Press Enter to exit...");
Console.ReadLine();
