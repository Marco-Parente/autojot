using Core.Services;
using Core.Services.Bot;

namespace UnitTest;

public class BotServiceTest
{
    public class FileMatchesMenuTests
    {
        [Fact]
        public void FileMatchesMenu_ReturnsExpectedMenu()
        {
            // Arrange
            var matches = new List<MenuOption>
            {
                new() { Name = "file1.md" },
                new() { Name = "file2.md" },
                new() { Name = "file3.md" },
            }
                .Append(new MenuOption { Name = "None", Action = BotAction.Finish })
                .ToList();

            var menu = new MenuMessage { FileOptions = matches };

            // Act
            var message = menu.ToString();

            // Assert
            Assert.Contains("1 - file1.md", message);
            Assert.Contains("2 - file2.md", message);
            Assert.Contains("3 - file3.md", message);
            Assert.Contains("4 - None", message);
        }
    }

    public class GetSelectedOptionTest
    {
        [Fact]
        public void GetSelectedOptionText_ReturnsCorrectOptionAction()
        {
            // Arrange
            var menu = new MenuMessage()
            {
                FileOptions = [new MenuOption { Name = "file1.md", Action = BotAction.UpdateNote }],
                ExtraOptions =
                [
                    new MenuOption { Name = "New file", Action = BotAction.CreateNewNote },
                    new MenuOption { Name = "None", Action = BotAction.Finish },
                ],
            };

            // Act
            var updateFile = menu.GetActionFromOption("1");
            var createFile = menu.GetActionFromOption("2");
            var finish = menu.GetActionFromOption("3");

            // Assert
            Assert.Equal(BotAction.UpdateNote, updateFile?.Action);
            Assert.Equal(BotAction.CreateNewNote, createFile?.Action);
            Assert.Equal(BotAction.Finish, finish?.Action);
        }

        [Fact]
        public void GetSelectedOptionText_ReturnsNullIfNotFound()
        {
            // Arrange
            var menu = new MenuMessage()
            {
                FileOptions = [new MenuOption { Name = "file1.md", Action = BotAction.UpdateNote }],
                ExtraOptions =
                [
                    new MenuOption { Name = "New file", Action = BotAction.CreateNewNote },
                    new MenuOption { Name = "None", Action = BotAction.Finish },
                ],
            };

            // Act
            var notAnOption = menu.GetActionFromOption("4");

            // Assert
            Assert.Null(notAnOption);
        }
    }
}
