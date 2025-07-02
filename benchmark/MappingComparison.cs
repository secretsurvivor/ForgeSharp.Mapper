#nullable disable

using AutoMapper;
using BenchmarkDotNet.Attributes;
using Mapster;

// Automapper requires this namespace for configuration
using Microsoft.Extensions.Logging.Abstractions;

namespace ForgeSharp.Mapper.Benchmark;

public class MappingComparison
{
    private User _source;
    private IMapper _autoMapper;
    MapperLinker<User, UserDto> _forgeSharpMapper;

    [GlobalSetup]
    public void Setup()
    {
        _source = new User { Id = 1, Name = "John Doe", Age = 30 };

        // AutoMapper setup
        var cfg = new MapperConfigurationExpression();
        cfg.CreateMap<User, UserDto>();

        var config = new MapperConfiguration(cfg, NullLoggerFactory.Instance);
        _autoMapper = config.CreateMapper();

        // ForgeSharp.Mapper setup
        _forgeSharpMapper = MapperRegistry.Create<User, UserDto>()
            .To(dest => dest.Id).From(src => src.Id)
            .To(dest => dest.Name).From(src => src.Name)
            .To(dest => dest.Age).From(src => src.Age)
            .Compile();
    }

    [Benchmark(Baseline = true)]
    public UserDto Manual() => new UserDto { Id = _source.Id, Name = _source.Name, Age = _source.Age };

    [Benchmark]
    public UserDto AutoMapper() => _autoMapper.Map<UserDto>(_source);

    // Doesn't need a setup but method does require it to be warmed up before benchmarking
    // otherwise it will skew the results towards the precompiled delegates
    [Benchmark]
    public UserDto Mapster() => _source.Adapt<UserDto>();

    [Benchmark]
    public UserDto ForgeSharpMapper() => _forgeSharpMapper.Map(_source);
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
}
