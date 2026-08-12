using System.Text;
using System.Text.Json;

namespace Pnle.Infrastructure.Ai;

internal sealed class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        var builder = new StringBuilder(name.Length + 8);

        foreach (var character in name)
        {
            if (char.IsUpper(character) && builder.Length > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
