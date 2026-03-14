using System.Text;

namespace vcxproj2cmake;

class CMakeExpression : IComparable, IComparable<CMakeExpression>
{
    public string Value { get; }
    public bool QuoteVariablesWhenStandalone { get; }

    private CMakeExpression(string value, bool quoteVariablesWhenStandalone)
    {
        Value = value;
        QuoteVariablesWhenStandalone = quoteVariablesWhenStandalone;
    }

    public static CMakeExpression Literal(string literal, bool quoteVariablesWhenStandalone = false)
    {
        return new CMakeExpression(Escape(literal), quoteVariablesWhenStandalone);
    }

    public static CMakeExpression Expression(string expression, bool quoteVariablesWhenStandalone = false)
    {
        return new CMakeExpression(expression, quoteVariablesWhenStandalone);
    }

    public CMakeExpression WithQuotedVariablesWhenStandalone()
    {
        return QuoteVariablesWhenStandalone
            ? this
            : new CMakeExpression(Value, quoteVariablesWhenStandalone: true);
    }

    public static CMakeExpression operator +(CMakeExpression expression1, CMakeExpression expression2)
    {
        return new CMakeExpression(
            expression1.Value + expression2.Value,
            expression1.QuoteVariablesWhenStandalone || expression2.QuoteVariablesWhenStandalone);
    }

    static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);

        foreach (char c in value)
            sb.Append(c switch
            {
                '\\' => "\\\\", // backslash
                '\"' => "\\\"", // quote
                '\n' => "\\n",  // newline
                '\r' => "\\r",  // carriage return
                '\t' => "\\t",  // tab
                _ => c.ToString()
            });

        return sb.ToString();
    }

    bool NeedsQuoting
    {
        get
        {
            bool NeedsQuoting(char c) =>
                char.IsWhiteSpace(c) ||  // space, tab, newline …
                c == ';' ||              // list separator inside variables
                c == '#' ||              // comment introducer
                c == '(' || c == ')' ||  // command delimiters
                c == '"' || c == '\\' || // must be escaped inside quotes
                (QuoteVariablesWhenStandalone && c == '$');

            return Value.Length == 0 || Value.Any(NeedsQuoting);
        }
    }

    public string RenderStandalone()
    {
        if (NeedsQuoting)
            return $"\"{Value}\"";
        else
            return Value;
    }

    public override string ToString() => RenderStandalone();

    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType())        
            return false;        

        var expression = (CMakeExpression)obj;
        return Value == expression.Value
            && QuoteVariablesWhenStandalone == expression.QuoteVariablesWhenStandalone;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, QuoteVariablesWhenStandalone);
    }

    public int CompareTo(object? obj)
    {
        if (obj is null)
            return 1;

        if (obj is not CMakeExpression otherExpression)
            throw new ArgumentException($"Object must be of type {nameof(CMakeExpression)}.", nameof(obj));

        return CompareTo(otherExpression);
    }

    public int CompareTo(CMakeExpression? other)
    {
        if (other is null)
            return 1;

        var valueComparison = StringComparer.Ordinal.Compare(Value, other.Value);
        if (valueComparison != 0)
            return valueComparison;

        return QuoteVariablesWhenStandalone.CompareTo(other.QuoteVariablesWhenStandalone);
    }

    public static bool operator ==(CMakeExpression? expression1, CMakeExpression? expression2)
    {
        if (expression1 is null || expression2 is null)
            return object.ReferenceEquals(expression1, expression2);

        if (expression1.GetType() != expression2.GetType())
            return false;

        return expression1.Value == expression2.Value
            && expression1.QuoteVariablesWhenStandalone == expression2.QuoteVariablesWhenStandalone;
    }

    public static bool operator !=(CMakeExpression? expression1, CMakeExpression? expression2)
    { 
        return !(expression1 == expression2);
    }
}
