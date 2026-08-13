using System.ComponentModel.DataAnnotations;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// Guards a trap this codebase has now fallen into twice.
///
/// Put a validation attribute on a positional record's parameter and the compiler places it on the
/// generated property. MVC refuses to bind such a type and throws, so the endpoint answers 500 for
/// every request — and no unit test notices, because model binding only happens in the web host.
/// The second occurrence reached production on the public contact form.
///
/// Rather than trust reviewers to remember, this asserts the rule directly against the assembly.
/// </summary>
public class RequestContractTests
{
    [Fact]
    public void No_request_record_carries_validation_metadata_on_a_constructor_property()
    {
        var offenders = new List<string>();

        foreach (var type in typeof(Desk.Api.Controllers.PublicEnquiryRequest).Assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract) continue;

            // Positional record: a constructor whose parameters mirror the properties.
            var ctorParams = type.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.Name)
                .Where(n => n is not null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (ctorParams.Count == 0) continue;

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!ctorParams.Contains(prop.Name)) continue;
                if (prop.GetCustomAttributes<ValidationAttribute>(inherit: true).Any())
                    offenders.Add($"{type.FullName}.{prop.Name}");
            }
        }

        offenders.Should().BeEmpty(
            "MVC throws on binding these types, so every request to the endpoint returns 500. " +
            "Move the rule into the service, or attach it to the constructor parameter.");
    }
}
