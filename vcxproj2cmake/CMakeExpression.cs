using System.Text;

namespace vcxproj2cmake;

class CMakeExpression : IComparable, IComparable<CMakeExpression>, IEquatable<CMakeExpression>
{
    public string Value { get; }

    CMakeExpression(string value)
    {
        Value = value;
    }

    public static CMakeExpression Literal(string literal)
    {
        return new CMakeExpression(Escape(literal));
    }

    public static CMakeExpression Expression(string expression)
    {
        return new CMakeExpression(expression);
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
                '$' => "\\$",   // variable expansion
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
                c == '$';                // variable expansion

            // We prefer to not quote generator expressions since it is not necessary
            if (Value.StartsWith("$<") && Value.EndsWith(">"))
                return false;

            return Value.Length == 0 || Value.Any(NeedsQuoting);
        }
    }

    public override string ToString()
    {
        if (NeedsQuoting)
            return $"\"{Value}\"";
        else
            return Value;
    }

    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType())
            return false;

        var expression = (CMakeExpression)obj;
        return Value == expression.Value;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
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

        return Value.CompareTo(other.Value);
    }

    public bool Equals(CMakeExpression? other)
    {
        if (other is null)
            return false;

        if (GetType() != other.GetType())
            return false;

        return Value.Equals(other.Value);
    }

    public bool Equals(CMakeExpression? other, StringComparison comparisonType)
    {
        if (other is null)
            return false;

        if (GetType() != other.GetType())
            return false;

        return Value.Equals(other.Value, comparisonType);
    }

    public static bool operator ==(CMakeExpression? expression1, CMakeExpression? expression2)
    {
        if (expression1 is null || expression2 is null)
            return object.ReferenceEquals(expression1, expression2);

        if (expression1.GetType() != expression2.GetType())
            return false;

        return expression1.Value == expression2.Value;
    }

    public static bool operator !=(CMakeExpression? expression1, CMakeExpression? expression2)
    {
        return !(expression1 == expression2);
    }

    public static CMakeExpression operator +(CMakeExpression expression1, CMakeExpression expression2)
    {
        return new CMakeExpression(expression1.Value + expression2.Value);
    }
}
