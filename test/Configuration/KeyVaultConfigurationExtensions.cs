using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace test.Configuration
{
    public static class KeyVaultConfigurationExtensions
    {
        public static IServiceCollection AddKeyVault(this IServiceCollection services)
        {
#if DEBUG
            return services
                .AddSingleton<IKeyVaultClient>(svc =>
                    new KeyVaultClient((authority, resource, scope) => Task.FromResult("")))
                .AddSingleton<SecretResolver>();
#else
            return services
                .AddSingleton<AzureServiceTokenProvider>()
                .AddSingleton(svc => new KeyVaultClient.AuthenticationCallback(svc.GetService<AzureServiceTokenProvider>().KeyVaultTokenCallback))
                .AddSingleton<IKeyVaultClient>(svc => new KeyVaultClient(svc.GetService<KeyVaultClient.AuthenticationCallback>()))
                .AddSingleton<SecretResolver>();
#endif
        }

        public static OptionsBuilder<T> ResolveSecrets<T>(this OptionsBuilder<T> options, params Expression<Func<T, string>>[] expressions)
            where T : class
        {
            options.Configure((T obj, SecretResolver res) =>
            {
                foreach (var expression in expressions)
                {
                    var (current, resolved) = SecretResolver.ResolveFromExpression<T>(expression, obj, res);

                    if (current != resolved)
                    {
                        SetPropertyValue(expression, obj, resolved);
                    }
                }
            });
            return options;
        }

        private static void SetPropertyValue<T>(Expression<Func<T, string>> expression, object sourceObject, string resolved) where T : class
        {
            PropertyInfo propertyInfo;
            if (expression.Body is UnaryExpression unaryExpression)
            {
                propertyInfo = ((MemberExpression) unaryExpression.Operand).Member as PropertyInfo;
            }
            else
            {
                propertyInfo = ((MemberExpression) expression.Body).Member as PropertyInfo;
            }

            if (propertyInfo == null)
            {
                throw new ArgumentException("The lambda expression provided is not a property.");
            }

            var propertyName = propertyInfo.Name;
            var theType = sourceObject.GetType();
            var setter = theType.GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty);
            if (setter == null || setter.SetMethod == null)
            {
                throw new ArgumentException("The property provided in the expression is not settable.");
            }

            setter.SetMethod.Invoke(sourceObject, new object[] {resolved});
        }
    }
}