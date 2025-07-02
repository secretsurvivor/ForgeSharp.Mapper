using System.Runtime.CompilerServices;

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#else
using System.Collections.Concurrent;
#endif

namespace ForgeSharp.Mapper;

/// <summary>
/// Defines the contract for a mapping service that can map objects between types.
/// </summary>
public interface IMapperService
{
    /// <summary>
    /// Maps the source object to a new destination object of type <typeparamref name="TDestination"/>.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The source object.</param>
    /// <returns>The mapped destination object.</returns>
    public TDestination Map<TSource, TDestination>(TSource source) where TDestination : new();
    /// <summary>
    /// Maps the source object to the specified destination template object.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The source object.</param>
    /// <param name="template">The destination template object.</param>
    /// <returns>The mapped destination object.</returns>
    public TDestination Map<TSource, TDestination>(TSource source, TDestination template);
}

/// <summary>
/// Provides mapping services for converting objects between types using registered mappers.
/// </summary>
public sealed class MapperService : IMapperService
{
#if NET8_0_OR_GREATER
    /// <summary>
    /// Gets the frozen dictionary of registered mappers.
    /// </summary>
    private FrozenDictionary<(Type, Type), IMapperLinker> Mappers { get; }
#else
    /// <summary>
    /// Gets the dictionary of registered mappers.
    /// </summary>
    private ConcurrentDictionary<(Type, Type), IMapperLinker> Mappers { get; } = new();
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="MapperService"/> class with the specified builder.
    /// </summary>
    /// <param name="builder">The mapper builder used to register mappings.</param>
    public MapperService(MapperBuilder builder)
    {
        builder.Service = this;
#if NET8_0_OR_GREATER
        Mappers = builder.Compile().ToFrozenDictionary(x => x.GetTypes());
#else
        foreach (var linker in builder.Compile())
        {
            var types = linker.GetTypes();
            Mappers.TryAdd(types, linker);
        }
#endif
    }

    /// <summary>
    /// Maps the source object to a new destination object of type <typeparamref name="TDestination"/>.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The source object.</param>
    /// <returns>The mapped destination object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TDestination Map<TSource, TDestination>(TSource source) where TDestination : new()
    {
        return Map(source, new TDestination());
    }

    /// <summary>
    /// Maps the source object to the specified destination template object.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The source object.</param>
    /// <param name="template">The destination template object.</param>
    /// <returns>The mapped destination object.</returns>
    public TDestination Map<TSource, TDestination>(TSource source, TDestination template)
    {
        if (!Mappers.TryGetValue((typeof(TSource), typeof(TDestination)), out var mapperLinker))
        {
            throw new ArgumentException($"Missing mapper registry from '{typeof(TSource).Name}' to '{typeof(TDestination).Name}'");
        }

        if (mapperLinker is not MapperLinker<TSource, TDestination> linker)
        {
            throw new InvalidCastException($"MapperLinker type mismatch for '{typeof(TSource)}' → '{typeof(TDestination)}'");
        }

        return linker.Map(source, template);
    }
}

/// <summary>
/// Provides extension methods for the <see cref="IMapperService"/> interface.
/// </summary>
public static class MapperServiceExtension
{
    /// <summary>
    /// Clones the specified object by mapping it to a new instance of the same type.
    /// </summary>
    /// <typeparam name="TClone">The type to clone.</typeparam>
    /// <param name="service">The mapper service instance.</param>
    /// <param name="source">The source object to clone.</param>
    /// <returns>A new instance of <typeparamref name="TClone"/> with copied values.</returns>
    public static TClone Clone<TClone>(this IMapperService service, TClone source) where TClone : new()
    {
        return service.Map(source, new TClone());
    }
}

/// <summary>
/// Provides a base class for building and registering mappers.
/// </summary>
public abstract class MapperBuilder
{
    private readonly List<IMapperRegister> _registers = [];

    /// <summary>
    /// Gets the list of registered mappers.
    /// </summary>
    public IReadOnlyList<IMapperRegister> Registers => _registers.AsReadOnly();

    // Needs to not be nullable so we can later inject the service into
    // the builder
    internal IMapperService? Service { private get; set; }

    /// <summary>
    /// Registers a new mapper for the specified source and destination types.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <returns>The mapper register for further configuration.</returns>
    protected IMapperRegister<TSource, TDestination> Register<TSource, TDestination>()
    {
        var newRegister = new MapperRegistry<TSource, TDestination>();
        _registers.Add(newRegister);
        return newRegister;
    }

    /// <summary>
    /// Maps the source object to a new destination object of type <typeparamref name="TDestination"/> using the registered service.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The source object.</param>
    /// <returns>The mapped destination object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected TDestination Map<TSource, TDestination>(TSource source) where TDestination : new() => Service!.Map<TSource, TDestination>(source);

    /// <summary>
    /// Maps the source object to the specified destination template object using the registered service.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The source object.</param>
    /// <param name="template">The destination template object.</param>
    /// <returns>The mapped destination object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected TDestination Map<TSource, TDestination>(TSource source, TDestination template) => Service!.Map(source, template);

    /// <summary>
    /// Compiles all registered mappers and returns the linker objects.
    /// </summary>
    /// <returns>An enumerable of linker objects for the registered mappers.</returns>
    public IEnumerable<IMapperLinker> Compile()
    {
        foreach (var register in _registers)
        {
            yield return register.CompileInternal_();
        }
    }
}

/// <summary>
/// Provides extension methods for the <see cref="MapperBuilder"/> class.
/// </summary>
public static class MapperBuilderExtension
{
    /// <summary>
    /// Builds a new <see cref="MapperService"/> instance from the specified builder.
    /// </summary>
    /// <param name="builder">The mapper builder.</param>
    /// <returns>A new <see cref="MapperService"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IMapperService BuildService(this MapperBuilder builder)
    {
        return new MapperService(builder);
    }

    /// <summary>
    /// Builds a new <see cref="MapperTester"/> instance from the specified builder.
    /// </summary>
    /// <param name="builder">The mapper builder.</param>
    /// <returns>A new <see cref="MapperTester"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IMapperTester BuildTester(this MapperBuilder builder)
    {
        return new MapperTester(builder);
    }
}

/// <summary>
/// Defines the contract for a mapper linker that provides type information.
/// </summary>
public interface IMapperLinker
{
    /// <summary>
    /// Gets the source and destination types for the mapper.
    /// </summary>
    /// <returns>A tuple containing the source and destination types.</returns>
    public (Type sourceType, Type destinationType) GetTypes();
}

/// <summary>
/// Represents a strongly-typed mapper linker for mapping between source and destination types.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
/// <param name="mapperDelegate">The mapping delegate.</param>
public sealed class MapperLinker<TSource, TDestination>(MapperDelegate<TSource, TDestination> mapperDelegate) : IMapperLinker
{
    /// <summary>
    /// Gets the source and destination types for the mapper.
    /// </summary>
    /// <returns>A tuple containing the source and destination types.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (Type sourceType, Type destinationType) GetTypes()
    {
        return (typeof(TSource), typeof(TDestination));
    }

    /// <summary>
    /// Maps the source object to the destination template object using the compiled delegate.
    /// </summary>
    /// <param name="source">The source object.</param>
    /// <param name="template">The destination template object.</param>
    /// <returns>The mapped destination object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TDestination Map(TSource source, TDestination template)
    {
        return mapperDelegate(source, template);
    }
}

/// <summary>
/// Provides extension methods for <see cref="MapperLinker{TSource, TDestination}"/>.
/// </summary>
public static class MapperLinkerExtension
{
    /// <summary>
    /// Maps the source object to a new destination object of type <typeparamref name="TDestination"/> using the linker.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="linker">The mapper linker.</param>
    /// <param name="source">The source object.</param>
    /// <returns>The mapped destination object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TDestination Map<TSource, TDestination>(this MapperLinker<TSource, TDestination> linker, TSource source) where TDestination : new()
    {
        return linker.Map(source, new TDestination());
    }

    /// <summary>
    /// Clones the specified object by mapping it to a new instance of the same type using the linker.
    /// </summary>
    /// <typeparam name="TClone">The type to clone.</typeparam>
    /// <param name="linker">The mapper linker.</param>
    /// <param name="source">The source object to clone.</param>
    /// <returns>A new instance of <typeparamref name="TClone"/> with copied values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TClone Clone<TClone>(this MapperLinker<TClone, TClone> linker, TClone source) where TClone : new()
    {
        return linker.Map(source, new TClone());
    }
}
