using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace ForgeSharp.Mapper.Reflection;

/// <summary>
/// Provides extension methods for configuring clone mappings using reflection.
/// </summary>
public static class CloneExtension
{
    /// <summary>
    /// Configures the mapper register to clone all readable and writable properties of type <typeparamref name="T"/> using reflection.
    /// </summary>
    /// <typeparam name="T">The type to clone.</typeparam>
    /// <param name="register">The mapper register to configure.</param>
    /// <returns>The mapper register for further configuration.</returns>
    /// <remarks>
    /// This method uses reflection to enumerate all properties of <typeparamref name="T"/> that are both readable and writable,
    /// and adds assignments for each property to the mapping configuration.
    /// </remarks>
    /// <exception cref="RequiresUnreferencedCodeAttribute">Uses reflection to construct mapping expressions at runtime. This may break when trimming or using Native AOT.</exception>
    [RequiresUnreferencedCode("Uses reflection to construct mapping expressions at runtime. This may break when trimming or using Native AOT.")]
    public static IMapperRegister<T, T> UseReflectionToClone<T>(this IMapperRegister<T, T> register)
    {
        var sourceParameter = Expression.Parameter(typeof(T), "source");

        foreach (var property in typeof(T).GetProperties())
        {
            if (property.CanRead && property.CanWrite)
            {
                var setter = Expression.Lambda(Expression.Property(sourceParameter, property), sourceParameter);
                register.AddAssignment(property, setter);
            }
        }

        return register;
    }
}
