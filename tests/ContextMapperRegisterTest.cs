#nullable disable

using ForgeSharp.Mapper.Reflection;

namespace ForgeSharp.Mapper.Test
{
    public class ContextMapperRegisterTest
    {
        /*
         * I know the tests are bit sparse for this feature but the context mapper
         * basically uses the exact same backend logic as the regular mapper so
         * any of the tests in MapperRegisterTest should also apply here too.
         */

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

        private class TestContext(string testString)
        {
            public string GetString() => testString;

            public int Parseint(string value, int defaultValue = default)
            {
                if (int.TryParse(value, out var result))
                {
                    return result;
                }

                return defaultValue;
            }
        }

        private static ContextMapperLinker<TSource, TContext, TDestination> CreateMapper<TSource, TContext, TDestination>(Action<IContextMapperRegister<TSource, TContext, TDestination>> configure)
        {
            var registry = ContextMapperRegistry.Create<TSource, TContext, TDestination>();
            configure(registry);
            return registry.Compile();
        }

        [Fact]
        public void CanMapWithContext()
        {
            var registry = CreateMapper<TestClass1, TestContext, TestClass2>(x =>
            {
                x.To(dest => dest.TestNumber).From((src, context) => context.Parseint(src.TestString, -1));
                x.To(dest => dest.TestString).From((src, context) => context.GetString());
            });

            var context = new TestContext("Test String");
            var result = registry.MapAndInject(new TestClass1 { TestString = "123", TestNumber = 456 }, context);

            Assert.Equal("Test String", result.TestString);
            Assert.Equal(123, result.TestNumber);
        }

        [Fact]
        public void CanMapWithConfigure()
        {
            var registry = CreateMapper<TestClass1, TestContext, TestClass2>(x =>
            {
                x.Configure((src, ctx, dest) => new TestClass2
                {
                    TestNumber = ctx.Parseint(src.TestString, -1),
                    TestString = ctx.GetString()
                });
            });

            var context = new TestContext("Test String");
            var result = registry.MapAndInject(new TestClass1 { TestString = "123", TestNumber = 456 }, context);

            Assert.Equal("Test String", result.TestString);
            Assert.Equal(123, result.TestNumber);
        }
    }
}
