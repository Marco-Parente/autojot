namespace Core.Shared;

public static class YamlHelper
{
    public static List<string> ExtractTags(string yaml)
    {
        var tags = new List<string>();

        if (string.IsNullOrWhiteSpace(yaml))
            return tags;

        var lines = yaml.Split(["\r\n", "\n"], StringSplitOptions.None);
        var inTags = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.StartsWith("tags:"))
            {
                inTags = true;
                // Handle inline list like: tags: [a, b, c]
                if (line.Contains("[") && line.Contains("]"))
                {
                    var inside = line[(line.IndexOf('[') + 1)..line.IndexOf(']')];
                    tags.AddRange(inside.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0));
                    break;
                }
                continue;
            }

            // Break when we hit another key (like created_at:)
            if (inTags && line.Contains(":") && !line.StartsWith("-"))
                break;

            if (inTags && line.StartsWith("-"))
            {
                var tag = line[1..].Trim();
                if (!string.IsNullOrEmpty(tag))
                    tags.Add(tag);
            }
        }

        return tags;
    }
}
