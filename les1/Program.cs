using System.CommandLine;
using System.Globalization;
 
var rootCommand = new RootCommand("Root command for file bundle CLI");
var bundleCommand = new Command("bundle", "bundle code file");
rootCommand.Add(bundleCommand);

var languageOption = new Option<string[]>("--language", "-l")
{
    Description = "list of language program",
    Required = true,
    AllowMultipleArgumentsPerToken = true,
};
var outputOption = new Option<FileInfo>("--output", "-o")
{
    Description = "fileName and path",
    DefaultValueFactory = _ => new FileInfo("bundle.txt"),
};
var noteOption = new Option<bool>("--note", "-n")
{
    Description = "if to show the rsource"
};
var sortOption = new Option<string>("--sort", "-s")
{
    Description = "how to sort",
    DefaultValueFactory = _ => "name",
};
sortOption.AcceptOnlyFromAmong("name", "type");

var remove_empty_linesOption = new Option<bool>("--remove_empty_lines", "-r")
{
    Description = "if to remove empty lines",
};
var authorOption = new Option<string>("--author", "-a")
{
    Description = "author name",
};

bundleCommand.Options.Add(authorOption);
bundleCommand.Options.Add(remove_empty_linesOption);
bundleCommand.Options.Add(sortOption);
bundleCommand.Options.Add(outputOption);
bundleCommand.Options.Add(noteOption);
bundleCommand.Options.Add(languageOption);

static string[] languageFilter(string[] candidates, string[] languages)
{
    if (languages.Any(l => l.Equals("all", StringComparison.OrdinalIgnoreCase)))
    {
        return candidates;
    }

    Dictionary<string, string[]> languageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c#"] = [".cs"],
        ["python"] = [".py", ".pyw"],
        ["java"] = [".java"],
        ["javascript"] = [".js", ".jsx", ".mjs", ".cjs"],
        ["typescript"] = [".ts", ".tsx"],
        ["html"] = [".html", ".htm"],
        ["css"] = [".css", ".scss", ".sass", ".less"],
        ["c"] = [".c", ".h"],
        ["c++"] = [".cpp", ".cc", ".cxx", ".hpp", ".hh", ".hxx"],
    };

    var wantedExtensions = languages
        .Where(lang => languageExtensions.ContainsKey(lang))
        .SelectMany(lang => languageExtensions[lang])
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    return candidates
        .Where(f => wantedExtensions.Contains(Path.GetExtension(f)))
        .ToArray();
}

static string[] sortFiles(string[] files, string sort)
{
    if (sort == "type")
    {
        return files
            .OrderBy(f => Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    return files
        .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static int writeBundle(
    FileInfo output, string[] files, bool note, bool removeEmptyLines, string author, string baseDir)
{
    try
    {
        using var writer = new StreamWriter(output.FullName, append: false);

        if (!string.IsNullOrWhiteSpace(author))
        {
            writer.WriteLine($"// Author: {author}");
            writer.WriteLine();
        }

        foreach (var file in files)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch (Exception)
            {
                Console.Error.WriteLine($"Eror: couldnt read file {file}");
                continue;
            }

            if (removeEmptyLines)
            {
                lines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            }

            if (note)
            {
                var relativePath = Path.GetRelativePath(baseDir, file);
                writer.WriteLine($"// Source: {relativePath}");
            }

            foreach (var line in lines)
            {
                writer.WriteLine(line);
            }

            writer.WriteLine();
        }

        return 0;
    }
    catch (Exception)
    {
        Console.Error.WriteLine("Eror: couldnt create the bundle file");
        return 1;
    }
}

static string Ask(string question)
{
    Console.Write(question);
    return Console.ReadLine()?.Trim() ?? "";
}

static string AskRequired(string question)
{
    string answer;
    do
    {
        answer = Ask(question);
        if (answer == "")
        {
            Console.Error.WriteLine("Eror: this field is required, please enter a value");
        }
    } while (answer == "");

    return answer;
}

static bool AskYesNo(string question)
{
    while (true)
    {
        var answer = Ask($"{question} (y/n): ").ToLowerInvariant();

        if (answer == "y" || answer == "yes") return true;
        if (answer == "n" || answer == "no" || answer == "") return false;

        Console.Error.WriteLine("Eror: please answer y or n");
    }
}

static string AskFromOptions(string question, string[] validOptions, string defaultValue)
{
    while (true)
    {
        var answer = Ask($"{question} ({string.Join("/", validOptions)}) [{defaultValue}]: ").ToLowerInvariant();

        if (answer == "") return defaultValue;
        if (validOptions.Contains(answer)) return answer;

        Console.Error.WriteLine($"Eror: invalid value, must be one of: {string.Join(", ", validOptions)}");
    }
}

bundleCommand.SetAction((parseResult) =>
{
    var languages = parseResult.GetValue(languageOption);
    var output = parseResult.GetValue(outputOption);
    var note = parseResult.GetValue(noteOption);
    var sort = parseResult.GetValue(sortOption);
    var removeEmptyLines = parseResult.GetValue(remove_empty_linesOption);
    var author = parseResult.GetValue(authorOption);

    var baseDir = Directory.GetCurrentDirectory();

    try
    {
        if (output.Directory != null && !output.Directory.Exists)
        {
            output.Directory.Create();
        }
    }
    catch (Exception)
    {
        Console.Error.WriteLine("Eror: couldnt open or create directory");
        return 1;
    }

    string[] allfilse;
    try
    {
        allfilse = Directory.GetFiles(baseDir, "*", SearchOption.AllDirectories);
    }
    catch (Exception)
    {
        Console.Error.WriteLine("Eror:premmision denied");
        return 1;
    }

    var excludedDirs = new[] { "bin", "obj", ".vs", ".git", "node_modules", "packages", "publish" };
    var sep = Path.DirectorySeparatorChar;

    var candidates = allfilse.Where(f =>
        !excludedDirs.Any(d => f.Contains(sep + d + sep)) &&
        !string.Equals(f, output.FullName, StringComparison.OrdinalIgnoreCase))
        .ToArray();

    candidates = languageFilter(candidates, languages);

    if (candidates.Length == 0)
    {
        Console.Error.WriteLine("Eror: no files matched the selected language(s)");
        return 1;
    }

    candidates = sortFiles(candidates, sort);

    return writeBundle(output, candidates, note, removeEmptyLines, author, baseDir);
});

var createRspCommand = new Command("create-rsp", "create a response file with a ready-made bundle command");
rootCommand.Add(createRspCommand);

createRspCommand.SetAction((parseResult) =>
{
    var languages = AskRequired("Languages to bundle (separated by spaces, or 'all'): ");
    var output = Ask("Output file name or path (Enter for default): ");
    var note = AskYesNo("Write the source path as a comment?");
    var sort = AskFromOptions("Sort by", ["name", "type"], "name");
    var removeEmptyLines = AskYesNo("Remove empty lines?");
    var author = Ask("Author name (Enter to skip): ");

    var commandParts = new List<string> { "bundle", languageOption.Name, languages };

    if (output != "")
    {
        commandParts.Add(outputOption.Name);
        commandParts.Add($"\"{output}\"");
    }

    if (note)
    {
        commandParts.Add(noteOption.Name);
    }

    commandParts.Add(sortOption.Name);
    commandParts.Add(sort);

    if (removeEmptyLines)
    {
        commandParts.Add(remove_empty_linesOption.Name);
    }

    if (author != "")
    {
        commandParts.Add(authorOption.Name);
        commandParts.Add($"\"{author}\"");
    }

    var command = string.Join(' ', commandParts);
    const string rspFileName = "response.rsp";

    try
    {
        File.WriteAllText(rspFileName, command);
    }
    catch (Exception)
    {
        Console.Error.WriteLine($"Eror: couldnt save the response file {rspFileName}");
        return 1;
    }

    Console.WriteLine($"response file created: {Path.GetFullPath(rspFileName)}");
    var exeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath);
    Console.WriteLine($"run it with: {exeName} @{rspFileName}"); return 0;
});

rootCommand.Parse(args).Invoke();