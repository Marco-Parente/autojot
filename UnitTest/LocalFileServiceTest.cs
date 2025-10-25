using Core;
using Core.Services;
using Core.Services.Files;

namespace UnitTest;

public class LocalFileServiceTest
{
    [Fact]
    public void Test1() { }

    [Fact]
    public void UpsertFile_CreatesAndUpdatesFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var relativeFilePath = "testfile.txt";
        var initialContent = "Hello, world!";
        var updatedContent = "Updated content.";
        var service = new LocalFileService();

        try
        {
            // Act - create
            service.UpsertFile(tempDir, relativeFilePath, initialContent);
            var fullPath = Path.Combine(tempDir, relativeFilePath);
            Assert.True(File.Exists(fullPath));
            Assert.Equal(initialContent, File.ReadAllText(fullPath));
            var fileCreationDate = File.GetCreationTime(fullPath);
            var fileModificationDate = File.GetLastWriteTime(fullPath);

            // Act - update
            service.UpsertFile(tempDir, relativeFilePath, updatedContent);
            Assert.Equal(updatedContent, File.ReadAllText(fullPath));
            Assert.Equal(fileCreationDate, File.GetCreationTime(fullPath));
            Assert.True(File.GetLastWriteTime(fullPath) > fileModificationDate);
        }
        finally
        {
            // Cleanup
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetMatchResults_ReturnsCorrectScores()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var service = new LocalFileService();

        // Create files with tags in YAML front matter and file names
        var file1 = "note1.md";
        var content1 = "---\ntags:\n  - keyword\n  - test\n---\nContent 1";

        var file2 = "project-keyword.md";
        var content2 = "---\ntags:\n  - other\n---\nContent 2";

        var file3 = "random.md";
        var content3 = "No front matter here.";

        File.WriteAllText(Path.Combine(tempDir, file1), content1);
        File.WriteAllText(Path.Combine(tempDir, file2), content2);
        File.WriteAllText(Path.Combine(tempDir, file3), content3);

        var keywords = new List<string> { "keyword", "project" };

        try
        {
            // Act
            var results = service.GetMatchResults(tempDir, keywords);

            // Assert
            var result1 = results.FirstOrDefault(r => r.RelativeFilePath == file1);
            var result2 = results.FirstOrDefault(r => r.RelativeFilePath == file2);
            var result3 = results.FirstOrDefault(r => r.RelativeFilePath == file3);

            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.NotNull(result3);

            // file1: tag "keyword" matches (score +1)
            Assert.Equal(1, result1.Score);

            // file2: file name contains "project" and "keyword" (score +4)
            Assert.Equal(4, result2.Score);

            // file3: no tags, no file name match
            Assert.Equal(0, result3.Score);

            // Results should be sorted descending by score
            Assert.Equal(
                new[] { file2, file1, file3 },
                results.Select(r => r.RelativeFilePath).ToArray()
            );
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
