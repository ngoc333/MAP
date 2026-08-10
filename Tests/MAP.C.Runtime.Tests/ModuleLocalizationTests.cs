using System.Reflection;
using System.Xml.Linq;

namespace MAP.C.Runtime.Tests;

public sealed class ModuleLocalizationTests
{
    [Fact]
    public void ReleaseAssemblies_EmbedVietnameseAndEnglishLocalizationResources()
    {
        var repositoryRoot = FindRepositoryRoot();
        var moduleProjects = FindModuleProjects(repositoryRoot);

        Assert.NotEmpty(moduleProjects);

        foreach (var moduleProject in moduleProjects)
        {
            var assemblyPath = FindReleaseAssembly(moduleProject);
            var assembly = LoadAssemblyWithoutLocking(assemblyPath);
            var resources = assembly.GetManifestResourceNames();
            var assemblyName = assembly.GetName().Name;

            Assert.NotNull(assemblyName);
            AssertResourceExists(
                moduleProject,
                $"{assemblyName}.Localization.vi.json",
                resources);
            AssertResourceExists(
                moduleProject,
                $"{assemblyName}.Localization.en.json",
                resources);
        }
    }

    private static void AssertResourceExists(
        string moduleProjectPath,
        string resourceName,
        IReadOnlyCollection<string> resources)
    {
        Assert.True(
            resources.Contains(resourceName, StringComparer.Ordinal),
            $"Module '{Path.GetFileNameWithoutExtension(moduleProjectPath)}' is missing embedded resource '{resourceName}'.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Modules")) &&
                File.Exists(Path.Combine(directory.FullName, "MAP.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the repository root from '{AppContext.BaseDirectory}'.");
    }

    private static IReadOnlyList<string> FindModuleProjects(string repositoryRoot)
    {
        var modulesRoot = Path.Combine(repositoryRoot, "Modules");
        return Directory.GetDirectories(modulesRoot)
            .SelectMany(moduleDirectory => Directory.GetFiles(
                moduleDirectory,
                "*.csproj",
                SearchOption.TopDirectoryOnly))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string FindReleaseAssembly(string moduleProjectPath)
    {
        var projectDirectory = Path.GetDirectoryName(moduleProjectPath)!;
        var projectFile = XDocument.Load(moduleProjectPath);
        var assemblyName = projectFile.Root?
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "AssemblyName")?
            .Value;

        assemblyName = string.IsNullOrWhiteSpace(assemblyName)
            ? Path.GetFileNameWithoutExtension(moduleProjectPath)
            : assemblyName.Trim();

        var releaseDirectory = Path.Combine(projectDirectory, "bin", "Release");
        var assemblyPath = Directory.Exists(releaseDirectory)
            ? Directory.GetFiles(
                    releaseDirectory,
                    $"{assemblyName}.dll",
                    SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
            : null;

        Assert.True(
            assemblyPath is not null,
            $"Module '{Path.GetFileNameWithoutExtension(moduleProjectPath)}' has no Release assembly at '{releaseDirectory}'.");

        return assemblyPath!;
    }

    private static Assembly LoadAssemblyWithoutLocking(string assemblyPath)
    {
        var bytes = File.ReadAllBytes(assemblyPath);
        return Assembly.Load(bytes);
    }
}
