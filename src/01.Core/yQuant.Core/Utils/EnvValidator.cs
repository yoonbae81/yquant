using System;

namespace yQuant.Core.Utils
{
    public static class EnvValidator
    {
        public static void Validate(string[]? additionalVars = null)
        {
            var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

            if (string.IsNullOrWhiteSpace(env))
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("CRITICAL ERROR: 'DOTNET_ENVIRONMENT' environment variable is not set.");
                Console.WriteLine("Please set it to 'Development' or 'Production' before running the application.");
                Console.ForegroundColor = originalColor;

                throw new InvalidOperationException("DOTNET_ENVIRONMENT is missing.");
            }

            if (additionalVars != null)
            {
                foreach (var variable in additionalVars)
                {
                    var value = Environment.GetEnvironmentVariable(variable);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        var originalColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"CRITICAL ERROR: '{variable}' environment variable is not set.");
                        Console.ForegroundColor = originalColor;
                        throw new InvalidOperationException($"{variable} is missing.");
                    }
                }
            }
        }
    }
}
