# ForgeSharp.Mapper

**ForgeSharp.Mapper** is a high-performance, type-safe, and extensible object mapping library for .NET. It enables you to define, configure, and execute mappings between different object types using a fluent, strongly-typed API. The library is designed for speed, maintainability, and ease of integration with modern .NET applications.

---

## Features

- **Fluent, Type-Safe API:**  
  Define mappings using expressive, chainable syntax with full compile-time safety.
- **Reflection-Free Runtime Mapping:**  
  Mappings are compiled to delegates for maximum performance and Native AOT.
- **Extensible Builder Pattern:**  
  Easily register and configure mappings for any type combination.
- **Validation Tools:**  
  Built-in utilities to detect unmapped properties and validate mapping completeness.
- **Dependency Injection Ready:**  
  Seamless integration with Microsoft.Extensions.DependencyInjection.
- **Advanced Scenarios:**  
  Supports custom mapping logic, property transformations, and cloning.

---

## Installation

Add the NuGet package to your project:

```sh
dotnet add package ForgeSharp.Mapper
```

---

## Quick Start

### 1. Define a Mapper Builder

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

### 2. Register the Mapper in DI

```csharp
services.AddMapper<MyMapperBuilder>();
```

### 3. Use the Mapper

```csharp
var mapper = serviceProvider.GetRequiredService<IMapperService>();
var destination = mapper.Map<Source, Destination>(sourceObject);
```

---

## Advanced Usage

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
