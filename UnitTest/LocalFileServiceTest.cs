using Core.Services.Files;

namespace UnitTest;

public class LocalFileServiceTest : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        Guid.NewGuid().ToString()
    );
    private readonly LocalFileService _service = new();

    public LocalFileServiceTest()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void UpsertFile_CreatesAndUpdatesFile()
    {
        // Arrange
        var relativeFilePath = "testfile.md";
        var initialContent = "Hello, world!";
        var updatedContent = "Updated content.";

        // Act - create
        _service.UpsertFile(_tempDir, relativeFilePath, initialContent);
        var fullPath = Path.Combine(_tempDir, relativeFilePath);
        Assert.True(File.Exists(fullPath));
        Assert.Equal(initialContent, File.ReadAllText(fullPath));
        var fileCreationDate = File.GetCreationTime(fullPath);
        var fileModificationDate = File.GetLastWriteTime(fullPath);

        // Act - update
        _service.UpsertFile(_tempDir, relativeFilePath, updatedContent);
        Assert.Equal(updatedContent, File.ReadAllText(fullPath));
        Assert.Equal(fileCreationDate, File.GetCreationTime(fullPath));
        Assert.True(File.GetLastWriteTime(fullPath) > fileModificationDate);
    }

    [Fact]
    public void UpsertFile_CreatesMissingDirectories()
    {
        _service.UpsertFile(_tempDir, "recipes/desserts/cake.md", "content");

        Assert.True(File.Exists(Path.Combine(_tempDir, "recipes", "desserts", "cake.md")));
    }

    [Theory]
    // File paths can be suggested by the AI model, so they are untrusted input.
    [InlineData("../escaped.md")]
    [InlineData("notes/../../escaped.md")]
    [InlineData("/tmp/escaped.md")]
    public void UpsertFile_RejectsPathsOutsideTheVault(string relativeFilePath)
    {
        var escapeTarget = Path.GetFullPath(Path.Combine(_tempDir, relativeFilePath));

        Assert.Throws<VaultPathException>(
            () => _service.UpsertFile(_tempDir, relativeFilePath, "owned")
        );
        Assert.False(File.Exists(escapeTarget));
    }

    [Theory]
    [InlineData("notes/script.sh")]
    [InlineData("secrets.json")]
    public void UpsertFile_RejectsNonMarkdownFiles(string relativeFilePath)
    {
        Assert.Throws<VaultPathException>(
            () => _service.UpsertFile(_tempDir, relativeFilePath, "content")
        );
        Assert.False(File.Exists(Path.Combine(_tempDir, relativeFilePath)));
    }

    [Fact]
    public void FileExists_ReturnsFalseForPathsOutsideTheVault()
    {
        Assert.False(_service.FileExists(_tempDir, "../escaped.md"));
    }

    [Fact]
    public void GetFileContent_RejectsPathsOutsideTheVault()
    {
        Assert.Throws<VaultPathException>(() => _service.GetFileContent(_tempDir, "../escaped.md"));
    }
}
