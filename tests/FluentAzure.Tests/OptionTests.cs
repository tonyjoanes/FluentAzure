using System.Collections.Concurrent;
using FluentAssertions;
using FluentAzure.Core;

namespace FluentAzure.Tests.Core;

/// <summary>
/// Comprehensive tests for the Option&lt;T&gt; monad implementation.
/// Tests cover all functionality including Some/None scenarios, Map, Bind, Match, operators, and thread safety.
/// </summary>
public class OptionTests
{
    #region Some Creation Tests

    [Fact]
    public void Some_WithValue_ShouldCreateOptionWithValue()
    {
        // Arrange & Act
        var option = Option<int>.Some(42);

        // Assert
        option.HasValue.Should().BeTrue();
        option.IsNone.Should().BeFalse();
        option.Value.Should().Be(42);
    }

    [Fact]
    public void Some_WithNullValue_ShouldCreateOptionWithNullValue()
    {
        // Arrange & Act
        var option = Option<string?>.Some(null);

        // Assert
        option.HasValue.Should().BeTrue();
        option.IsNone.Should().BeFalse();
        option.Value.Should().BeNull();
    }

    [Fact]
    public void Some_StaticMethod_ShouldCreateOptionWithValue()
    {
        // Arrange & Act
        var option = Option.Some("test");

        // Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().Be("test");
    }

    #endregion

    #region None Creation Tests

    [Fact]
    public void None_ShouldCreateEmptyOption()
    {
        // Arrange & Act
        var option = Option<int>.None();

        // Assert
        option.HasValue.Should().BeFalse();
        option.IsNone.Should().BeTrue();
    }

    [Fact]
    public void None_StaticMethod_ShouldCreateEmptyOption()
    {
        // Arrange & Act
        var option = Option.None<string>();

        // Assert
        option.HasValue.Should().BeFalse();
        option.IsNone.Should().BeTrue();
    }

    [Fact]
    public void FromNullable_WithNullValue_ShouldCreateNone()
    {
        // Arrange
        string? nullValue = null;

        // Act
        var option = Option<string>.FromNullable(nullValue);

        // Assert
        option.IsNone.Should().BeTrue();
    }

    [Fact]
    public void FromNullable_WithValue_ShouldCreateSome()
    {
        // Arrange
        string? value = "test";

        // Act
        var option = Option<string>.FromNullable(value);

        // Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().Be("test");
    }

    #endregion

    #region Property Access Tests

    [Fact]
    public void Value_OnOptionWithValue_ShouldReturnValue()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act & Assert
        option.Value.Should().Be(42);
    }

    [Fact]
    public void Value_OnNone_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var option = Option<int>.None();

        // Act & Assert
        var act = () => option.Value;
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Cannot access value of an empty option");
    }

    #endregion

    #region Map Tests

    [Fact]
    public void Map_OnOptionWithValue_ShouldTransformValue()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var mapped = option.Map(x => x.ToString());

        // Assert
        mapped.HasValue.Should().BeTrue();
        mapped.Value.Should().Be("42");
    }

    [Fact]
    public void Map_OnNone_ShouldReturnNone()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var mapped = option.Map(x => x.ToString());

        // Assert
        mapped.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Map_WithNullMapper_ShouldThrowArgumentNullException()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act & Assert
        var act = () => option.Map<string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Map_ChainedOperations_ShouldWork()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var mapped = option.Map(x => x * 2).Map(x => x.ToString()).Map(x => $"Result: {x}");

        // Assert
        mapped.HasValue.Should().BeTrue();
        mapped.Value.Should().Be("Result: 84");
    }

    #endregion

    #region Bind Tests

    [Fact]
    public void Bind_OnOptionWithValue_ShouldTransformValue()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var bound = option.Bind(x => Option<string>.Some(x.ToString()));

        // Assert
        bound.HasValue.Should().BeTrue();
        bound.Value.Should().Be("42");
    }

    [Fact]
    public void Bind_OnOptionWithValueToNone_ShouldReturnNone()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var bound = option.Bind(x => Option<string>.None());

        // Assert
        bound.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Bind_OnNone_ShouldReturnNone()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var bound = option.Bind(x => Option<string>.Some(x.ToString()));

        // Assert
        bound.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Bind_WithNullBinder_ShouldThrowArgumentNullException()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act & Assert
        var act = () => option.Bind<string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Bind_ChainedOperations_ShouldWork()
    {
        // Arrange
        var option = Option<int>.Some(10);

        // Act
        var bound = option
            .Bind(x => x > 5 ? Option<int>.Some(x * 2) : Option<int>.None())
            .Bind(x => x < 100 ? Option<string>.Some(x.ToString()) : Option<string>.None());

        // Assert
        bound.HasValue.Should().BeTrue();
        bound.Value.Should().Be("20");
    }

    [Fact]
    public void Bind_ChainedOperations_WithNone_ShouldStopAtFirstNone()
    {
        // Arrange
        var option = Option<int>.Some(2);

        // Act
        var bound = option
            .Bind(x => x > 5 ? Option<int>.Some(x * 2) : Option<int>.None())
            .Bind(x => x < 100 ? Option<string>.Some(x.ToString()) : Option<string>.None());

        // Assert
        bound.IsNone.Should().BeTrue();
    }

    [Fact]
    public void FlatMap_ShouldBehaveLikeBind()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var flatMapped = option.FlatMap(x => Option<string>.Some(x.ToString()));
        var bound = option.Bind(x => Option<string>.Some(x.ToString()));

        // Assert
        flatMapped.Should().Be(bound);
    }

    #endregion

    #region Match Tests

    [Fact]
    public void Match_OnOptionWithValue_ShouldExecuteSomeFunction()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var matched = option.Match(some: value => $"Some: {value}", none: () => "None");

        // Assert
        matched.Should().Be("Some: 42");
    }

    [Fact]
    public void Match_OnNone_ShouldExecuteNoneFunction()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var matched = option.Match(some: value => $"Some: {value}", none: () => "None");

        // Assert
        matched.Should().Be("None");
    }

    [Fact]
    public void Match_ActionOverload_OnOptionWithValue_ShouldExecuteSomeAction()
    {
        // Arrange
        var option = Option<int>.Some(42);
        var executedAction = "";

        // Act
        option.Match(
            some: value => executedAction = $"Some: {value}",
            none: () => executedAction = "None"
        );

        // Assert
        executedAction.Should().Be("Some: 42");
    }

    [Fact]
    public void Match_ActionOverload_OnNone_ShouldExecuteNoneAction()
    {
        // Arrange
        var option = Option<int>.None();
        var executedAction = "";

        // Act
        option.Match(
            some: value => executedAction = $"Some: {value}",
            none: () => executedAction = "None"
        );

        // Assert
        executedAction.Should().Be("None");
    }

    [Fact]
    public void Match_WithNullFunctions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act & Assert
        var act1 = () => option.Match(null!, () => "");
        act1.Should().Throw<ArgumentNullException>();

        var act2 = () => option.Match(value => "", null!);
        act2.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetValueOrDefault Tests

    [Fact]
    public void GetValueOrDefault_OnOptionWithValue_ShouldReturnValue()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var result = option.GetValueOrDefault(100);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void GetValueOrDefault_OnNone_ShouldReturnDefaultValue()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var result = option.GetValueOrDefault(100);

        // Assert
        result.Should().Be(100);
    }

    [Fact]
    public void GetValueOrDefault_WithFactory_OnOptionWithValue_ShouldReturnValue()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var result = option.GetValueOrDefault(() => 100);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void GetValueOrDefault_WithFactory_OnNone_ShouldReturnFactoryResult()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var result = option.GetValueOrDefault(() => 100);

        // Assert
        result.Should().Be(100);
    }

    [Fact]
    public void GetValueOrDefault_WithNullFactory_ShouldThrowArgumentNullException()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act & Assert
        var act = () => option.GetValueOrDefault(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Or Tests

    [Fact]
    public void Or_OnOptionWithValue_ShouldReturnOriginalOption()
    {
        // Arrange
        var option = Option<int>.Some(42);
        var alternative = Option<int>.Some(100);

        // Act
        var result = option.Or(alternative);

        // Assert
        result.Should().Be(option);
    }

    [Fact]
    public void Or_OnNone_ShouldReturnAlternative()
    {
        // Arrange
        var option = Option<int>.None();
        var alternative = Option<int>.Some(100);

        // Act
        var result = option.Or(alternative);

        // Assert
        result.Should().Be(alternative);
    }

    [Fact]
    public void Or_WithFactory_OnOptionWithValue_ShouldReturnOriginalOption()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var result = option.Or(() => Option<int>.Some(100));

        // Assert
        result.Should().Be(option);
    }

    [Fact]
    public void Or_WithFactory_OnNone_ShouldReturnFactoryResult()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var result = option.Or(() => Option<int>.Some(100));

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(100);
    }

    [Fact]
    public void Or_WithNullFactory_ShouldThrowArgumentNullException()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act & Assert
        var act = () => option.Or(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Filter Tests

    [Fact]
    public void Filter_OnOptionWithValue_WithTruePredicate_ShouldReturnOption()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var filtered = option.Filter(x => x > 40);

        // Assert
        filtered.Should().Be(option);
    }

    [Fact]
    public void Filter_OnOptionWithValue_WithFalsePredicate_ShouldReturnNone()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var filtered = option.Filter(x => x > 50);

        // Assert
        filtered.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Filter_OnNone_ShouldReturnNone()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var filtered = option.Filter(x => x > 40);

        // Assert
        filtered.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Where_ShouldBehaveLikeFilter()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var filtered = option.Filter(x => x > 40);
        var whereResult = option.Where(x => x > 40);

        // Assert
        whereResult.Should().Be(filtered);
    }

    [Fact]
    public void Filter_WithNullPredicate_ShouldThrowArgumentNullException()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act & Assert
        var act = () => option.Filter(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Do/Tap Tests

    [Fact]
    public void Do_OnOptionWithValue_ShouldExecuteAction()
    {
        // Arrange
        var option = Option<int>.Some(42);
        var executed = false;

        // Act
        var result = option.Do(x => executed = true);

        // Assert
        executed.Should().BeTrue();
        result.Should().Be(option);
    }

    [Fact]
    public void Do_OnNone_ShouldNotExecuteAction()
    {
        // Arrange
        var option = Option<int>.None();
        var executed = false;

        // Act
        var result = option.Do(x => executed = true);

        // Assert
        executed.Should().BeFalse();
        result.Should().Be(option);
    }

    [Fact]
    public void Tap_ShouldBehaveLikeDo()
    {
        // Arrange
        var option = Option<int>.Some(42);
        var executed = false;

        // Act
        var result = option.Tap(x => executed = true);

        // Assert
        executed.Should().BeTrue();
        result.Should().Be(option);
    }

    [Fact]
    public void Do_WithNullAction_ShouldThrowArgumentNullException()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act & Assert
        var act = () => option.Do(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void ToNullable_OnOptionWithValue_ShouldReturnValue()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var result = option.ToNullable();

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void ToNullable_OnNone_ShouldReturnNull()
    {
        // Arrange
        var option = Option<string>.None();

        // Act
        var result = option.ToNullable();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToResult_OnOptionWithValue_ShouldReturnSuccess()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var result = option.ToResult("Value not found");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ToResult_OnNone_ShouldReturnError()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var result = option.ToResult("Value not found");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Value not found");
    }

    [Fact]
    public void ToResult_WithFactory_OnNone_ShouldReturnErrorFromFactory()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var result = option.ToResult(() => "Generated error message");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Generated error message");
    }

    [Fact]
    public void ToEnumerable_OnOptionWithValue_ShouldReturnSingleItemEnumerable()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var result = option.ToEnumerable();

        // Assert
        result.Should().ContainSingle().Which.Should().Be(42);
    }

    [Fact]
    public void ToEnumerable_OnNone_ShouldReturnEmptyEnumerable()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var result = option.ToEnumerable();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Static Utility Methods Tests

    [Fact]
    public void Combine_AllSome_ShouldReturnSomeWithAllValues()
    {
        // Arrange
        var options = new[] { Option<int>.Some(1), Option<int>.Some(2), Option<int>.Some(3) };

        // Act
        var combined = Option.Combine(options);

        // Assert
        combined.HasValue.Should().BeTrue();
        combined.Value.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void Combine_SomeNone_ShouldReturnNone()
    {
        // Arrange
        var options = new[] { Option<int>.Some(1), Option<int>.None(), Option<int>.Some(3) };

        // Act
        var combined = Option.Combine(options);

        // Assert
        combined.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Traverse_AllSome_ShouldReturnSomeWithAllResults()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };

        // Act
        var result = Option.Traverse(
            items,
            x => x > 0 ? Option<int>.Some(x * 2) : Option<int>.None()
        );

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new[] { 2, 4, 6 });
    }

    [Fact]
    public void Traverse_SomeNone_ShouldReturnNone()
    {
        // Arrange
        var items = new[] { 1, -2, 3 };

        // Act
        var result = Option.Traverse(
            items,
            x => x > 0 ? Option<int>.Some(x * 2) : Option<int>.None()
        );

        // Assert
        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public void FirstSome_WithSomeOptions_ShouldReturnFirstSome()
    {
        // Arrange
        var options = new[] { Option<int>.None(), Option<int>.Some(42), Option<int>.Some(100) };

        // Act
        var result = Option.FirstSome(options);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void FirstSome_WithAllNone_ShouldReturnNone()
    {
        // Arrange
        var options = new[] { Option<int>.None(), Option<int>.None(), Option<int>.None() };

        // Act
        var result = Option.FirstSome(options);

        // Assert
        result.IsNone.Should().BeTrue();
    }

    #endregion

    #region Implicit Operator Tests

    [Fact]
    public void ImplicitOperator_FromValue_ShouldCreateSome()
    {
        // Arrange & Act
        Option<int> option = 42;

        // Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().Be(42);
    }

    [Fact]
    public void ExplicitOperator_ToNullable_ShouldReturnNullableValue()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var result = (int?)option;

        // Assert
        result.Should().Be(42);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_TwoSomeWithSameValue_ShouldBeEqual()
    {
        // Arrange
        var option1 = Option<int>.Some(42);
        var option2 = Option<int>.Some(42);

        // Act & Assert
        option1.Equals(option2).Should().BeTrue();
        (option1 == option2).Should().BeTrue();
        (option1 != option2).Should().BeFalse();
    }

    [Fact]
    public void Equals_TwoSomeWithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var option1 = Option<int>.Some(42);
        var option2 = Option<int>.Some(24);

        // Act & Assert
        option1.Equals(option2).Should().BeFalse();
        (option1 == option2).Should().BeFalse();
        (option1 != option2).Should().BeTrue();
    }

    [Fact]
    public void Equals_TwoNone_ShouldBeEqual()
    {
        // Arrange
        var option1 = Option<int>.None();
        var option2 = Option<int>.None();

        // Act & Assert
        option1.Equals(option2).Should().BeTrue();
        (option1 == option2).Should().BeTrue();
        (option1 != option2).Should().BeFalse();
    }

    [Fact]
    public void Equals_SomeAndNone_ShouldNotBeEqual()
    {
        // Arrange
        var option1 = Option<int>.Some(42);
        var option2 = Option<int>.None();

        // Act & Assert
        option1.Equals(option2).Should().BeFalse();
        (option1 == option2).Should().BeFalse();
        (option1 != option2).Should().BeTrue();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_OnSome_ShouldReturnSomeFormat()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var stringRepresentation = option.ToString();

        // Assert
        stringRepresentation.Should().Be("Some(42)");
    }

    [Fact]
    public void ToString_OnNone_ShouldReturnNoneFormat()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var stringRepresentation = option.ToString();

        // Assert
        stringRepresentation.Should().Be("None");
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void Option_ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var option = Option<int>.Some(42);
        var exceptions = new ConcurrentBag<Exception>();
        var values = new ConcurrentBag<int>();

        // Act
        Parallel.For(
            0,
            1000,
            i =>
            {
                try
                {
                    values.Add(option.Value);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        );

        // Assert
        exceptions.Should().BeEmpty();
        values.Should().HaveCount(1000);
        values.Should().AllSatisfy(v => v.Should().Be(42));
    }

    [Fact]
    public void Option_ConcurrentMap_ShouldBeThreadSafe()
    {
        // Arrange
        var option = Option<int>.Some(42);
        var exceptions = new ConcurrentBag<Exception>();
        var mappedOptions = new ConcurrentBag<Option<string>>();

        // Act
        Parallel.For(
            0,
            1000,
            i =>
            {
                try
                {
                    var mapped = option.Map(x => x.ToString());
                    mappedOptions.Add(mapped);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        );

        // Assert
        exceptions.Should().BeEmpty();
        mappedOptions.Should().HaveCount(1000);
        mappedOptions
            .Should()
            .AllSatisfy(o =>
            {
                o.HasValue.Should().BeTrue();
                o.Value.Should().Be("42");
            });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GetHashCode_ShouldBeConsistent()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var hash1 = option.GetHashCode();
        var hash2 = option.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_ForDifferentValues_ShouldBeDifferent()
    {
        // Arrange
        var option1 = Option<int>.Some(42);
        var option2 = Option<int>.Some(24);

        // Act
        var hash1 = option1.GetHashCode();
        var hash2 = option2.GetHashCode();

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Option_WithComplexTypes_ShouldWork()
    {
        // Arrange
        var person = new Person("John", 30);
        var option = Option<Person>.Some(person);

        // Act & Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().Be(person);
        option.Value.Name.Should().Be("John");
        option.Value.Age.Should().Be(30);
    }

    [Fact]
    public void Option_WithNullableTypes_ShouldWork()
    {
        // Arrange & Act
        var option1 = Option<int?>.Some(42);
        var option2 = Option<int?>.Some(null);

        // Assert
        option1.HasValue.Should().BeTrue();
        option1.Value.Should().Be(42);

        option2.HasValue.Should().BeTrue();
        option2.Value.Should().BeNull();
    }

    #endregion

    #region Helper Types

    private record Person(string Name, int Age);

    #endregion
}
