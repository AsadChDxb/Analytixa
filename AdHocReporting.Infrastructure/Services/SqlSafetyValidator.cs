using System.Text.RegularExpressions;
using AdHocReporting.Domain.Enums;

namespace AdHocReporting.Infrastructure.Services;

public static class SqlSafetyValidator
{
    private static readonly string[] ForbiddenKeywords =
    {
        "DROP ", "DELETE ", "TRUNCATE ", "UPDATE ", "INSERT ", "ALTER ", "MERGE ", "GRANT ", "REVOKE ", "EXECUTE AS ", "XP_"
    };

    public static void ValidateDefinition(DatasourceType type, string definition)
    {
        if (string.IsNullOrWhiteSpace(definition))
        {
            throw new InvalidOperationException("Datasource definition is required.");
        }

        var trimmed = definition.Trim();
        var upper = trimmed.ToUpperInvariant();

        if (type == DatasourceType.Query)
        {
            ValidateSingleSelectQuery(trimmed, upper, "Only SELECT/CTE query is allowed.");
        }

        foreach (var keyword in ForbiddenKeywords)
        {
            if (upper.Contains(keyword))
            {
                throw new InvalidOperationException($"Forbidden SQL keyword detected: {keyword.Trim()}");
            }
        }

        if (Regex.IsMatch(upper, "\\bEXEC\\b") && type != DatasourceType.StoredProcedure)
        {
            throw new InvalidOperationException("EXEC is only allowed for registered stored procedures.");
        }
    }

    public static void ValidateAgentSelectQuery(string selectQuery)
    {
        if (string.IsNullOrWhiteSpace(selectQuery))
        {
            throw new InvalidOperationException("Agent query is required.");
        }

        var trimmed = selectQuery.Trim();
        var upper = trimmed.ToUpperInvariant();
        ValidateSingleSelectQuery(trimmed, upper, "Agent query must be a single SELECT/CTE statement.");

        foreach (var keyword in ForbiddenKeywords)
        {
            if (upper.Contains(keyword))
            {
                throw new InvalidOperationException($"Forbidden SQL keyword detected in agent query: {keyword.Trim()}");
            }
        }

        if (Regex.IsMatch(upper, "\\bEXEC\\b"))
        {
            throw new InvalidOperationException("EXEC is not allowed in agent query.");
        }
    }

    private static void ValidateSingleSelectQuery(string trimmed, string upper, string message)
    {
        if (!upper.StartsWith("SELECT ") && !upper.StartsWith("WITH "))
        {
            throw new InvalidOperationException(message);
        }

        if (HasMultipleStatements(trimmed))
        {
            throw new InvalidOperationException("Multiple SQL statements are not allowed.");
        }
    }

    private static bool HasMultipleStatements(string sql)
    {
        for (var index = 0; index < sql.Length; index++)
        {
            var current = sql[index];
            if (current == '\'')
            {
                index = SkipStringLiteral(sql, index);
                continue;
            }

            if (current == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                index = SkipLineComment(sql, index + 2);
                continue;
            }

            if (current == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                index = SkipBlockComment(sql, index + 2);
                continue;
            }

            if (current == ';' && HasMeaningfulContentAfterTerminator(sql, index + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMeaningfulContentAfterTerminator(string sql, int startIndex)
    {
        for (var index = startIndex; index < sql.Length; index++)
        {
            var current = sql[index];
            if (char.IsWhiteSpace(current))
            {
                continue;
            }

            if (current == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                index = SkipLineComment(sql, index + 2);
                continue;
            }

            if (current == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                index = SkipBlockComment(sql, index + 2);
                continue;
            }

            return true;
        }

        return false;
    }

    private static int SkipStringLiteral(string sql, int startIndex)
    {
        for (var index = startIndex + 1; index < sql.Length; index++)
        {
            if (sql[index] != '\'')
            {
                continue;
            }

            if (index + 1 < sql.Length && sql[index + 1] == '\'')
            {
                index++;
                continue;
            }

            return index;
        }

        return sql.Length;
    }

    private static int SkipLineComment(string sql, int startIndex)
    {
        for (var index = startIndex; index < sql.Length; index++)
        {
            if (sql[index] == '\n')
            {
                return index;
            }
        }

        return sql.Length;
    }

    private static int SkipBlockComment(string sql, int startIndex)
    {
        for (var index = startIndex; index + 1 < sql.Length; index++)
        {
            if (sql[index] == '*' && sql[index + 1] == '/')
            {
                return index + 1;
            }
        }

        return sql.Length;
    }
}
