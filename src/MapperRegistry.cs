using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ForgeSharp.Mapper;

/// <summary>
/// Provides a static factory for creating mapper registries.
/// </summary>
public static class MapperRegistry
{
    /// <summary>
    /// Creates a new mapper register for the specified source and destination types.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <returns>A new <see cref="IMapperRegister{TSource, TDestination}"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IMapperRegister<TSource, TDestination> Create<TSource, TDestination>()
    {
        return new MapperRegistry<TSource, TDestination>();
    }
}

/// <summary>
/// Represents a registry for configuring and compiling mappings between source and destination types.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
public class MapperRegistry<TSource, TDestination> : IMapperRegister<TSource, TDestination>
{
    private readonly LinkedList<IMapperEntry> _entries = new LinkedList<IMapperEntry>();

    /// <summary>
    /// Specifies the destination property to map to.
    /// </summary>
    /// <typeparam name="TValue">The property type.</typeparam>
    /// <param name="propertySelector">An expression selecting the destination property.</param>
    /// <returns>A link for specifying the source mapping.</returns>
    public IMapperLink<TSource, TDestination, TValue> To<TValue>(Expression<Func<TDestination, TValue>> propertySelector)
    {
        _entries.AddLast(new MapperEntry<TValue>(GetMember(propertySelector)));
        return new Link<TValue>(this);
    }

    /// <summary>
    /// Builds the mapping delegate for the configured mappings.
    /// </summary>
    /// <returns>A delegate that maps from source to destination.</returns>
    public MapperDelegate<TSource, TDestination> Build()
    {
        var sourceParameter = Expression.Parameter(typeof(TSource), "source");
        var templateParameter = Expression.Parameter(typeof(TDestination), "template");

        var assignmentList = new List<Expression>();

        foreach (var entry in _entries)
        {
            assignmentList.Add(entry.CompileExpression(sourceParameter, templateParameter));
        }

        assignmentList.Add(templateParameter);

        var body = Expression.Block(assignmentList);
        
        return Expression.Lambda<MapperDelegate<TSource, TDestination>>(body, sourceParameter, templateParameter).Compile();
    }

    /// <summary>
    /// Compiles the mapping delegate and returns a strongly-typed linker.
    /// </summary>
    /// <returns>A <see cref="MapperLinker{TSource, TDestination}"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MapperLinker<TSource, TDestination> Compile()
    {
        return new MapperLinker<TSource, TDestination>(Build());
    }

    /// <summary>
    /// Compiles the mapping delegate and returns a linker.
    /// </summary>
    /// <returns>An <see cref="IMapperLinker"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IMapperLinker CompileInternal_()
    {
        return Compile();
    }

    /// <summary>
    /// Returns the properties on the destination type that are not mapped.
    /// </summary>
    /// <returns>An enumerable of missing <see cref="PropertyInfo"/> objects.</returns>
    [RequiresUnreferencedCode("Uses reflection to construct mapping expressions at runtime. This may break when trimming or using Native AOT.")]
    public IEnumerable<PropertyInfo> MissingProperties()
    {
        var entries = _entries.ToFrozenDictionary(x => x.TargetMember.Name, x => x.TargetMember);

        foreach (var property in typeof(TDestination).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanWrite || !property.CanRead)
            {
                continue;
            }

            if (!entries.ContainsKey(property.Name))
            {
                yield return property;
            }
        }
    }

    private class Link<TValue>(MapperRegistry<TSource, TDestination> parent) : IMapperLink<TSource, TDestination, TValue>
    {
        private readonly IMapperEntry _currentEntry = parent._entries.Last!.Value;

        /// <summary>
        /// Specifies the source property or value to map from.
        /// </summary>
        /// <param name="propertySelector">An expression selecting the source property or value.</param>
        /// <returns>The parent mapper register for further configuration.</returns>
        public IMapperRegister<TSource, TDestination> From(Expression<Func<TSource, TValue>> propertySelector)
        {
            _currentEntry.ValueExpression = propertySelector;
            return parent;
        }

        /// <summary>
        /// Specifies the source and destination properties or values to map from.
        /// </summary>
        /// <param name="propertySelector">An expression selecting the source and destination properties or values.</param>
        /// <returns>The parent mapper register for further configuration.</returns>
        public IMapperRegister<TSource, TDestination> From(Expression<Func<TSource, TDestination, TValue>> propertySelector)
        {
            _currentEntry.ValueExpression = propertySelector;
            return parent;
        }
    }

    private interface IMapperEntry
    {
        public MemberInfo TargetMember { get; }
        public Expression? ValueExpression { get; set; }
        public Expression CompileExpression(ParameterExpression sourceParameter, ParameterExpression destinationExpression);
    }

    private class MapperEntry<TValue>(MemberExpression memberExpression) : IMapperEntry
    {
        public MemberInfo TargetMember => memberExpression.Member;
        public Expression? ValueExpression { get; set; }
        public Expression CompileExpression(ParameterExpression sourceParameter, ParameterExpression destinationExpression)
        {
            if (ValueExpression is null)
            {
                throw new InvalidOperationException($"No value expression was provided for destination member '{TargetMember.Name}'.");
            }

            Expression body;

            if (ValueExpression is Expression<Func<TSource, TDestination, TValue>> sourceDestinationSetter)
            {
                var halfReplaced = ParameterReplaceVisiter.Visit(sourceDestinationSetter.Parameters[0], sourceParameter, sourceDestinationSetter.Body);
                body = ParameterReplaceVisiter.Visit(sourceDestinationSetter.Parameters[1], destinationExpression, halfReplaced);
            }
            else if (ValueExpression is Expression<Func<TSource, TValue>> sourceSetter)
            {
                body = ParameterReplaceVisiter.Visit(sourceSetter.Parameters[0], sourceParameter, sourceSetter.Body);
            }
            // This will never happen but we have to put this here so it will compile
            else
            {
                throw new Exception();
            }

            var propertyExpression = Expression.MakeMemberAccess(destinationExpression, TargetMember);

            return Expression.Assign(propertyExpression, body);
        }
    }

    private static MemberExpression GetMember<TObject, TValue>(Expression<Func<TObject, TValue>> propertySelector)
    {
        var body = propertySelector.Body;

        if (body is UnaryExpression { Operand: MemberExpression memberExpr })
            body = memberExpr;

        if (body is not MemberExpression member)
            throw new ArgumentException("Only member access expressions are supported.", nameof(propertySelector));

        if (member.Member is not PropertyInfo { CanWrite: true })
            throw new ArgumentException($"The member '{member.Member.Name}' must be a writable property.", nameof(propertySelector));

        return member;
    }
}

/// <summary>
/// Represents a delegate for mapping from a source object to a destination object.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
/// <param name="source">The source object.</param>
/// <param name="template">The destination template object.</param>
/// <returns>The mapped destination object.</returns>
public delegate TDestination MapperDelegate<TSource, TDestination>(TSource source, TDestination template);

/// <summary>
/// Defines the contract for a mapper register.
/// </summary>
public interface IMapperRegister
{
    internal IMapperLinker CompileInternal_();

    /// <summary>
    /// Returns the properties on the destination type that are not mapped.
    /// </summary>
    /// <returns>An enumerable of missing <see cref="PropertyInfo"/> objects.</returns>
    [RequiresUnreferencedCode("Uses reflection to construct mapping expressions at runtime. This may break when trimming or using Native AOT.")]
    public IEnumerable<PropertyInfo> MissingProperties();

    /// <summary>
    /// Gets the source and destination types for the mapper.
    /// </summary>
    /// <param name="sourceType">The source type.</param>
    /// <param name="destinationType">The destination type.</param>
    public void GetTypes(out Type sourceType, out Type destinationType);
}

/// <summary>
/// Defines the contract for a strongly-typed mapper register.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
public interface IMapperRegister<TSource, TDestination> : IMapperRegister
{
    /// <summary>
    /// Specifies the destination property to map to.
    /// </summary>
    /// <typeparam name="TValue">The property type.</typeparam>
    /// <param name="propertySelector">An expression selecting the destination property.</param>
    /// <returns>A link for specifying the source mapping.</returns>
    public IMapperLink<TSource, TDestination, TValue> To<TValue>(Expression<Func<TDestination, TValue>> propertySelector);

    /// <summary>
    /// Builds the mapping delegate for the configured mappings.
    /// </summary>
    /// <returns>A delegate that maps from source to destination.</returns>
    public MapperDelegate<TSource, TDestination> Build();

    /// <summary>
    /// Compiles the mapping delegate and returns a strongly-typed linker.
    /// </summary>
    /// <returns>A <see cref="MapperLinker{TSource, TDestination}"/> instance.</returns>
    public MapperLinker<TSource, TDestination> Compile();

    void IMapperRegister.GetTypes(out Type sourceType, out Type destinationType)
    {
        sourceType = typeof(TSource);
        destinationType = typeof(TDestination);
    }
}

/// <summary>
/// Defines the contract for specifying the source mapping in a fluent mapping configuration.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
/// <typeparam name="TValue">The property type.</typeparam>
public interface IMapperLink<TSource, TDestination, TValue>
{
    /// <summary>
    /// Specifies the source property or value to map from.
    /// </summary>
    /// <param name="propertySelector">An expression selecting the source property or value.</param>
    /// <returns>The parent mapper register for further configuration.</returns>
    public IMapperRegister<TSource, TDestination> From(Expression<Func<TSource, TValue>> propertySelector);
    /// <summary>
    /// Specifies the source and destination properties or values to map from.
    /// </summary>
    /// <param name="propertySelector">An expression selecting the source and destination properties or values.</param>
    /// <returns>The parent mapper register for further configuration.</returns>
    public IMapperRegister<TSource, TDestination> From(Expression<Func<TSource, TDestination, TValue>> propertySelector);
}

internal class ParameterReplaceVisiter(ParameterExpression oldParameter, ParameterExpression newParameter) : ExpressionVisitor
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Expression Visit(ParameterExpression oldParameter, ParameterExpression newParameter, Expression body)
    {
        return new ParameterReplaceVisiter(oldParameter, newParameter).Visit(body);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (node == oldParameter)
        {
            return newParameter;
        }

        return base.VisitParameter(node);
    }
}
