# ForgeSharp.Mapper

**ForgeSharp.Mapper** is a high-performance, type-safe, and extensible object mapping library for .NET. 
It enables you to define, configure, and execute mappings between different object types using a fluent, strongly-typed API.
The library is designed for speed, maintainability, and ease of integration with modern .NET applications.

---

## Features

- **Fluent, Type-Safe API:**  
  Define mappings using expressive, chainable syntax with full compile-time safety.
- **Reflection-Free Runtime Mapping:**  
  Mappings are compiled to delegates for maximum performance.
- **Extensible Builder Pattern:**  
  Easily register and configure mappings for any type combination.
- **Validation Tools:**  
  Built-in utilities to detect unmapped properties and validate mapping completeness.
- **Dependency Injection Ready:**  
  Seamless integration with Microsoft.Extensions.DependencyInjection with the `ForgeSharp.Mapper.DependencyInjection` NuGet.
- **Advanced Scenarios:**  
  Supports custom mapping logic, context-aware mapping, property transformations, and cloning.

---

## Performance Comparison

At the time of writing, ForgeSharp.Mapper is significantly faster than other popular mapping libraries. 
Below is a benchmark* comparison against AutoMapper, Mapster, and manual mapping:

| Method            | Mean      | Error     | StdDev    | Ratio | RatioSD |
|------------------ |----------:|----------:|----------:|------:|--------:|
| Manual            |  5.321 ns | 0.1659 ns | 0.3942 ns |  1.01 |    0.10 |
| ForgeSharp.Mapper | 11.809 ns | 0.2449 ns | 0.2291 ns |  2.23 |    0.16 |
| Mapster           | 21.771 ns | 0.4347 ns | 0.8977 ns |  4.11 |    0.34 |
| AutoMapper        | 68.273 ns | 1.4161 ns | 2.2047 ns | 12.90 |    1.01 |

Forgesharp.Mapper is:
- ~46% faster than Mapster
- ~83% faster than AutoMapper

Despite its simplicity and lightweight footprint, ForgeSharp.Mapper delivers the same core functionality as other major mappers:
- Complex mapping support
- Custom value transformations
- Validation features

**The full benchmark project is included in the repository for reproducibility.*

---

## Installation

Add the NuGet package to your project:

```sh
dotnet add package ForgeSharp.Mapper
```

For Dependency Injection support, also install:
```sh
dotnet add package ForgeSharp.Mapper.DependencyInjection
```

---

## Quick Start

This mapping solution can be used in two ways: with Dependency Injection or without it.

### Define a Mapper Builder

```csharp
public class MyMapperBuilder : MapperBuilder
{
    public MyMapperBuilder()
    {
        Register<Source, Destination>()
            .To(d => d.Name).From(s => s.SourceName)
            .To(d => d.Age).From((s, d) => s.Years + 1);
    }
}
```

### Without Dependency Injection

Without Dependency Injection, you can create the IMapperService directly from the builder:

```csharp
var mapper = new MyMapperBuilder().BuildService();
mapper.Map<Source, Destination>(sourceObject);
```

### With Dependency Injection

This requires the `ForgeSharp.Mapper.DependencyInjection` NuGet package.

#### 1. Register the Mapper in DI

```csharp
services.AddMapper<MyMapperBuilder>();
```

#### 2. Use the Mapper

```csharp
var mapper = serviceProvider.GetRequiredService<IMapperService>();
var destination = mapper.Map<Source, Destination>(sourceObject);
```

---

## Advanced Usage

### Configure Extension (Recommended)
The `Configure` extension method allows you to define complex mappings in a single, expressive member-initializer expression. This makes your mapping configuration more readable and maintainable, especially for larger objects.
```csharp
Register<Source, Destination>()
    .Configure((src, dest) => new Destination
    {
        Name = src.SourceName,
        Age = src.Years + 1,
        IsActive = true
    });
```

You can also use `Configure` for context-aware mappings:
```csharp
RegisterWithContext<Source, MyContext, Destination>()
    .Configure((src, ctx, dest) => new Destination
    {
        Name = src.SourceName,
        UserId = ctx.CurrentUser,
        IsActive = true
    });
```

This just uses reflection to handle the fluent API, but the actual mapping is still compiled to delegates for performance.

### Cloning

```csharp
Register<MyType, MyType>().UseReflectionToClone();
```

### Validation

```csharp
var tester = serviceProvider.GetRequiredService<IMapperTester>();
foreach (var result in tester.ValidateMissingProperties())
{
    Console.WriteLine(result);
}
```

### Custom Reflection-Based Mapping

```csharp
register.AddAssignment(
    typeof(Destination).GetProperty(nameof(Destination.SomeProperty)),
    (Expression<Func<Source, string>>)(s => s.SomeSourceProperty)
);
```

### Context-aware Mapping
ForgeSharpMapper supports context-aware mappers, allowing you to inject a custom context object into the mapping logic.
```csharp
public class MyContext(Guid currentUser)
{
    public Guid CurrentUser { get; set; } = currentUser;
}
```
This context can then be injected into the mapping logic:
```csharp
RegisterWithContext<Source, MyContext, Destination>()
    .To(d => d.UserId).From((s, ctx) => ctx.CurrentUser)
    .To(d => d.Name).From(s => s.SourceName);
```

---

## API Overview

- **IMapperService**: Main entry point for mapping operations.
- **MapperBuilder**: Base class for registering and configuring mappings.
- **IMapperRegister**: Fluent interface for mapping configuration.
- **IMapperTester**: Utility for validating mapping completeness.
- **ReflectionExtension / ConfigureExtension / CloneExtension**: Helpers for advanced and reflection-based mapping scenarios.

---

## Project Structure

- `MapperRegistry.cs` – Core fluent mapping API and registry.
- `MapperService.cs` – Service and DI integration.
- `MapperTester.cs` – Validation and diagnostics.
- `Reflection/` – Extensions for reflection-based and advanced mapping.

---

## Requirements

- .NET 8.0 or later

---

## Contributing

Contributions are welcome! Please open issues or submit pull requests for bug fixes, improvements, or new features.

---

## License

[MIT License](LICENSE)

---

## Acknowledgements

- Inspired by AutoMapper and other mapping libraries, but designed for maximum performance, type safety and zero reflection.
- Built with modern C# features and best practices.

---

## Contact

For questions, suggestions, or support, please open an issue on GitHub.

---

**ForgeSharp.Mapper** – Fast, type-safe, and extensible mapping for .NET.
