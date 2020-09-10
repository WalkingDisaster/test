using System;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Extensions.Options;

namespace test
{
    public static class Extensions
    {
        public static void Configure<T>(this IOptionsMonitor<T> monitor, Action<T> setValues)
        {
            monitor.OnChange(setValues);
            setValues(monitor.CurrentValue);
        }
    }
}