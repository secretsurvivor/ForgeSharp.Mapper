using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace ForgeSharp.Mapper.Reflection;

/// <summary>
/// Provides extension methods for configuring mappings using member initialization expressions.
/// </summary>
public static class ConfigureExtension
{
    /// <summary>
    /// Configures the mapper register using a member initialization expression that describes how to map from the source to the destination.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="register">The mapper register to configure.</param>
    /// <param name="expression">A member initialization expression describing the mapping.</param>
    /// <returns>The mapper register for further configuration.</returns>
    /// <exception cref="ArgumentException">Thrown if the expression is not a valid member initialization or contains no bindings.</exception>
    /// <exception cref="NotSupportedException">Thrown if a binding type is not supported.</exception>
    [RequiresUnreferencedCode("Uses reflection to construct mapping expressions at runtime. This may break when trimming or using Native AOT.")]
    public static IMapperRegister<TSource, TDestination> Configure<TSource, TDestination>(this IMapperRegister<TSource, TDestination> register, Expression<Func<TSource, TDestination, TDestination>> expression)
    {
        var sourceParameter = Expression.Parameter(typeof(TSource), "source");
        var destinationParameter = Expression.Parameter(typeof(TDestination), "destination");

        var halfReplaced = ParameterReplaceVisiter.Visit(expression.Parameters[0], sourceParameter, expression.Body);
        var replacedBody = ParameterReplaceVisiter.Visit(expression.Parameters[1], destinationParameter, halfReplaced);

        if (replacedBody is not MemberInitExpression initExpression)
        {
            throw new ArgumentException("Expression must be a MemberInitExpression.", nameof(expression));
        }

        if (initExpression.Bindings.Count == 0)
        {
            throw new ArgumentException("Expression must contain at least one binding.", nameof(expression));
        }

        foreach (var binding in initExpression.Bindings)
        {
            if (binding is MemberAssignment assignment)
            {
                register.AddAssignment(assignment.Member, Expression.Lambda(assignment.Expression, sourceParameter, destinationParameter));
            }
            else
            {
                throw new NotSupportedException($"Binding type '{binding.GetType().Name}' is not supported.");
            }
        }

        return register;
    }
}
