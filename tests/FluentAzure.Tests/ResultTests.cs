using System.Collections.Concurrent;
using FluentAssertions;
using FluentAzure.Core;

namespace FluentAzure.Tests.Core;

/// <summary>
/// Comprehensive tests for the Result<T> monad implementation.
/// Tests cover all functionality including success/error scenarios, Map, Bind, Match, operators, and thread safety.
/// </summary>
public class ResultTests
{
    #region Success Creation Tests

    [Fact]
    public void Success_WithValue_ShouldCreateSuccessfulResult()
    {
        // Arrange & Act
        var result = Result<int>.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Success_WithNullValue_ShouldCreateSuccessfulResult()
    {
        // Arrange & Act
        var result = Result<string?>.Success(null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Success_StaticMethod_ShouldCreateSuccessfulResult()
    {
        // Arrange & Act
        var result = Result.Success("test");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("test");
    }

    #endregion

    #region Error Creation Tests

    [Fact]
    public void Error_WithSingleError_ShouldCreateFailedResult()
    {
        // Arrange & Act
        var result = Result<int>.Error("Something went wrong");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(1);
        result.Errors.Should().Contain("Something went wrong");
    }

    [Fact]
    public void Error_WithMultipleErrors_ShouldCreateFailedResult()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2", "Error 3" };

        // Act
        var result = Result<int>.Error(errors);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void Error_WithEmptyErrorCollection_ShouldThrowArgumentException()
    {
        // Arrange
        var emptyErrors = Array.Empty<string>();

        // Act & Assert
        var act = () => Result<int>.Error(emptyErrors);
        act.Should().Throw<ArgumentException>().WithMessage("At least one error must be provided*");
    }

    [Fact]
    public void Error_WithNullErrorCollection_ShouldThrowArgumentNullException()
    {
        // Arrange
        IEnumerable<string> nullErrors = null!;

        // Act & Assert
        var act = () => Result<int>.Error(nullErrors);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Error_StaticMethod_ShouldCreateFailedResult()
    {
        // Arrange & Act
        var result = Result.Error<string>("test error");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(1);
        result.Errors.Should().Contain("test error");
    }

    #endregion

    #region Property Access Tests

    [Fact]
    public void Value_OnSuccessfulResult_ShouldReturnValue()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act & Assert
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Value_OnFailedResult_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var result = Result<int>.Error("Error");

        // Act & Assert
        var act = () => result.Value;
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Cannot access value of a failed result");
    }

    [Fact]
    public void Errors_OnFailedResult_ShouldReturnErrors()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2" };
        var result = Result<int>.Error(errors);

        // Act & Assert
        result.Errors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void Errors_OnSuccessfulResult_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act & Assert
        var act = () => result.Errors;
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Cannot access errors of a successful result");
    }

    #endregion

    #region Map Tests

    [Fact]
    public void Map_OnSuccessfulResult_ShouldTransformValue()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var mapped = result.Map(x => x.ToString());

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be("42");
    }

    [Fact]
    public void Map_OnFailedResult_ShouldPreserveErrors()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2" };
        var result = Result<int>.Error(errors);

        // Act
        var mapped = result.Map(x => x.ToString());

        // Assert
        mapped.IsFailure.Should().BeTrue();
        mapped.Errors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void Map_WithNullTransform_ShouldThrowArgumentNullException()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act & Assert
        var act = () => result.Map<string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Map_ChainedOperations_ShouldWork()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var mapped = result.Map(x => x * 2).Map(x => x.ToString()).Map(x => $"Result: {x}");

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be("Result: 84");
    }

    #endregion

    #region Bind Tests

    [Fact]
    public void Bind_OnSuccessfulResult_ShouldTransformValue()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var bound = result.Bind(x => Result<string>.Success(x.ToString()));

        // Assert
        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be("42");
    }

    [Fact]
    public void Bind_OnSuccessfulResultWithFailingTransform_ShouldReturnFailure()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var bound = result.Bind(x => Result<string>.Error("Transform failed"));

        // Assert
        bound.IsFailure.Should().BeTrue();
        bound.Errors.Should().Contain("Transform failed");
    }

    [Fact]
    public void Bind_OnFailedResult_ShouldPreserveErrors()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2" };
        var result = Result<int>.Error(errors);

        // Act
        var bound = result.Bind(x => Result<string>.Success(x.ToString()));

        // Assert
        bound.IsFailure.Should().BeTrue();
        bound.Errors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void Bind_WithNullTransform_ShouldThrowArgumentNullException()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act & Assert
        var act = () => result.Bind<string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Bind_ChainedOperations_ShouldWork()
    {
        // Arrange
        var result = Result<int>.Success(10);

        // Act
        var bound = result
            .Bind(x => x > 5 ? Result<int>.Success(x * 2) : Result<int>.Error("Too small"))
            .Bind(x =>
                x < 100 ? Result<string>.Success(x.ToString()) : Result<string>.Error("Too large")
            );

        // Assert
        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be("20");
    }

    [Fact]
    public void Bind_ChainedOperations_WithFailure_ShouldStopAtFirstFailure()
    {
        // Arrange
        var result = Result<int>.Success(2);

        // Act
        var bound = result
            .Bind(x => x > 5 ? Result<int>.Success(x * 2) : Result<int>.Error("Too small"))
            .Bind(x =>
                x < 100 ? Result<string>.Success(x.ToString()) : Result<string>.Error("Too large")
            );

        // Assert
        bound.IsFailure.Should().BeTrue();
        bound.Errors.Should().Contain("Too small");
    }

    #endregion

    #region Match Tests

    [Fact]
    public void Match_OnSuccessfulResult_ShouldExecuteSuccessFunction()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var matched = result.Match(
            success => $"Success: {success}",
            errors => $"Errors: {string.Join(", ", errors)}"
        );

        // Assert
        matched.Should().Be("Success: 42");
    }

    [Fact]
    public void Match_OnFailedResult_ShouldExecuteFailureFunction()
    {
        // Arrange
        var result = Result<int>.Error("Something went wrong");

        // Act
        var matched = result.Match(
            success => $"Success: {success}",
            errors => $"Errors: {string.Join(", ", errors)}"
        );

        // Assert
        matched.Should().Be("Errors: Something went wrong");
    }

    [Fact]
    public void Match_ActionOverload_OnSuccessfulResult_ShouldExecuteSuccessAction()
    {
        // Arrange
        var result = Result<int>.Success(42);
        var executedAction = "";

        // Act
        result.Match(
            success => executedAction = $"Success: {success}",
            errors => executedAction = $"Errors: {string.Join(", ", errors)}"
        );

        // Assert
        executedAction.Should().Be("Success: 42");
    }

    [Fact]
    public void Match_ActionOverload_OnFailedResult_ShouldExecuteFailureAction()
    {
        // Arrange
        var result = Result<int>.Error("Something went wrong");
        var executedAction = "";

        // Act
        result.Match(
            success => executedAction = $"Success: {success}",
            errors => executedAction = $"Errors: {string.Join(", ", errors)}"
        );

        // Assert
        executedAction.Should().Be("Errors: Something went wrong");
    }

    [Fact]
    public void Match_WithNullFunctions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act & Assert
        var act1 = () => result.Match(null!, errors => "");
        act1.Should().Throw<ArgumentNullException>();

        var act2 = () => result.Match(success => "", null!);
        act2.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Combine Tests

    [Fact]
    public void Combine_TwoSuccessfulResults_ShouldCombineValues()
    {
        // Arrange
        var result1 = Result<int>.Success(10);
        var result2 = Result<int>.Success(20);

        // Act
        var combined = result1.Combine(result2, (a, b) => a + b);

        // Assert
        combined.IsSuccess.Should().BeTrue();
        combined.Value.Should().Be(30);
    }

    [Fact]
    public void Combine_FirstFailedSecondSuccessful_ShouldReturnFirstErrors()
    {
        // Arrange
        var result1 = Result<int>.Error("Error 1");
        var result2 = Result<int>.Success(20);

        // Act
        var combined = result1.Combine(result2, (a, b) => a + b);

        // Assert
        combined.IsFailure.Should().BeTrue();
        combined.Errors.Should().Contain("Error 1");
    }

    [Fact]
    public void Combine_FirstSuccessfulSecondFailed_ShouldReturnSecondErrors()
    {
        // Arrange
        var result1 = Result<int>.Success(10);
        var result2 = Result<int>.Error("Error 2");

        // Act
        var combined = result1.Combine(result2, (a, b) => a + b);

        // Assert
        combined.IsFailure.Should().BeTrue();
        combined.Errors.Should().Contain("Error 2");
    }

    [Fact]
    public void Combine_BothFailed_ShouldReturnAllErrors()
    {
        // Arrange
        var result1 = Result<int>.Error(new[] { "Error 1", "Error 2" });
        var result2 = Result<int>.Error(new[] { "Error 3", "Error 4" });

        // Act
        var combined = result1.Combine(result2, (a, b) => a + b);

        // Assert
        combined.IsFailure.Should().BeTrue();
        combined.Errors.Should().HaveCount(4);
        combined
            .Errors.Should()
            .BeEquivalentTo(new[] { "Error 1", "Error 2", "Error 3", "Error 4" });
    }

    [Fact]
    public void Combine_StaticMethod_MultipleSuccessful_ShouldReturnAllValues()
    {
        // Arrange
        var results = new[]
        {
            Result<int>.Success(1),
            Result<int>.Success(2),
            Result<int>.Success(3),
        };

        // Act
        var combined = Result.Combine(results);

        // Assert
        combined.IsSuccess.Should().BeTrue();
        combined.Value.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void Combine_StaticMethod_SomeFailures_ShouldReturnAllErrors()
    {
        // Arrange
        var results = new[]
        {
            Result<int>.Success(1),
            Result<int>.Error("Error 1"),
            Result<int>.Success(3),
            Result<int>.Error("Error 2"),
        };

        // Act
        var combined = Result.Combine(results);

        // Assert
        combined.IsFailure.Should().BeTrue();
        combined.Errors.Should().BeEquivalentTo(new[] { "Error 1", "Error 2" });
    }

    #endregion

    #region Implicit Operator Tests

    [Fact]
    public void ImplicitOperator_FromValue_ShouldCreateSuccessfulResult()
    {
        // Arrange & Act
        Result<int> result = 42;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ImplicitOperator_FromErrorString_ShouldCreateFailedResult()
    {
        // Arrange & Act
        Result<int> result = "Something went wrong";

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Something went wrong");
    }

    [Fact]
    public void ImplicitOperator_FromErrorArray_ShouldCreateFailedResult()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2" };

        // Act
        Result<int> result = errors;

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().BeEquivalentTo(errors);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_TwoSuccessfulResultsWithSameValue_ShouldBeEqual()
    {
        // Arrange
        var result1 = Result<int>.Success(42);
        var result2 = Result<int>.Success(42);

        // Act & Assert
        result1.Equals(result2).Should().BeTrue();
        (result1 == result2).Should().BeTrue();
        (result1 != result2).Should().BeFalse();
    }

    [Fact]
    public void Equals_TwoSuccessfulResultsWithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var result1 = Result<int>.Success(42);
        var result2 = Result<int>.Success(24);

        // Act & Assert
        result1.Equals(result2).Should().BeFalse();
        (result1 == result2).Should().BeFalse();
        (result1 != result2).Should().BeTrue();
    }

    [Fact]
    public void Equals_TwoFailedResultsWithSameErrors_ShouldBeEqual()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2" };
        var result1 = Result<int>.Error(errors);
        var result2 = Result<int>.Error(errors);

        // Act & Assert
        result1.Equals(result2).Should().BeTrue();
        (result1 == result2).Should().BeTrue();
        (result1 != result2).Should().BeFalse();
    }

    [Fact]
    public void Equals_TwoFailedResultsWithDifferentErrors_ShouldNotBeEqual()
    {
        // Arrange
        var result1 = Result<int>.Error("Error 1");
        var result2 = Result<int>.Error("Error 2");

        // Act & Assert
        result1.Equals(result2).Should().BeFalse();
        (result1 == result2).Should().BeFalse();
        (result1 != result2).Should().BeTrue();
    }

    [Fact]
    public void Equals_SuccessfulAndFailedResult_ShouldNotBeEqual()
    {
        // Arrange
        var result1 = Result<int>.Success(42);
        var result2 = Result<int>.Error("Error");

        // Act & Assert
        result1.Equals(result2).Should().BeFalse();
        (result1 == result2).Should().BeFalse();
        (result1 != result2).Should().BeTrue();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_OnSuccessfulResult_ShouldReturnSuccessFormat()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var stringRepresentation = result.ToString();

        // Assert
        stringRepresentation.Should().Be("Success(42)");
    }

    [Fact]
    public void ToString_OnFailedResult_ShouldReturnErrorFormat()
    {
        // Arrange
        var result = Result<int>.Error(new[] { "Error 1", "Error 2" });

        // Act
        var stringRepresentation = result.ToString();

        // Assert
        stringRepresentation.Should().Be("Error(Error 1, Error 2)");
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void Result_ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var result = Result<int>.Success(42);
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
                    values.Add(result.Value);
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
    public void Result_ConcurrentMap_ShouldBeThreadSafe()
    {
        // Arrange
        var result = Result<int>.Success(42);
        var exceptions = new ConcurrentBag<Exception>();
        var mappedResults = new ConcurrentBag<Result<string>>();

        // Act
        Parallel.For(
            0,
            1000,
            i =>
            {
                try
                {
                    var mapped = result.Map(x => x.ToString());
                    mappedResults.Add(mapped);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        );

        // Assert
        exceptions.Should().BeEmpty();
        mappedResults.Should().HaveCount(1000);
        mappedResults
            .Should()
            .AllSatisfy(r =>
            {
                r.IsSuccess.Should().BeTrue();
                r.Value.Should().Be("42");
            });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GetHashCode_ShouldBeConsistent()
    {
        // Arrange
        var result1 = Result<int>.Success(42);
        var result2 = Result<int>.Success(42);

        // Act & Assert
        result1.GetHashCode().Should().Be(result2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ForDifferentValues_ShouldBeDifferent()
    {
        // Arrange
        var result1 = Result<int>.Success(42);
        var result2 = Result<int>.Success(24);

        // Act & Assert
        result1.GetHashCode().Should().NotBe(result2.GetHashCode());
    }

    [Fact]
    public void Result_WithComplexTypes_ShouldWork()
    {
        // Arrange
        var person = new Person("John", 30);
        var result = Result<Person>.Success(person);

        // Act & Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(person);
        result.Value.Name.Should().Be("John");
        result.Value.Age.Should().Be(30);
    }

    [Fact]
    public void Result_WithNullableTypes_ShouldWork()
    {
        // Arrange & Act
        var result1 = Result<int?>.Success(42);
        var result2 = Result<int?>.Success(null);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result1.Value.Should().Be(42);

        result2.IsSuccess.Should().BeTrue();
        result2.Value.Should().BeNull();
    }

    #endregion

    #region Helper Types

    private record Person(string Name, int Age);

    #endregion
}
