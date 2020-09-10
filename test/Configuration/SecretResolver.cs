using System;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.Azure.KeyVault;
using Microsoft.Extensions.Logging;

namespace test.Configuration
{
    public class SecretResolver
    {
        private readonly IKeyVaultClient _keyVaultClient;
        private readonly ILogger<SecretResolver> _log;

        public SecretResolver(IKeyVaultClient keyVaultClient, ILogger<SecretResolver> log)
        {
            _keyVaultClient = keyVaultClient;
            _log = log;
        }

        public string Resolve(string candidate)
        {
            var (matches, secretConnectionString) = IsKeyVaultReference(candidate);
            if (!matches)
            {
                return candidate;
            }
            var secret = _keyVaultClient.GetSecretAsync(secretConnectionString).GetAwaiter().GetResult();
            return secret.Value;
        }

        private (bool matches, string secretConnectionString) IsKeyVaultReference(string candidate)
        {
            var match = Regex.Match(candidate, @"@Microsoft\.KeyVault\(SecretUri=(?<url>.+)\)");
            if (match.Success) // contains URL
            {
                _log.LogWarning("Secret not automatically resolved. Falling back to explicit client access using URL {url}", match.Groups["url"]);
                return (true, match.Groups["url"].Value);
            }

            match = Regex.Match(candidate, @"@Microsoft\.KeyVault\(VaultName=(?<vaultName>.+);SecretName=(?<secretName>.+);SecretVersion=(?<secretVersion>.+)\)");
            if (match.Success) // contains parts
            {
                _log.LogWarning("Secret {secretName} not automatically resolved from vault {vaultName}. Falling back to explicit client access to version {secretVersion}", match.Groups["secretName"], match.Groups["vaultName"], match.Groups["secretVersion"]);
                return (true, $"https://{match.Groups["vaultName"]}.vault.azure.net/secrets/{match.Groups["secretName"]}/{match.Groups["secretVersion"]}");
            }
            _log.LogDebug("Secret successfully retrieved from app settings without using SecretClient.");
            return (false, string.Empty);
        }

        public static (string candidate, string resolved) ResolveFromExpression<T>(Expression<Func<T, string>> expression, T sourceObject, SecretResolver resolver) where T : class
        {
            var currentResult = expression.Compile()(sourceObject);
            var finalResult = resolver.Resolve(currentResult);
            return (currentResult, finalResult);
        }

    }
}