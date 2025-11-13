using Microsoft.AspNetCore.Identity;

var hasher = new PasswordHasher<object>();
Console.WriteLine(hasher.HashPassword(null, "DevAdmin!2025!!"));
Console.WriteLine(hasher.HashPassword(null, "DevEditor!2025!!"));
Console.WriteLine(hasher.HashPassword(null, "DevViewer!2025!!"));

