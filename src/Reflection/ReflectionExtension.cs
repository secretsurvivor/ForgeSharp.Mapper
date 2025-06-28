using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ForgeSharp.Mapper.Reflection;

/// <summary>
/// Provides extension methods for configuring mappings using reflection and expression trees.
/// </summary>
public static class ReflectionExtension
{
    /// <summary>
    /// Adds an assignment mapping for the specified member using the provided setter expression.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="register">The mapper register to configure.</param>
    /// <param name="member">The destination member (property or field).</param>
    /// <param name="setter">The setter expression for the value to assign.</param>
    /// <returns>The mapper register for further configuration.</returns>
    /// <exception cref="ArgumentException">Thrown if the setter return type does not match the member type or if the member type is not supported.</exception>
    [RequiresUnreferencedCode("Uses reflection to construct mapping expressions at runtime. This may break when trimming or using Native AOT.")]
    public static IMapperRegister<TSource, TDestination> AddAssignment<TSource, TDestination>(this IMapperRegister<TSource, TDestination> register, MemberInfo member, LambdaExpression setter)
    {
        var destinationParameter = Expression.Parameter(typeof(TDestination), "destination");
        var lambda = Expression.Lambda(Expression.MakeMemberAccess(destinationParameter, member), destinationParameter);

        var memberType = member switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new NotSupportedException($"Member type '{member.GetType().Name}' is not supported.")
        };

        if (setter.ReturnType != memberType)
        {
            throw new ArgumentException($"Setter expression must return type '{memberType.Name}'.", nameof(setter));
        }

        AddAssignment<TSource, TDestination>(memberType, register, lambda, setter);

        return register;
    }

    [RequiresUnreferencedCode("Uses reflection to construct mapping expressions at runtime. This may break when trimming or using Native AOT.")]
    private static void AddAssignment<TSource, TDestination>(Type valueType, IMapperRegister register, Expression member, Expression setter)
    {
        var linker = MapperLinkReflection.Create(typeof(TSource), typeof(TDestination), valueType, register);
        linker.Add(member, setter);
    }

    private interface IReflectionLinker
    {
        /// <summary>
        /// Adds a mapping for the specified member and setter expression.
        /// </summary>
        /// <param name="member">The member expression.</param>
        /// <param name="setter">The setter expression.</param>
        /// <returns>The reflection linker for chaining.</returns>
        public IReflectionLinker Add(Expression member, Expression setter);
    }

    private static class MapperLinkReflection
    {
        /// <summary>
        /// Creates a reflection linker for the specified types and register.
        /// </summary>
        /// <param name="sourceType">The source type.</param>
        /// <param name="destinationType">The destination type.</param>
        /// <param name="valueType">The value type.</param>
        /// <param name="register">The mapper register.</param>
        /// <returns>A reflection linker instance.</returns>
        [RequiresUnreferencedCode("Uses reflection to construct mapping expressions at runtime. This may break when trimming or using Native AOT.")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReflectionLinker Create(Type sourceType, Type destinationType, Type valueType, IMapperRegister register)
        {
            return (Activator.CreateInstance(typeof(MapperLinkReflection<,,>).MakeGenericType(sourceType, destinationType, valueType), register) as IReflectionLinker)!;
        }
    }

    private class MapperLinkReflection<TSource, TDestination, TValue> : IReflectionLinker
    {
        private readonly IMapperRegister<TSource, TDestination> _target;

        /// <summary>
        /// Initializes a new instance of the <see cref="MapperLinkReflection{TSource, TDestination, TValue}"/> class.
        /// </summary>
        /// <param name="target">The target mapper register.</param>
        /// <exception cref="ArgumentException">Thrown if the target is not of the expected type.</exception>
        public MapperLinkReflection(IMapperRegister target)
        {
            if (target is not IMapperRegister<TSource, TDestination> genericTarget)
            {
                throw new ArgumentException($"Target must be of type IMapperRegister<{typeof(TSource).Name}, {typeof(TDestination).Name}>.", nameof(target));
            }

            _target = genericTarget;
        }

        /// <inheritdoc/>
        public IReflectionLinker Add(Expression member, Expression setter)
        {
            if (member is not Expression<Func<TDestination, TValue>> memberLambda)
            {
                throw new ArgumentException("Member must be an expression of type Expression<Func<TDestination, TValue>>.", nameof(member));
            }
            
            if (setter is Expression<Func<TSource, TDestination, TValue>> setterDestinationLambda)
            {
                _target.To(memberLambda).From(setterDestinationLambda);
                return this;
            }
            else if (setter is Expression<Func<TSource, TValue>> setterLambda)
            {
                _target.To(memberLambda).From(setterLambda);
            }
            else
            {
                throw new ArgumentException("Setter must be an expression of type Expression<Func<TSource, TValue>>.", nameof(setter));
            }

            return this;
        }
    }
}
