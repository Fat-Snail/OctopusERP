using FluentAssertions;
using OctopusOA.Api.Workflow;

namespace OctopusOA.Api.Tests;

public class ExpressionEvaluatorTests
{
    private static Dictionary<string, object?> Data(params (string, object?)[] kv)
        => kv.ToDictionary(x => x.Item1, x => x.Item2);

    [Fact]
    public void Numeric_Comparison()
    {
        ExpressionEvaluator.Evaluate("amount >= 5000", Data(("amount", 8000L))).Should().BeTrue();
        ExpressionEvaluator.Evaluate("amount >= 5000", Data(("amount", 100L))).Should().BeFalse();
        ExpressionEvaluator.Evaluate("amount < 1000", Data(("amount", 500L))).Should().BeTrue();
    }

    [Fact]
    public void String_Equality()
    {
        ExpressionEvaluator.Evaluate("category == \"urgent\"", Data(("category", "urgent"))).Should().BeTrue();
        ExpressionEvaluator.Evaluate("category != 'urgent'", Data(("category", "normal"))).Should().BeTrue();
    }

    [Fact]
    public void Logical_And_Or()
    {
        ExpressionEvaluator.Evaluate("amount >= 5000 && category == 'urgent'",
            Data(("amount", 8000L), ("category", "urgent"))).Should().BeTrue();

        ExpressionEvaluator.Evaluate("amount >= 5000 || category == 'urgent'",
            Data(("amount", 100L), ("category", "normal"))).Should().BeFalse();
    }

    [Fact]
    public void Parentheses_Precedence()
    {
        ExpressionEvaluator.Evaluate("(a == 1 || b == 2) && c == 3",
            Data(("a", 9L), ("b", 2L), ("c", 3L))).Should().BeTrue();

        ExpressionEvaluator.Evaluate("(a == 1 || b == 2) && c == 3",
            Data(("a", 9L), ("b", 9L), ("c", 3L))).Should().BeFalse();
    }

    [Fact]
    public void Negation()
    {
        ExpressionEvaluator.Evaluate("!done", Data(("done", false))).Should().BeTrue();
        ExpressionEvaluator.Evaluate("!(amount < 100)", Data(("amount", 500L))).Should().BeTrue();
    }

    [Fact]
    public void MissingField_IsNull()
    {
        ExpressionEvaluator.Evaluate("amount >= 5000", Data(("other", 1L))).Should().BeFalse();
        // 缺失字段视为 null，null == null 成立
        ExpressionEvaluator.Evaluate("category == null", Data()).Should().BeTrue();
        ExpressionEvaluator.Evaluate("category == null", Data(("category", "x"))).Should().BeFalse();
    }
}
