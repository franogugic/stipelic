using System.Reflection;
using CreatorPlatform.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary>
/// The rate-limit catalog is the only place policies are registered, so every <c>[EnableRateLimiting]</c> must name
/// a catalog entry (an unknown name only fails at request time, with a 500).
/// </summary>
public sealed class RateLimitPolicyCatalogTests
{
    private static readonly IReadOnlyList<(string Location, string? PolicyName)> Usages = FindUsages();

    private static List<(string Location, string? PolicyName)> FindUsages()
    {
        var controllers = typeof(RateLimitPolicies).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(type));

        var usages = new List<(string, string?)>();
        foreach (var controller in controllers)
        {
            foreach (var attribute in controller.GetCustomAttributes<EnableRateLimitingAttribute>(inherit: true))
                usages.Add((controller.Name, attribute.PolicyName));

            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var action in actions)
            {
                foreach (var attribute in action.GetCustomAttributes<EnableRateLimitingAttribute>(inherit: true))
                    usages.Add(($"{controller.Name}.{action.Name}", attribute.PolicyName));
            }
        }

        return usages;
    }

    [Fact]
    public void EveryEnableRateLimiting_NamesACatalogPolicy()
    {
        var catalogNames = RateLimitPolicies.Catalog.Select(policy => policy.Name).ToHashSet(StringComparer.Ordinal);

        var unknown = Usages
            .Where(usage => usage.PolicyName is null || !catalogNames.Contains(usage.PolicyName))
            .Select(usage => $"{usage.Location}: {usage.PolicyName ?? "<no name>"}")
            .ToList();

        Assert.NotEmpty(Usages);
        Assert.Empty(unknown);
    }

    [Fact]
    public void TheCatalog_HasNoDuplicateNames()
    {
        var duplicates = RateLimitPolicies.Catalog
            .GroupBy(policy => policy.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(duplicates);
        Assert.DoesNotContain(RateLimitPolicies.Catalog,
            policy => string.Equals(policy.Name, RateLimitPolicies.Global.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EveryPolicyConstant_HasACatalogEntry_AndEveryEntryAConstant()
    {
        var constants = typeof(RateLimitPolicies)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal)
            .ToList();

        var catalogNames = RateLimitPolicies.Catalog
            .Select(policy => policy.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(constants, catalogNames);
    }

    [Fact]
    public void EveryPolicy_HasAPositiveLimitAndWindow()
    {
        foreach (var policy in RateLimitPolicies.Catalog.Append(RateLimitPolicies.Global))
        {
            Assert.True(policy.PermitLimit > 0, $"{policy.Name}: PermitLimit must be positive.");
            Assert.True(policy.Window > TimeSpan.Zero, $"{policy.Name}: Window must be positive.");
        }
    }
}
