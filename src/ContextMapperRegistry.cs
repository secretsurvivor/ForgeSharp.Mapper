using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

namespace ForgeSharp.Mapper;

/*
 * I'm not too fond of creating an entirely new registry for context-aware mappers,
 * and basically duplicating the logic of the regular mapper registry.
 * It does create some extra maintenance having to support basically the same code
 * twice but with an extra type in the mix but I think it's worth it. Trying to support
 * contexts in the regular mapper whilst also supporting normal will create a lot of complexity.
 * They're designed to be left alone afterwards though so the maintenance cost should be minimal.
 * Keep the scope small and the code simple (even if the file is a bit big).
 */

/// <summary>
/// Provides a static factory for creating context-aware mapper registries.
/// </summary>
public class ContextMapperRegistry
{
    /// <summary>
    /// Creates a new context-aware mapper register for the specified source, context, and destination types.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <returns>A new <see cref="IContextMapperRegister{TSource, TContext, TDestination}"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IContextMapperRegister<TSource, TContext, TDestination> Create<TSource, TContext, TDestination>()
    {
        return new ContextMapperRegistry<TSource, TContext, TDestination>();
    }
}

/// <summary>
/// Represents a registry for configuring and compiling context-aware mappings between source, context, and destination types.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TContext">The context type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
public class ContextMapperRegistry<TSource, TContext, TDestination> : IContextMapperRegister<TSource, TContext, TDestination>
{
    private readonly LinkedList<IEntry> _entries = new LinkedList<IEntry>();

    /// <summary>
    /// Builds the mapping delegate for the configured context-aware mappings.
    /// </summary>
    /// <returns>A delegate that maps from source and context to destination.</returns>
    public ContextMapperDelegate<TSource, TContext, TDestination> Build()
    {
        var sourceParameter = Expression.Parameter(typeof(TSource), "source");
        var contextParameter = Expression.Parameter(typeof(TContext), "context");
        var destinationParameter = Expression.Parameter(typeof(TDestination), "template");
        var assignments = _entries.Select(entry => entry.CompileExpression(sourceParameter, contextParameter, destinationParameter)).ToArray();
        var body = Expression.Block(assignments.Concat([destinationParameter]));

        return Expression.Lambda<ContextMapperDelegate<TSource, TContext, TDestination>>(body, sourceParameter, contextParameter, destinationParameter).Compile();
    }

    /// <summary>
    /// Compiles the mapping delegate and returns a strongly-typed context-aware linker.
    /// </summary>
    /// <returns>A <see cref="ContextMapperLinker{TSource, TContext, TDestination}"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ContextMapperLinker<TSource, TContext, TDestination> Compile()
    {
        return new ContextMapperLinker<TSource, TContext, TDestination>(Build());
    }

    /// <summary>
    /// Specifies the destination property to map to.
    /// </summary>
    /// <typeparam name="TValue">The property type.</typeparam>
    /// <param name="to">An expression selecting the destination property.</param>
    /// <returns>A link for specifying the source/context mapping.</returns>
    public IContextMapperLink<TSource, TContext, TDestination, TValue> To<TValue>(Expression<Func<TDestination, TValue>> to)
    {
        _entries.AddLast(new Entry<TValue>(GetMember(to)));
        return new Link<TValue>(this);
    }

    /// <summary>
    /// Returns the properties on the destination type that are not mapped.
    /// </summary>
    /// <returns>An enumerable of missing <see cref="PropertyInfo"/> objects.</returns>
#if NET5_0_OR_GREATER
    [RequiresUnreferencedCode("Uses reflection to examine properties. This may break when trimming or using Native AOT.")]
#endif
    public IEnumerable<PropertyInfo> MissingProperties()
    {
#if NET8_0_OR_GREATER
        var entries = _entries.ToFrozenDictionary(x => x.TargetMember.Name, x => x.TargetMember);
#else
        var entries = _entries.ToDictionary(x => x.TargetMember.Name, x => x.TargetMember);   
#endif

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

    private class Link<TValue>(ContextMapperRegistry<TSource, TContext, TDestination> parent) : IContextMapperLink<TSource, TContext, TDestination, TValue>
    {
        private readonly IEntry _currentEntry = parent._entries.Last!.Value;

        /// <summary>
        /// Specifies the source property or value to map from.
        /// </summary>
        /// <param name="from">An expression selecting the source property or value.</param>
        /// <returns>The parent context mapper register for further configuration.</returns>
        public IContextMapperRegister<TSource, TContext, TDestination> From(Expression<Func<TSource, TValue>> from)
        {
            _currentEntry.ValueExpression = from;
            return parent;
        }

        /// <summary>
        /// Specifies the source and context properties or values to map from.
        /// </summary>
        /// <param name="fromContext">An expression selecting the source and context properties or values.</param>
        /// <returns>The parent context mapper register for further configuration.</returns>
        public IContextMapperRegister<TSource, TContext, TDestination> From(Expression<Func<TSource, TContext, TValue>> fromContext)
        {
            _currentEntry.ValueExpression = fromContext;
            return parent;
        }

        /// <summary>
        /// Specifies the source, context, and destination properties or values to map from.
        /// </summary>
        /// <param name="fromContext">An expression selecting the source, context, and destination properties or values.</param>
        /// <returns>The parent context mapper register for further configuration.</returns>
        public IContextMapperRegister<TSource, TContext, TDestination> From(Expression<Func<TSource, TContext, TDestination, TValue>> fromContext)
        {
            _currentEntry.ValueExpression = fromContext;
            return parent;
        }

        /// <summary>
        /// Specifies the context property or value to map from.
        /// </summary>
        /// <param name="context">An expression selecting the context property or value.</param>
        /// <returns>The parent context mapper register for further configuration.</returns>
        public IContextMapperRegister<TSource, TContext, TDestination> FromContext(Expression<Func<TContext, TValue>> context)
        {
            _currentEntry.ValueExpression = context;
            return parent;
        }
    }

    private interface IEntry
    {
        public MemberInfo TargetMember { get; }
        public LambdaExpression? ValueExpression { get; set; }
        public Expression CompileExpression(ParameterExpression sourceParameter, ParameterExpression contextParameter, ParameterExpression destinationParameter);
    }

    private sealed class Entry<TValue>(MemberExpression memberExpression) : IEntry
    {
        public MemberInfo TargetMember => memberExpression.Member;
        public LambdaExpression? ValueExpression { get; set; }
        public Expression CompileExpression(ParameterExpression sourceParameter, ParameterExpression contextParameter, ParameterExpression destinationParameter)
        {
            if (ValueExpression is null)
            {
                throw new InvalidOperationException("ValueExpression must be set before compiling the expression.");
            }

            var propertyExpression = Expression.MakeMemberAccess(destinationParameter, TargetMember);

            switch (ValueExpression)
            {
                case Expression<Func<TSource, TValue>> sourceLambda:
                    return Expression.Assign(propertyExpression, ParameterReplaceVisiter.Visit(sourceLambda.Parameters[0], sourceParameter, sourceLambda.Body));

                case Expression<Func<TSource, TContext, TValue>> contextLambda:
                    var sourceBody = ParameterReplaceVisiter.Visit(contextLambda.Parameters[0], sourceParameter, contextLambda.Body);
                    var contextBody = ParameterReplaceVisiter.Visit(contextLambda.Parameters[1], contextParameter, sourceBody);
                    return Expression.Assign(propertyExpression, ParameterReplaceVisiter.Visit(contextLambda.Parameters[1], contextParameter, contextBody));

                case Expression<Func<TSource, TContext, TDestination, TValue>> destinationLambda:
                    var sourceBody1 = ParameterReplaceVisiter.Visit(destinationLambda.Parameters[0], sourceParameter, destinationLambda.Body);
                    var contextBody1 = ParameterReplaceVisiter.Visit(destinationLambda.Parameters[1], contextParameter, sourceBody1);
                    var destinationBody = ParameterReplaceVisiter.Visit(destinationLambda.Parameters[2], destinationParameter, contextBody1);
                    return Expression.Assign(propertyExpression, ParameterReplaceVisiter.Visit(destinationLambda.Parameters[0], sourceParameter, destinationBody));

                case Expression<Func<TContext, TValue>> contextOnlyLambda:
                    return Expression.Assign(propertyExpression, ParameterReplaceVisiter.Visit(contextOnlyLambda.Parameters[0], contextParameter, contextOnlyLambda.Body));

                default:
                    throw new NotSupportedException("Unsupported ValueExpression type.");
            }
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

    /// <summary>
    /// Compiles the mapping delegate and returns a context-aware linker.
    /// </summary>
    /// <returns>An <see cref="IContextMapperLinker"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IContextMapperLinker CompileInternal_() => Compile();

    /// <summary>
    /// Gets the source, context, and destination types for the mapper.
    /// </summary>
    /// <param name="source">The source type.</param>
    /// <param name="context">The context type.</param>
    /// <param name="destination">The destination type.</param>
    public void GetTypes(out Type source, out Type context, out Type destination)
    {
        source = typeof(TSource);
        context = typeof(TContext);
        destination = typeof(TDestination);
    }
}

/// <summary>
/// Defines the contract for a context-aware mapper register.
/// </summary>
public interface IContextMapperRegister
{
    internal IContextMapperLinker CompileInternal_();
    /// <summary>
    /// Returns the properties on the destination type that are not mapped.
    /// </summary>
    /// <returns>An enumerable of missing <see cref="PropertyInfo"/> objects.</returns>
    public IEnumerable<PropertyInfo> MissingProperties();
    /// <summary>
    /// Gets the source, context, and destination types for the mapper.
    /// </summary>
    /// <param name="source">The source type.</param>
    /// <param name="context">The context type.</param>
    /// <param name="destination">The destination type.</param>
    public void GetTypes(out Type source, out Type context, out Type destination);
}

/// <summary>
/// Defines the contract for a strongly-typed context-aware mapper register.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TContext">The context type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
public interface IContextMapperRegister<TSource, TContext, TDestination> : IContextMapperRegister
{
    /// <summary>
    /// Specifies the destination property to map to.
    /// </summary>
    /// <typeparam name="TValue">The property type.</typeparam>
    /// <param name="to">An expression selecting the destination property.</param>
    /// <returns>A link for specifying the source/context mapping.</returns>
    public IContextMapperLink<TSource, TContext, TDestination, TValue> To<TValue>(Expression<Func<TDestination, TValue>> to);
    /// <summary>
    /// Compiles the mapping delegate and returns a strongly-typed context-aware linker.
    /// </summary>
    /// <returns>A <see cref="ContextMapperLinker{TSource, TContext, TDestination}"/> instance.</returns>
    public ContextMapperLinker<TSource, TContext, TDestination> Compile();
    /// <summary>
    /// Builds the mapping delegate for the configured context-aware mappings.
    /// </summary>
    /// <returns>A delegate that maps from source and context to destination.</returns>
    public ContextMapperDelegate<TSource, TContext, TDestination> Build();
}

/// <summary>
/// Represents a delegate for mapping from a source object and context to a destination object.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TContext">The context type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
/// <param name="source">The source object.</param>
/// <param name="context">The context object.</param>
/// <param name="template">The destination template object.</param>
/// <returns>The mapped destination object.</returns>
public delegate TDestination ContextMapperDelegate<TSource, TContext, TDestination>(TSource source, TContext context, TDestination template);

/// <summary>
/// Defines the contract for specifying the source/context mapping in a fluent mapping configuration.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TContext">The context type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
/// <typeparam name="TValue">The property type.</typeparam>
public interface IContextMapperLink<TSource, TContext, TDestination, TValue>
{
    /// <summary>
    /// Specifies the source property or value to map from.
    /// </summary>
    /// <param name="from">An expression selecting the source property or value.</param>
    /// <returns>The parent context mapper register for further configuration.</returns>
    public IContextMapperRegister<TSource, TContext, TDestination> From(Expression<Func<TSource, TValue>> from);
    /// <summary>
    /// Specifies the source and context properties or values to map from.
    /// </summary>
    /// <param name="fromContext">An expression selecting the source and context properties or values.</param>
    /// <returns>The parent context mapper register for further configuration.</returns>
    public IContextMapperRegister<TSource, TContext, TDestination> From(Expression<Func<TSource, TContext, TValue>> fromContext);
    /// <summary>
    /// Specifies the source, context, and destination properties or values to map from.
    /// </summary>
    /// <param name="fromContext">An expression selecting the source, context, and destination properties or values.</param>
    /// <returns>The parent context mapper register for further configuration.</returns>
    public IContextMapperRegister<TSource, TContext, TDestination> From(Expression<Func<TSource, TContext, TDestination, TValue>> fromContext);
    /// <summary>
    /// Specifies the context property or value to map from.
    /// </summary>
    /// <param name="context">An expression selecting the context property or value.</param>
    /// <returns>The parent context mapper register for further configuration.</returns>
    public IContextMapperRegister<TSource, TContext, TDestination> FromContext(Expression<Func<TContext, TValue>> context);
}

/// <summary>
/// Defines the contract for a context-aware mapper linker that provides type information.
/// </summary>
public interface IContextMapperLinker
{
    /// <summary>
    /// Gets the source, context, and destination types for the mapper.
    /// </summary>
    /// <returns>A tuple containing the source, context, and destination types.</returns>
    public (Type source, Type context, Type destination) GetTypes();
}

/// <summary>
/// Represents a strongly-typed context-aware mapper linker for mapping between source, context, and destination types.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TContext">The context type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
/// <param name="mapperDelegate">The mapping delegate.</param>
public sealed class ContextMapperLinker<TSource, TContext, TDestination>(ContextMapperDelegate<TSource, TContext, TDestination> mapperDelegate) : IContextMapperLinker
{
    /// <summary>
    /// Gets the source, context, and destination types for the mapper.
    /// </summary>
    /// <returns>A tuple containing the source, context, and destination types.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (Type source, Type context, Type destination) GetTypes()
    {
        return (typeof(TSource), typeof(TContext), typeof(TDestination));
    }

    /// <summary>
    /// Maps the source and context objects to the destination template object using the compiled delegate.
    /// </summary>
    /// <param name="source">The source object.</param>
    /// <param name="context">The context object.</param>
    /// <param name="destination">The destination template object.</param>
    /// <returns>The mapped destination object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TDestination MapAndInject(TSource source, TContext context, TDestination destination)
    {
        return mapperDelegate(source, context, destination);
    }
}

/// <summary>
/// Provides extension methods for <see cref="ContextMapperLinker{TSource, TContext, TDestination}"/>.
/// </summary>
public static class ContextMapperRegistryExtension
{
    /// <summary>
    /// Maps the source and context objects to a new destination object of type <typeparamref name="TDestination"/> using the linker.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="linker">The context mapper linker.</param>
    /// <param name="source">The source object.</param>
    /// <param name="context">The context object.</param>
    /// <returns>The mapped destination object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TDestination MapAndInject<TSource, TContext, TDestination>(this ContextMapperLinker<TSource, TContext, TDestination> linker, TSource source, TContext context)
        where TDestination : new()
    {
        return linker.MapAndInject(source, context, new TDestination());
    }
}
