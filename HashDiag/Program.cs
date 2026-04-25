var hash = "$2a$11$R2XwHURhS96vM4coxOg8/.O4N8x5bTYQ4AlYxLTVWnY6W0QefAaaS";

Console.WriteLine($"Verify Admin@12345 against SQL hash: {BCrypt.Net.BCrypt.Verify("Admin@12345", hash)}");
Console.WriteLine($"Verify admin123 against SQL hash: {BCrypt.Net.BCrypt.Verify("admin123", hash)}");
Console.WriteLine($"Generated new hash for Admin@12345: {BCrypt.Net.BCrypt.HashPassword("Admin@12345")}");
