#nullable disable

using ForgeSharp.Mapper.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq.Expressions;

namespace ForgeSharp.Mapper.Test;

public class MapperRegisterTest
{
    private class TestClass1
    {
        public string TestString { get; set; }
        public int TestNumber { get; set; }
        public IEnumerable<string> Strings { get; set; }
        public bool HasStrings { get; set; }
    }

    private class TestClass2
    {
        public string TestString { get; set; }
        public int TestNumber { get; set; }
    }

    private static MapperLinker<TSource, TDestination> CreateMapper<TSource, TDestination>(Action<IMapperRegister<TSource, TDestination>> configure)
    {
        var registry = MapperRegistry.Create<TSource, TDestination>();
        configure(registry);
        return registry.Compile();
    }

    private static IHost CreateService<TBuilder>(out IMapperService service) where TBuilder : MapperBuilder
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddMapper<MappingConfig>();
            })
            .Build();

        service = host.Services.GetRequiredService<IMapperService>();

        return host;
    }

    [Fact]
    public void CanMap()
    {
        var registry = CreateMapper<TestClass1, TestClass2>(x =>
        {
            x.To(to => to.TestNumber).From(from => from.TestNumber);
        });

        var result = registry.Map(new TestClass1 { TestNumber = 123 });

        Assert.Equal(123, result.TestNumber);
    }

    [Fact]
    public void CanMapWithMethods()
    {
        var registry = CreateMapper<TestClass1, TestClass2>(x =>
        {
            x.To(to => to.TestString).From(from => string.Join(", ", from.Strings));
        });

        var result = registry.Map(new TestClass1 { Strings = ["1", "2", "3", "4", "5",] });

        Assert.Equal("1, 2, 3, 4, 5", result.TestString);
    }

    [Fact]
    public void CanMerge()
    {
        var registry = CreateMapper<TestClass1, TestClass2>(x =>
        {
            x.To(to => to.TestString).From(from => from.TestString);
        });

        var result = registry.Map(new TestClass1 { TestString = "Hello World"}, new TestClass2 { TestNumber = 123 });

        Assert.Equal("Hello World", result.TestString);
        Assert.Equal(123, result.TestNumber);
    }

    [Fact]
    public void ConditionalMap()
    {
        var registry = CreateMapper<TestClass1, TestClass2>(x =>
        {
            x.To(to => to.TestString).From((source, destination) => source.HasStrings ? string.Join(", ", source.Strings) : destination.TestString);
        });

        var result1 = registry.Map(new TestClass1 { Strings = ["1", "2", "3", "4", "5",], HasStrings = false }, new TestClass2 { TestString = "Hello World"});

        Assert.Equal("Hello World", result1.TestString);

        var result2 = registry.Map(new TestClass1 { Strings = ["1", "2", "3", "4", "5",], HasStrings = true }, new TestClass2 { TestString = "Hello World" });

        Assert.Equal("1, 2, 3, 4, 5", result2.TestString);
    }

    private class NestedClass1
    {
        public string TestString { get; set; }
        public TestClass1 TestClass { get; set; }
    }

    private class NestedClass2
    {
        public string TestString { get; set; }
        public TestClass2 TestClass { get; set; }
    }

    public class MappingConfig : MapperBuilder
    {
        public MappingConfig()
        {
            Register<TestClass1, TestClass2>()
                .To(x => x.TestString).From(x => x.TestString)
                .To(x => x.TestNumber).From(x => x.TestNumber);

            Register<NestedClass1, NestedClass2>()
                .To(x => x.TestString).From(x => x.TestString)
                .To(x => x.TestClass).From(x => Map<TestClass1, TestClass2>(x.TestClass));
        }
    }

    [Fact]
    public void CanAccessThroughService()
    {
        using var host = CreateService<MappingConfig>(out var mapperService);

        var result = mapperService.Map<TestClass1, TestClass2>(new TestClass1 { TestString = "Hello World", TestNumber = 123 });

        Assert.Equal("Hello World", result.TestString);
        Assert.Equal(123, result.TestNumber);
    }

    [Fact]
    public void CanNestMaps()
    {
        using var host = CreateService<MappingConfig>(out var mapperService);

        var testClass = new NestedClass1
        {
            TestString = "Hello World",
            TestClass = new TestClass1
            {
                TestString = "Nested Hello World",
                TestNumber = 12345,
            }
        };

        var result = mapperService.Map<NestedClass1, NestedClass2>(testClass);

        Assert.Equal("Hello World", result.TestString);
        Assert.Equal("Nested Hello World", result.TestClass.TestString);
        Assert.Equal(12345, result.TestClass.TestNumber);
    }

    [Fact]
    public void CanUseReflectionToSetConfig()
    {
        var sourceParameter = Expression.Parameter(typeof(TestClass1), "source");
        var lambda = Expression.Lambda<Func<TestClass1, string>>(Expression.Property(sourceParameter, typeof(TestClass1).GetProperty(nameof(TestClass1.TestString))!), sourceParameter);

        var registry = CreateMapper<TestClass1, TestClass2>(x =>
        {
            x.AddAssignment(typeof(TestClass2).GetProperty(nameof(TestClass2.TestString))!, lambda);
        });

        var result = registry.Map(new TestClass1 { TestString = "Hello World" });

        Assert.Equal("Hello World", result.TestString);
    }

    [Fact]
    public void CanUseConfigureMethod()
    {
        var registry = CreateMapper<TestClass1, TestClass2>(x =>
        {
            x.Configure((src, dest) => new TestClass2
            {
                TestString = src.TestString,
            });
        });

        var result = registry.Map(new TestClass1 { TestString = "Hello World" });

        Assert.Equal("Hello World", result.TestString);
    }

    [Fact]
    public void CanUseCloneMethod()
    {
        var registry = CreateMapper<TestClass2, TestClass2>(x =>
        {
            x.UseReflectionToClone();
        });

        var result = registry.Map(new TestClass2 { TestString = "Hello World", TestNumber = 123 });

        Assert.Equal("Hello World", result.TestString);
        Assert.Equal(123, result.TestNumber);
    }

    [Fact]
    public void DoesntFindMapping()
    {
        using var host = CreateService<MappingConfig>(out var service);

        Assert.ThrowsAny<ArgumentException>(() => service.Map<TestClass1, TestClass1>(new TestClass1()));
    }
}