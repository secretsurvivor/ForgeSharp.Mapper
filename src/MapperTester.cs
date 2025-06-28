using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ForgeSharp.Mapper;

/// <summary>
/// Provides functionality to test and validate registered mappers for missing property mappings.
/// </summary>
public class MapperTester(MapperBuilder builder) : IMapperTester
{
    /// <summary>
    /// Validates all registered mappers and returns results for missing destination properties.
    /// </summary>
    /// <returns>An enumerable of <see cref="MapperValidationResult"/> for each mapping.</returns>
    [RequiresUnreferencedCode("Uses reflection to construct mapping expressions at runtime. This may break when trimming or using Native AOT.")]
    public IEnumerable<MapperValidationResult> ValidateMissingProperties()
    {
        foreach (var registry in builder.Registers)
        {
            registry.GetTypes(out var sourceType, out var destinationType);

            yield return new MapperValidationResult(sourceType, destinationType, registry.MissingProperties());
        }
    }
}

/// <summary>
/// Defines the contract for a mapper tester that can validate missing property mappings.
/// </summary>
public interface IMapperTester
{
    /// <summary>
    /// Validates all registered mappers and returns results for missing destination properties.
    /// </summary>
    /// <returns>An enumerable of <see cref="MapperValidationResult"/> for each mapping.</returns>
    public IEnumerable<MapperValidationResult> ValidateMissingProperties();
}

/// <summary>
/// Represents the result of validating a mapping for missing destination properties.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct MapperValidationResult(Type sourceType, Type destinationType, IEnumerable<PropertyInfo> properties)
{
    /// <summary>
    /// Gets a value indicating whether there are any missing properties in the mapping.
    /// </summary>
    public bool HasMissingProperties => properties.Any();
    /// <summary>
    /// Gets the source type for the mapping.
    /// </summary>
    public Type SourceType => sourceType;
    /// <summary>
    /// Gets the destination type for the mapping.
    /// </summary>
    public Type DestinationType => destinationType;
    /// <summary>
    /// Gets the collection of missing destination properties.
    /// </summary>
    public IEnumerable<PropertyInfo> Properties { get; } = properties.ToList();

    /// <summary>
    /// Returns a string representation of the validation result, including missing properties if any.
    /// </summary>
    /// <returns>A string describing the validation result.</returns>
    public override readonly string ToString()
    {
        if (!HasMissingProperties)
        {
            return $"No missing mapping from {SourceType.Name} to {DestinationType.Name}";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Missing mapping from {SourceType.Name} to {DestinationType.Name}");

        foreach (var property in Properties)
        {
            builder.AppendLine($"- {property.PropertyType.Name} {property.Name}");
        }

        return builder.ToString();
    }
}
