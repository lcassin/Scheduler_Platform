using System.Security.Cryptography;
using System.Text;

namespace SchedulerPlatform.IdentityServer.Services;

public static class PasswordGenerator
{
    private const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
    private const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string DigitChars = "0123456789";
    private const string SpecialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";
    
    public static string GeneratePassword(int length = 16)
    {
        if (length < 16)
        {
            throw new ArgumentException("Password length must be at least 16 characters", nameof(length));
        }

        var password = new StringBuilder();
        
        password.Append(GetRandomChar(UppercaseChars));
        
        for (int i = 0; i < 3; i++)
        {
            password.Append(GetRandomChar(DigitChars));
        }
        
        for (int i = 0; i < 2; i++)
        {
            password.Append(GetRandomChar(SpecialChars));
        }
        
        var allChars = LowercaseChars + UppercaseChars + DigitChars + SpecialChars;
        int remainingLength = length - password.Length;
        
        for (int i = 0; i < remainingLength; i++)
        {
            password.Append(GetRandomChar(allChars));
        }
        
        return Shuffle(password.ToString());
    }
    
    private static char GetRandomChar(string chars)
    {
        var randomIndex = RandomNumberGenerator.GetInt32(0, chars.Length);
        return chars[randomIndex];
    }
    
    private static string Shuffle(string input)
    {
        var array = input.ToCharArray();
        int n = array.Length;
        
        for (int i = n - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(0, i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
        
        return new string(array);
    }
}
