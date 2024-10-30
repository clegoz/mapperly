namespace Riok.Mapperly.Tests.Mapping;

public class EnableNullChecksInProjectionsTest
{
    [Fact]
    public Task ClassToClassNullableProperty()
    {
        var source = TestSourceBuilder.Mapping(
            "System.Linq.IQueryable<A>",
            "System.Linq.IQueryable<C>",
            TestSourceBuilderOptions.WithDisabledNullChecksInProjections,
            "class A { public B? Nested { get; set; } }",
            "class B { public int? Id { get; set; } }",
            "class C { public D? Nested { get; set; } }",
            "class D { public int? Id { get; set; } }"
        );

        return TestHelper.VerifyGenerator(source);
    }

    [Fact]
    public Task ClassToClassNullableTargetProperty()
    {
        var source = TestSourceBuilder.Mapping(
            "System.Linq.IQueryable<A>",
            "System.Linq.IQueryable<C>",
            TestSourceBuilderOptions.WithDisabledNullChecksInProjections,
            "class A { public B? Nested { get; set; } }",
            "class B { public int Id { get; set; } }",
            "class C { public D? Nested { get; set; } }",
            "class D { public int? Id { get; set; } }"
        );

        return TestHelper.VerifyGenerator(source);
    }

    [Fact]
    public Task ClassToClassNullableSourceProperty()
    {
        var source = TestSourceBuilder.Mapping(
            "System.Linq.IQueryable<A>",
            "System.Linq.IQueryable<C>",
            TestSourceBuilderOptions.WithDisabledNullChecksInProjections,
            "class A { public B? Nested { get; set; } }",
            "class B { public int? Id { get; set; } }",
            "class C { public D? Nested { get; set; } }",
            "class D { public int Id { get; set; } }"
        );

        return TestHelper.VerifyGenerator(source);
    }

    [Fact]
    public Task ClassToClassNullablePropertyWithToString()
    {
        var source = TestSourceBuilder.Mapping(
            "System.Linq.IQueryable<A>",
            "System.Linq.IQueryable<C>",
            TestSourceBuilderOptions.WithDisabledNullChecksInProjections,
            "class A { public B? Nested { get; set; } }",
            "class B { public int? Id { get; set; } }",
            "class C { public D? Nested { get; set; } }",
            "class D { public string? Id { get; set; } }"
        );

        return TestHelper.VerifyGenerator(source);
    }
}
